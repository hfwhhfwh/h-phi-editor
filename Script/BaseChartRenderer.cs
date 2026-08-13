using Godot;
using System;
using System.Collections.Generic;

public abstract partial class BaseChartRenderer : Node
{
    public Control Parent { get; set; }
    
    // 开关 
    public bool Disabled { get; set; }

    protected Texture2D _tapTexture;
    protected Texture2D _dragTexture;
    protected Texture2D _flickTexture;
    protected Texture2D _holdHeadTexture;
    protected Texture2D _holdBodyTexture;
    protected Texture2D _holdEndTexture;
    [Export] protected Texture2D lineTexture;
    public ResourcePack Pack
    {
        set
        {
            _tapTexture = value.textureDic["click"];
            _dragTexture = value.textureDic["drag"];
            _flickTexture = value.textureDic["flick"];
            _holdHeadTexture = value.holdHeadTexture;
            _holdBodyTexture = value.holdBodyTexture;
            _holdEndTexture = value.holdEndTexture;
        }
    }

    public abstract void Initialize(Control parent);

    public abstract void Render(
        JudgeLineRenderData[] lineData, int lineCount,
        NoteRenderData[] noteData, int noteCount);
}
