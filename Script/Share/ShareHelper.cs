using Godot;
using System;

public partial class ShareHelper : Node, IShareHelper
{
    /// <summary>
    /// 全局单例入口。在 _Ready 之后可安全访问。
    /// </summary>
    public static ShareHelper Instance { get; private set; }

    /// <summary>
    /// 分享后端（SharePlugin）是否真的可用。
    /// SharePlugin 是 Android 单例，桌面 / Web / 编辑器里都不存在，
    /// 此时 Share 节点的 _ready 会直接 push_error，所以这里先判断平台再创建节点。
    /// </summary>
    public static bool IsAvailable =>
        OS.HasFeature("android") && Engine.HasSingleton("SharePlugin");

    private Node _shareNode;

    public override void _Ready()
    {
        // 1. 设置单例
        Instance = this;

        // 2. 非 Android 平台没有 SharePlugin 单例，创建 Share 节点只会产生
        //    “SharePlugin singleton not found!” 报错，因此直接跳过。
        if (!OS.HasFeature("android"))
        {
            GD.Print("[ShareHelper] 当前平台不支持系统分享，分享功能已禁用");
            return;
        }

        // 3. 动态创建 Share 插件节点并挂到自身下面
        var shareScript = GD.Load<GDScript>("uid://cg0hkbe6r1bil");
        if(shareScript == null)
        {
            shareScript = GD.Load<GDScript>("res://addons/share/Share.gd");
        }
        if (shareScript == null)
        {
            GD.PrintErr("[ShareHelper] 未找到 Share 插件，请确认已启用并安装到 addons/share/ 目录");
            return;
        }

        var shareNode = new Node();
        shareNode.Name = "Share";
        shareNode.SetScript(shareScript);
        AddChild(shareNode);

        _shareNode = shareNode;
    }

    // ---------------- IShareHelper 实现 ----------------

    public void ShareText(string title, string subject, string content)
    {
        if (!EnsureReady()) return;
        _shareNode.Call("share_text", title, subject, content);
    }

    public void ShareImage(string fullPath, string title, string subject, string content)
    {
        if (!EnsureReady()) return;
        _shareNode.Call("share_image", fullPath, title, subject, content);
    }

    public void ShareFile(string path, string mimeType, string title, string subject, string content)
    {
        if (!EnsureReady()) return;
        // Share 插件没有独立的 share_file，share_image 实际上支持任意文件类型，
        // 插件会根据扩展名自动推断 MIME。
        _shareNode.Call("share_image", path, title, subject, content);
    }

    public void ShareTexture(Texture2D texture, string title, string subject, string content)
    {
        if (!EnsureReady()) return;

        // 插件没有直接分享 Texture 的接口，先把 Texture 保存为 user:// 下的 PNG
        string tempPath = "user://share_temp.png";
        var image = texture.GetImage();
        var err = image.SavePng(tempPath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[ShareHelper] 保存临时图片失败: {err}");
            return;
        }

        string fullPath = ProjectSettings.GlobalizePath(tempPath);
        _shareNode.Call("share_image", fullPath, title, subject, content);
    }

    public void ShareViewport(Viewport viewport, string title, string subject, string content)
    {
        if (!EnsureReady()) return;
        _shareNode.Call("share_viewport", viewport, title, subject, content);
    }

    // ---------------- 内部工具 ----------------

    private bool EnsureReady()
    {
        if (_shareNode == null)
        {
            GD.PrintErr("[ShareHelper] Share 节点尚未就绪，无法调用分享功能");
            return false;
        }
        return true;
    }
}