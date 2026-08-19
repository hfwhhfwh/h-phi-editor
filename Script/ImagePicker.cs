using System;
using Godot;
using Godot.Collections;

public partial class ImagePicker : Node
{
    public static ImagePicker Instance;
    private GodotObject _plugin;
    private const string PluginName = "GodotGetImage";

    public bool IsValid => OS.HasFeature("android") || OS.HasFeature("Android");

    // [Signal]
    // public delegate void ImageLoadedEventHandler(ImageTexture texture);

    public Action<ImageTexture> ImageLoaded;

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

        if(!IsValid) return;

        // 检查并获取插件单例
        if (Engine.HasSingleton(PluginName))
        {
            _plugin = Engine.GetSingleton(PluginName);
            GD.Print("GodotGetImage 插件加载成功");
        }
        else
        {
            GD.PrintErr("无法加载插件: ", PluginName);
            return;
        }

        // 连接插件信号
        _plugin.Connect("image_request_completed", 
            new Callable(this, nameof(OnImageRequestCompleted)));
        _plugin.Connect("error", 
            new Callable(this, nameof(OnImageError)));
        _plugin.Connect("permission_not_granted_by_user", 
            new Callable(this, nameof(OnPermissionDenied)));
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


    /// <summary>
    /// 打开相册选择单张图片
    /// </summary>
    public void PickImageFromGallery(Action<ImageTexture> imageLoaded)
    {
        if(!IsValid) return;
        if (_plugin == null) return;

        ImageLoaded = imageLoaded;

        // 可选：设置图片压缩参数
        // var options = new Dictionary
        // {
        //     ["image_width"] = 1024,
        //     ["image_height"] = 1024,
        //     ["keep_aspect"] = true,
        //     ["image_quality"] = 90,
        //     ["image_format"] = "jpg",
        //     ["auto_rotate_image"] = true
        // };
        // _plugin.Call("setOptions", options);

        // 调用相册选择
        _plugin.Call("getGalleryImage");
    }

    /// <summary>
    /// 打开相册选择多张图片
    /// </summary>
    public void PickMultipleImagesFromGallery()
    {
        if(!IsValid) return;
        if (_plugin == null) return;

        _plugin?.Call("getGalleryImages");
    }

    /// <summary>
    /// 调用相机拍照
    /// </summary>
    public void TakePhoto()
    {
        if(!IsValid) return;
        if (_plugin == null) return;
        
        _plugin?.Call("getCameraImage");
    }

    // 图片选择完成回调
    private void OnImageRequestCompleted(Dictionary result)
    {
        GD.Print("收到图片数据，键: ", result.Keys);

        foreach (var key in result.Keys)
        {
            var imageData = result[key].As<byte[]>();
            if (imageData == null || imageData.Length == 0)
            {
                GD.PrintErr("图片数据为空，键: ", key);
                continue;
            }

            var image = new Image();
            Error err;

            // 根据设置的格式选择加载方式
            // 如果 setOptions 中 image_format 为 "png"，则使用 LoadPngFromBuffer
            err = image.LoadJpgFromBuffer(imageData);
            // err = image.LoadPngFromBuffer(imageData);

            if (err == Error.Ok)
            {
                var texture = ImageTexture.CreateFromImage(image);
                ImageLoaded?.Invoke(texture);
                ImageLoaded = null;
                GD.Print("图片加载成功，尺寸: ", image.GetSize());
            }
            else
            {
                GD.PrintErr("图片加载失败: ", err);
            }
        }
    }

    // 错误回调
    private void OnImageError(string error)
    {
        GD.PrintErr("图片选择错误: ", error);
    }

    // 权限被拒绝回调
    private void OnPermissionDenied()
    {
        GD.Print("用户拒绝了权限，尝试重新请求...");
        _plugin?.Call("resendPermission");
    }
}