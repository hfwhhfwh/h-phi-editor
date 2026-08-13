using Godot;
using System;

[GlobalClass]
public partial class PackManifest : Resource
{
    [Export] public string Name = "Unnamed";
    [Export] public string Author = "";
    [Export] public string Description = "";
    
    // 打击特效图集 [列数, 行数]
    [Export] public Vector2I HitFxGrid = new(5, 6);
    
    // Hold 贴图 [尾部高度, 头部高度]（像素）
    [Export] public Vector2I HoldAtlas = new(50, 50);
    [Export] public Vector2I HoldAtlasMH = new(50, 110);
    
    [Export] public float HitFxDuration = 0.5f;
    [Export] public float HitFxScale = 1.0f;
    [Export] public bool HitFxRotate = false;
    [Export] public bool HitFxTinted = true;
    [Export] public bool HideParticles = false;
    
    // // 颜色（存储为 Color，从 ARGB 转换）
    // [Export] public Color ColorPerfect = new(1f, 0.81f, 0.62f, 0.88f);
    // [Export] public Color ColorGood = new(0.73f, 0.88f, 1f, 0.92f);
}
