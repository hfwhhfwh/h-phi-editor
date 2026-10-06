using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

public partial class ImageBlur : Node
{
    public static ImageBlur Instance;

    private SubViewport _vpH;
    private SubViewport _vpV;
    private TextureRect _rectH;
    private TextureRect _rectV;

    private ShaderMaterial _matH;
    private ShaderMaterial _matV;

    public override void _Ready()
    {
        base._Ready();

        // ========== 单例保护 ==========
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            GD.PushWarning($"[{Name}] 单例已存在（{Instance.Name}），销毁当前重复实例");
            QueueFree();  // 自杀，保留旧实例
            return;
        }

        Instance = this;
        // =============================

        // 设置子节点
        _vpH = new SubViewport();
        _vpH.Name = "SubViewport_H";
        AddChild(_vpH);

        _rectH = new TextureRect();
        _rectH.Name = "TextureRect_H";
        _vpH.AddChild(_rectH);

        _vpV = new SubViewport();
        _vpV.Name = "SubViewport_V";
        AddChild(_vpV);

        _rectV = new TextureRect();
        _rectV.Name = "TextureRect_V";
        _vpV.AddChild(_rectV);

        _vpH.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _vpV.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        foreach (var r in new[] { _rectH, _rectV })
        {
            r.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            r.OffsetLeft = r.OffsetTop = r.OffsetRight = r.OffsetBottom = 0;
            r.Visible = true;
        }

        // 设置模糊材质shader
        var shader = GD.Load<Shader>("uid://dxvyy5evfk7uh");
        if(shader == null)
        {
            shader = GD.Load<Shader>("res://Shader/blur.gdshader");
        }
        if(shader == null)
        {
            GD.PushError($"[{Name}] shader 加载失败，模糊将失效");
        }
        
        _matH = new ShaderMaterial { Shader = shader };
        _matV = new ShaderMaterial { Shader = shader };
        _rectH.Material = _matH;
        _rectV.Material = _matV;
    }

    /// <summary>把图片做高斯模糊，返回模糊后的新 Image</summary>
    public async Task<Image> BlurImageAsync(Image src, float radius)
    {
        var size = new Vector2I(src.GetWidth(), src.GetHeight());
        _vpH.Size = size;
        _vpV.Size = size;
        _vpH.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _vpV.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        GD.Print("src: ", src.GetSize(), " format: ", src.GetFormat());
        _rectH.Texture = ImageTexture.CreateFromImage(src);
        GD.Print("rect texture: ", _rectH.Texture.GetSize());

        // 第一遍：横向
        _rectH.Texture = ImageTexture.CreateFromImage(src);
        _matH.SetShaderParameter("direction", new Vector2(1, 0));
        _matH.SetShaderParameter("radius", radius);

        // 等第一遍真正渲染完成
        // 等一整个帧：第 N 帧绘制把 rectH 画进 _vpH，到这里 _vpH 必定有内容
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        // 第二遍：此时 _vpH 的纹理才有内容
        _rectV.Texture = _vpH.GetTexture();
        _matV.SetShaderParameter("direction", new Vector2(0, 1));
        _matV.SetShaderParameter("radius", radius);

        // 再等第二遍渲染完成
        // 等一整个帧：第 N 帧绘制把 rectH 画进 _vpH，到这里 _vpH 必定有内容
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        return _vpV.GetTexture().GetImage();
    }

    public async Task BlurPicture(string picPath, float radius, string outputPath)
    {
        // 生成模糊曲绘
        (Image image, string _) = await FileUtil.LoadImageFromFileAsync(picPath);

        await BlurPicture(image, radius, outputPath);
    }

    public async Task BlurPicture(Image image, float radius, string outputPath)
    {
        // 生成模糊曲绘
        Image blurred = await BlurImageAsync(image, radius);

        // 保存
        string blurredPath = ProjectSettings.GlobalizePath(outputPath);

        Error err = await RunOnMainThreadAsync(() => blurred.SavePng(blurredPath));
        if (err != Error.Ok)
            GD.PrintErr($"模糊图保存失败: {err}");
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

    // ------------ 辅助方法 ------------
    
    /// <summary>
    /// 在主线程上执行一个委托，并异步等待其返回值。
    /// 可从任意线程调用。
    /// </summary>
    public Task<T> RunOnMainThreadAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();

        // CallDeferred 是线程安全的，会在主线程空闲时执行
        Callable.From(() =>
        {
            try   { tcs.SetResult(func()); }
            catch (Exception e) { tcs.SetException(e); }
        }).CallDeferred();

        return tcs.Task;
    }


}
