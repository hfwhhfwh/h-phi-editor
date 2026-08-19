using Godot;
using System;

public partial class IOSPhotoPicker : Node
{
    public static IOSPhotoPicker Instance;
    private GodotObject _picker;
    private const string PluginName = "PhotoPicker";

    private Action<Image> ImageLoaded;

    public override void _Ready()
    {
        // ========== 单例保护 ==========
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            GD.PushWarning($"[{Name}] 单例已存在（{Instance.Name}），销毁当前重复实例");
            QueueFree();  // 自杀，保留旧实例
            return;
        }

        Instance = this;
        // =============================

        // 检查并获取插件单例
        if (Engine.HasSingleton(PluginName))
        {
            _picker = Engine.GetSingleton(PluginName);
            
            // 连接信号
            _picker.Connect("image_picked", new Callable(this, nameof(OnImagePicked)));
            _picker.Connect("permission_updated", new Callable(this, nameof(OnPermissionUpdated)));
            _picker.Connect("error", new Callable(this, nameof(OnError)));
        }
        else
        {
            GD.PrintErr($"{PluginName} 插件不可用，请在 iOS 真机上测试");
        }
    }

    public override void _ExitTree()
    {
        // 先清自己的引用，再交给 base
        if (Instance == this)
        {
            Instance = null;
        }
        base._ExitTree();
    }

    // 打开相册选择图片
    public void PickImageFromGallery(Action<Image> imageLoaded)
    {
        if (_picker != null)
        {
            ImageLoaded = imageLoaded;

            // 参数：来源类型 (0=相册, 1=相机), 是否允许编辑
            _picker.Call("pick_image", 0, false);
        }
    }

    // 信号回调：图片已选择
    private void OnImagePicked(Image image)
    {
        GD.Print("收到图片，尺寸: ", image.GetWidth(), "x", image.GetHeight());
        
        ImageLoaded?.Invoke(image);

        ImageLoaded = null;
    }

    // 信号回调：权限状态更新
    private void OnPermissionUpdated(bool granted)
    {
        GD.Print("相册权限: ", granted);
    }

    // 信号回调：错误
    private void OnError(string error)
    {
        GD.PrintErr("PhotoPicker 错误: ", error);
    }
}