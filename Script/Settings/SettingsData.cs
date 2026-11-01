using Godot;
using System;
using System.Diagnostics.CodeAnalysis;

 [AttributeUsage(AttributeTargets.Property)]
public sealed class ResettableSettingAttribute : Attribute
{
}

// [GlobalClass]
public partial class SettingsData : Resource
{
    // [Export] public float MasterVolume { get; set; } = 1.0f;
    // [Export] public float MusicVolume { get; set; } = 0.8f;
    // [Export] public float SfxVolume { get; set; } = 0.9f;
    
    // [Export] public int ResolutionIndex { get; set; } = 0;
    // [Export] public bool Fullscreen { get; set; } = false;
    [Export, ResettableSetting] public DisplayServer.VSyncMode VSync { get; set; } = DisplayServer.VSyncMode.Mailbox;
    [Export, ResettableSetting] public int MaxFps { get; set; } = 0; // 无上限
    
    // [Export] public string Language { get; set; } = "zh_CN";
    // [Export] public float CameraSensitivity { get; set; } = 0.5f;
    // [Export] public bool ShowDamageNumbers { get; set; } = true;

    // ---------------- 资源包 ----------------
    [Export] public string ResourcePackId { get; set; } = "";
    [Export, ResettableSetting] public bool UseDefaultResource { get; set; } = true;

    // ---------------- 播放设置 ----------------
    [Export, ResettableSetting] public bool UseMultiholdHighlight { get; set; } = true;
    [Export, ResettableSetting] public float NoteScaleEditor { get; set; } = 1.0f;
    [Export, ResettableSetting] public float NoteScalePlay { get; set; } = 1.0f;


    // ---------------- 编辑器设置 ----------------
    [Export, ResettableSetting] public Color HorColor { get; set; } = new Color(1f, 0, 0, 0.686f);
    [Export, ResettableSetting] public float HorWidth { get; set; } = 1;
    [Export, ResettableSetting] public Color HorSubColor { get; set; } = new Color(1f, 1f, 0, 0.588f);
    [Export, ResettableSetting] public float HorSubWidth { get; set; } = 1;
    [Export, ResettableSetting] public Color VerColor { get; set; } = new Color(0, 1f, 0, 0.588f);
    [Export, ResettableSetting] public float VerWidth { get; set; } = 1;
	[Export, ResettableSetting] public Color GroundLineColor { get; set; } = new Color(0.7f, 0.7f, 0.7f, 0.9f);
	[Export, ResettableSetting] public float GroundLineWidth { get; set; } = 3;

    // 深拷贝，避免引用问题
    public SettingsData Clone()
    {
        return (SettingsData)Duplicate();
    }
}