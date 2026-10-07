using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// iOS 相册 / 相机取图封装（Godot iOS 插件 PhotoPicker）
///
/// 官方 API（注意：三个枚举都是“插件单例上的属性”，不是 ClassDB 常量）：
///   方法：present(PhotoPickerSourceType)
///         request_permission(PhotoPickerPermissionTarget)
///         permission_status(PhotoPickerPermissionTarget) -> PhotoPickerPermissionStatus
///   信号：image_picked(image: Image)
///         permission_updated(target: int, status: int)   // 文档正文只写了 1 个参数，实际是 2 个
///   属性：SOURCE_PHOTO_LIBRARY / SOURCE_CAMERA_FRONT / SOURCE_CAMERA_REAR / SOURCE_SAVED_PHOTOS_ALBUM
///         PERMISSION_TARGET_PHOTO_LIBRARY / PERMISSION_TARGET_CAMERA
///         PERMISSION_STATUS_UNKNOWN / PERMISSION_STATUS_ALLOWED / PERMISSION_STATUS_DENIED
/// </summary>
public partial class IOSPhotoPicker : Node
{
    public static IOSPhotoPicker Instance;

    private const string PluginName = "PhotoPicker";

    private const string ConstSourcePhotoLibrary = "SOURCE_PHOTO_LIBRARY";
    private const string ConstSourceSavedPhotosAlbum = "SOURCE_SAVED_PHOTOS_ALBUM";
    private const string ConstSourceCameraRear = "SOURCE_CAMERA_REAR";
    private const string ConstSourceCameraFront = "SOURCE_CAMERA_FRONT";

    private const string ConstTargetPhotoLibrary = "PERMISSION_TARGET_PHOTO_LIBRARY";
    private const string ConstTargetCamera = "PERMISSION_TARGET_CAMERA";

    private const string ConstStatusUnknown = "PERMISSION_STATUS_UNKNOWN";
    private const string ConstStatusAllowed = "PERMISSION_STATUS_ALLOWED";
    private const string ConstStatusDenied = "PERMISSION_STATUS_DENIED";

    /// <summary>权限回调迟迟不来时的兜底时间（秒）：直接 present，让 iOS 自己弹授权框</summary>
    private const double PermissionFallbackSeconds = 1.0;

    // 文档给出的枚举顺序，仅作为权限判断的兜底值（SOURCE_* 不做猜测，读不到就报错）
    private const long FallbackStatusUnknown = 0;
    private const long FallbackStatusAllowed = 1;
    private const long FallbackStatusDenied = 2;

    private static readonly StringName PropPresent = "present";
    private static readonly StringName PropRequestPermission = "request_permission";
    private static readonly StringName PropPermissionStatus = "permission_status";
    private static readonly StringName SignalImagePicked = "image_picked";
    private static readonly StringName SignalPermissionUpdated = "permission_updated";

    private GodotObject _plugin;

    /// <summary>是否处于 iOS 运行时（编辑器 / Android / 桌面均为 false）</summary>
    public bool IsValid => OS.HasFeature("ios");

    /// <summary>插件单例是否可用（iOS 上插件没链接进 Xcode 工程时为 false）</summary>
    public bool IsPluginReady => _plugin != null && GodotObject.IsInstanceValid(_plugin);

    /// <summary>最近一次错误信息（供 UI 层展示，避免“点了没反应”）</summary>
    public string LastError { get; private set; } = string.Empty;

    private readonly Dictionary<string, long> _enumCache = new Dictionary<string, long>();

    private Action<Image> _imageLoaded;
    private long _pendingSourceType = -1;
    private bool _waitingPermission;
    private double _permissionDeadline;

    public override void _Ready()
    {
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            GD.PushWarning($"[{Name}] 单例已存在（{Instance.Name}），销毁当前重复实例");
            QueueFree();
            return;
        }

        Instance = this;

        if (!IsValid)
        {
            return;
        }

        if (!Engine.HasSingleton(PluginName))
        {
            LastError = $"{PluginName} 插件单例不存在（插件未链接 / 初始化失败）";
            GD.PrintErr($"[{Name}] {LastError}");
            return;
        }

        _plugin = Engine.GetSingleton(PluginName);
        GD.Print($"[{Name}] registering photo picker: {PluginName}");

        _plugin.Connect(SignalImagePicked, Callable.From<Image>(OnImagePicked));
        _plugin.Connect(SignalPermissionUpdated, Callable.From<long, long>(OnPermissionUpdated));

