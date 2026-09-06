using Godot;
using System;
using System.Collections.Generic;

public abstract partial class BaseChartRenderer : Node
{
    public Control Parent { get; set; }
    
    // 开关 
    public bool Disabled { get; set; }

    // 资源包
    protected ResourcePack _resourcePack;
    public Texture2D TapTexture { get; set; }
    public Texture2D DragTexture { get; set; }
    public Texture2D FlickTexture { get; set; }
    public Texture2D HoldHeadTexture { get; set; }
    public Texture2D HoldBodyTexture { get; set; }
    public Texture2D HoldEndTexture { get; set; }
    public Texture2D TapMhTexture { get; set; }
    public Texture2D DragMhTexture { get; set; }
    public Texture2D FlickMhTexture { get; set; }
    public Texture2D HoldHeadMhTexture { get; set; }
    public Texture2D HoldBodyMhTexture { get; set; }
    public Texture2D HoldEndMhTexture { get; set; }
    [Export] protected Texture2D lineTexture;
    public ResourcePack Pack
    {
        set
        {
            _resourcePack = value;
            TapTexture = value.textureDic["click"];
            DragTexture = value.textureDic["drag"];
            FlickTexture = value.textureDic["flick"];
            HoldHeadTexture = value.holdHeadTexture;
            HoldBodyTexture = value.holdBodyTexture;
            HoldEndTexture = value.holdEndTexture;

            TapMhTexture = value.textureDic["click_mh"];
            DragMhTexture = value.textureDic["drag_mh"];
            FlickMhTexture = value.textureDic["flick_mh"];
            HoldHeadMhTexture = value.holdHeadTextureMh;
            HoldBodyMhTexture = value.holdBodyTextureMh;
            HoldEndMhTexture = value.holdEndTextureMh;
        }
    }

    /// <summary>note的宽度大小缩放</summary>
    public float NoteScale { get; protected set; }

    public abstract void UseDefaultResource();

    public abstract void Initialize(Control parent);

    public abstract void Render(
        JudgeLineRenderData[] lineData, int lineCount,
        NoteRenderData[] noteData, int noteCount);
}
