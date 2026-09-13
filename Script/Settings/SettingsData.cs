using Godot;
using System;
using System.Diagnostics.CodeAnalysis;

// [GlobalClass]
public partial class SettingsData : Resource
{
    // [Export] public float MasterVolume { get; set; } = 1.0f;
    // [Export] public float MusicVolume { get; set; } = 0.8f;
    // [Export] public float SfxVolume { get; set; } = 0.9f;
    
    // [Export] public int ResolutionIndex { get; set; } = 0;
    // [Export] public bool Fullscreen { get; set; } = false;
    [Export] public DisplayServer.VSyncMode VSync { get; set; } = DisplayServer.VSyncMode.Mailbox;
    [Export] public int MaxFps { get; set; } = 0; // 无上限
    
    // [Export] public string Language { get; set; } = "zh_CN";
    // [Export] public float CameraSensitivity { get; set; } = 0.5f;
    // [Export] public bool ShowDamageNumbers { get; set; } = true;

    // 资源包
    [Export] public string ResourcePackId { get; set; } = "";
    [Export] public bool UseDefaultResource { get; set; } = true;

    // 谱面播放
    [Export] public bool UseMultiholdHighlight { get; set; } = true;

    // ---------------- 编辑器设置 ----------------
    [Export] public Color HorColor { get; set; } = new Color(1f, 0, 0, 0.686f);
    [Export] public float HorWidth { get; set; } = 1;
    [Export] public Color HorSubColor { get; set; } = new Color(1f, 1f, 0, 0.588f);
    [Export] public float HorSubWidth { get; set; } = 1;
    [Export] public Color VerColor { get; set; } = new Color(0, 1f, 0, 0.588f);
    [Export] public float VerWidth { get; set; } = 1;
	[Export] public Color GroundLineColor { get; set; } = new Color(0.7f, 0.7f, 0.7f, 0.9f);
	[Export] public float GroundLineWidth { get; set; } = 3;

    // 深拷贝，避免引用问题
    public SettingsData Clone()
    {
        return (SettingsData)Duplicate();
    }
}