        LogPluginApi();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (!_waitingPermission)
        {
            return;
        }

        // 兜底：某些系统/版本下 request_permission 的异步 block 可能不回调，
        // 超时后直接 present，UIImagePickerController 仍会弹出系统授权框，避免“点了没反应”。
        _permissionDeadline -= delta;
        if (_permissionDeadline > 0)
        {
            return;
        }

        _waitingPermission = false;
        GD.PushWarning($"[{Name}] 等待 permission_updated 超时，直接 present 兜底");
        PresentPendingSource();
    }

    // ==================== 对外接口 ====================

    /// <summary>从系统相册选择一张图片</summary>
    public void PickImageFromGallery(Action<Image> imageLoaded)
    {
        Pick(ConstSourcePhotoLibrary, ConstTargetPhotoLibrary, imageLoaded);
    }

    /// <summary>从相册中的“已保存”相簿选择一张图片</summary>
    public void PickImageFromSavedPhotos(Action<Image> imageLoaded)
    {
        Pick(ConstSourceSavedPhotosAlbum, ConstTargetPhotoLibrary, imageLoaded);
    }

    /// <summary>调用相机拍照</summary>
    /// <param name="imageLoaded">选图回调</param>
    /// <param name="useFrontCamera">true 用前置，false 用后置</param>
    public void PickImageFromCamera(Action<Image> imageLoaded, bool useFrontCamera = false)
    {
        Pick(useFrontCamera ? ConstSourceCameraFront : ConstSourceCameraRear, ConstTargetCamera, imageLoaded);
    }

    // ==================== 内部流程 ====================

    private void Pick(string sourceConstant, string targetConstant, Action<Image> imageLoaded)
    {
        if (!IsValid)
        {
            Fail("当前不是 iOS 运行环境");
            return;
        }
        if (!IsPluginReady)
        {
            Fail(string.IsNullOrEmpty(LastError) ? $"{PluginName} 插件不可用" : LastError);
            return;
        }

        if (!TryGetEnum(sourceConstant, out long source))
        {
            Fail($"插件未提供 {sourceConstant}，请核对插件版本");
            return;
        }
        if (!TryGetEnum(targetConstant, out long target))
        {
            Fail($"插件未提供 {targetConstant}，请核对插件版本");
            return;
        }

        _imageLoaded = imageLoaded;
        _pendingSourceType = source;
        _waitingPermission = false;

        long status = GetPermissionStatus(target);
        long allowed = GetEnum(ConstStatusAllowed, FallbackStatusAllowed);
        long denied = GetEnum(ConstStatusDenied, FallbackStatusDenied);

        GD.Print($"[{Name}] {sourceConstant}: permission_status={status} (allowed={allowed})");

        if (status == allowed)
        {
            PresentPendingSource();
            return;
        }

        if (status == denied)
        {
            Fail("权限被拒绝，请到 系统设置 → 隐私与安全性 中允许本应用访问");
            return;
        }

        // UNKNOWN 或状态读不到：先请求权限，由 permission_updated 回调触发 present
        _waitingPermission = true;
        _permissionDeadline = PermissionFallbackSeconds;
        GD.Print($"[{Name}] request_permission({targetConstant})");
        CallPlugin(PropRequestPermission, Variant.From(target));
    }

    private void PresentPendingSource()
    {
        if (_pendingSourceType < 0)
        {
            Fail("没有待展示的取图来源");
            return;
        }

        GD.Print($"[{Name}] present(source={_pendingSourceType})");
        CallPlugin(PropPresent, Variant.From(_pendingSourceType));
    }

    private long GetPermissionStatus(long target)
    {
        if (!_plugin.HasMethod(PropPermissionStatus))
        {
            GD.PushWarning($"[{Name}] 插件没有 permission_status()，按 UNKNOWN 处理");
            return GetEnum(ConstStatusUnknown, -1);
        }

        Variant result = _plugin.Call(PropPermissionStatus, Variant.From(target));
        return result.VariantType == Variant.Type.Int ? (long)result : GetEnum(ConstStatusUnknown, -1);
    }

    private void CallPlugin(StringName method, params Variant[] args)
    {
        if (!_plugin.HasMethod(method))
        {
            Fail($"插件没有方法 {method}()，请核对 IOSPhotoPicker 与插件版本是否匹配");
            return;
        }

        try
        {
            _plugin.Call(method, args);
        }
        catch (Exception e)
        {
            Fail($"调用 {method}() 抛出异常: {e.Message}");
        }
    }

    private void Fail(string message)
    {
        LastError = message;
        _imageLoaded = null;
        _waitingPermission = false;
        _pendingSourceType = -1;
        GD.PrintErr($"[{Name}] {message}");
        PopupHelper.Instance?.ShowAlert("选择图片失败", message);
    }

    // ==================== 插件枚举（属性，不是 ClassDB 常量） ====================

    /// <summary>插件把枚举暴露为单例属性，例如 _picker.SOURCE_PHOTO_LIBRARY</summary>
    private bool TryGetEnum(string constantName, out long value)
    {
        value = GetEnum(constantName, long.MinValue);
        return value != long.MinValue;
    }

    private long GetEnum(string constantName, long fallback)
    {
        if (_enumCache.TryGetValue(constantName, out long cached))
        {
            return cached;
        }

        if (TryReadFromPropertyList(constantName, out long fromProperties))
        {
            _enumCache[constantName] = fromProperties;
            return fromProperties;
        }

        if (fallback == long.MinValue)
        {
            GD.PushWarning($"[{Name}] 读不到插件枚举 {constantName}");
        }

        return fallback;
    }

    /// <summary>
    /// 在单例属性与 ClassDB 常量里找枚举值（文档：_picker.SOURCE_PHOTO_LIBRARY 这类属性）。
    /// GetPropertyList() 同时包含脚本/插件的动态属性，所以能把插件 _get() 暴露的枚举也捞出来。
    /// </summary>
    private bool TryReadFromPropertyList(string constantName, out long value)
    {
        value = long.MinValue;

        if (_plugin == null)
        {
            return false;
        }

        Variant direct = _plugin.Get(constantName);
        if (direct.VariantType == Variant.Type.Int)
        {
            value = (long)direct;
            return true;
        }

        if (ClassDB.ClassHasIntegerConstant(PluginName, constantName))
        {
            value = ClassDB.ClassGetIntegerConstant(PluginName, constantName);
            return true;
        }

        foreach (Godot.Collections.Dictionary property in _plugin.GetPropertyList())
        {
            if (!property.ContainsKey("name"))
            {
                continue;
            }

            if (property["name"].AsString() != constantName)
            {
                continue;
            }

            Variant read = _plugin.Get(property["name"].AsString());
            if (read.VariantType == Variant.Type.Int)
            {
                value = (long)read;
                return true;
            }

            if (property.ContainsKey("default_value") && property["default_value"].VariantType == Variant.Type.Int)
            {
                value = (long)property["default_value"];
                return true;
            }
        }

        return false;
    }

    // ==================== 插件回调 ====================

    private void OnImagePicked(Image image)
    {
        if (image == null)
        {
            GD.Print($"[{Name}] 用户取消了选择");
            _imageLoaded = null;
            return;
        }

        GD.Print($"[{Name}] 收到图片，尺寸: {image.GetWidth()}x{image.GetHeight()}");

        Action<Image> callback = _imageLoaded;
        _imageLoaded = null;
        _pendingSourceType = -1;
        callback?.Invoke(image);
    }

    /// <summary>真实签名是 (target, status) 两个 int；写成 (bool granted) 会导致连接无效</summary>
    private void OnPermissionUpdated(long target, long status)
    {
        GD.Print($"[{Name}] permission_updated: target={target}, status={status}");

        if (!_waitingPermission)
        {
            return;
        }

        _waitingPermission = false;

        if (status == GetEnum(ConstStatusAllowed, FallbackStatusAllowed))
        {
            PresentPendingSource();
            return;
        }

        Fail(status == GetEnum(ConstStatusDenied, FallbackStatusDenied)
            ? "用户拒绝了权限"
            : $"权限状态异常（status={status}）");
    }

    private void LogPluginApi()
    {
        GD.Print($"[{Name}] {PluginName} 方法: {DescribeClassMembers(ClassDB.ClassGetMethodList(PluginName, true))}");
        GD.Print($"[{Name}] {PluginName} 信号: {DescribeClassMembers(ClassDB.ClassGetSignalList(PluginName, true))}");
    }

    private static string DescribeClassMembers(Godot.Collections.Array<Godot.Collections.Dictionary> infos)
    {
        List<string> names = new List<string>();
        foreach (Godot.Collections.Dictionary info in infos)
        {
            if (info.ContainsKey("name"))
            {
                names.Add(info["name"].AsString());
            }
        }

        return names.Count > 0 ? string.Join(", ", names) : "(空)";
    }
}
