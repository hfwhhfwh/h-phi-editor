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
        var shader = GD.Load<Shader>("res://Shader/blur.gdshader");
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

    public override void _ExitTree()
    {
        // 先清自己的引用，再交给 base
        if (Instance == this)
        {
            Instance = null;
        }
        base._ExitTree();
    }


}
