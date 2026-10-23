using Godot;
using System;
using System.Collections.Generic;

public enum ToastColorPreset
{
    OK = 0,
    Warning = 1,
    Error = 2
}

public partial class ToastManager : Node
{
    private const string ToastScenePath = "res://Scene/toast_item.tscn";
    private const double DisplayDuration = 3.0;
    private const float RightMargin = 20.0f;
    private const float TopMargin = 20.0f;
    private const float ToastSpacing = 10.0f;
    private const double AnimationDuration = 0.32;

    private readonly List<ToastEntry> _toasts = new();
    private PackedScene _toastScene;
    private Control _toastLayer;

    private readonly Color _iconColorOk = new Color("6eff9e");
    private readonly Color _textColorOk = new Color("45e66a");
    private readonly Color _iconColorWarning = new Color("ffdc82");
    private readonly Color _textColorWarning = new Color("ffc038");
    private readonly Color _iconColorErr = new Color("ff8a73");
    private readonly Color _textColorErr = new Color("ff5b4d");

    // 内置图标纹理缓存
    private readonly Dictionary<ToastColorPreset, Texture2D> _presetIcons = new();

    // 内置 SVG（24x24，白色填充，便于被 color_icon 着色）
    private const string SvgIconOk =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" fill=\"#ffffff\" viewBox=\"0 0 10 10\"><path d=\"M6.956 2.905a.7.7 0 0 1 .989.99l-3.2 3.2a.7.7 0 0 1-.99 0l-1.7-1.7a.7.7 0 0 1 .99-.99l.265.264c.419.419.628.628.886.649a1 1 0 0 0 .11 0c.257-.02.466-.23.885-.649z\"/></svg>";

    private const string SvgIconWarning =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" fill=\"#ffffff\" viewBox=\"0 0 10 10\"><path d=\"M7.33 4.378c.915 1.624 1.371 2.437 1.1 3.072a1.4 1.4 0 0 1-.176.302C7.834 8.3 6.9 8.3 5.037 8.3s-2.797 0-3.217-.548a1.4 1.4 0 0 1-.177-.302c-.27-.635.186-1.448 1.1-3.072.946-1.682 1.418-2.522 2.116-2.612a1.4 1.4 0 0 1 .355 0c.698.09 1.17.93 2.117 2.612M5.03 6.43a.56.56 0 1 0 0 1.12.56.56 0 0 0 0-1.12m0-3.43c-.448 0-.7.32-.7.63 0 .309.14 1.44.21 1.75.07.309.182.56.49.56.31 0 .421-.251.491-.56s.21-1.441.21-1.75c0-.31-.251-.63-.7-.63\"/></svg>";

    private const string SvgIconError =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" fill=\"#ffffff\" viewBox=\"0 0 10 10\"><path d=\"M6.344 2.667a.7.7 0 0 1 .99.99l-.538.537c-.359.359-.538.538-.556.759a1 1 0 0 0 0 .094c.018.22.197.4.556.76l.538.537a.701.701 0 0 1-.99.989l-.538-.538c-.36-.359-.539-.538-.76-.555a1 1 0 0 0-.093 0c-.221.017-.4.196-.76.555l-.537.538a.7.7 0 0 1-.99-.99l.538-.537c.36-.359.539-.538.556-.759a1 1 0 0 0 0-.094c-.017-.22-.197-.4-.556-.76l-.537-.537a.7.7 0 1 1 .989-.989l.538.538c.359.359.538.538.759.555a1 1 0 0 0 .094 0c.22-.017.4-.196.759-.555z\"/></svg>";


    public static ToastManager Instance { get; private set; }

    public override void _Ready()
    {
        base._Ready();

        _toastScene = GD.Load<PackedScene>(ToastScenePath);

        BuildPresetIcons(); // 预生成内置图标

        var canvasLayer = new CanvasLayer();
        AddChild(canvasLayer);

        _toastLayer = new Control
        {
            Name = "ToastContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _toastLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvasLayer.AddChild(_toastLayer);

        GetViewport().SizeChanged += OnViewportSizeChanged;

        Instance = this;
    }

    public override void _ExitTree()
    {
        Instance = null;

        base._ExitTree();
    }


    public void ShowToast(string text, 
        ToastColorPreset preset = ToastColorPreset.OK, bool insertFirst = false,
        Texture2D customIcon = null)
    {
        if (_toastScene == null || _toastLayer == null)
        {
            GD.PushError("ToastManager could not load the toast scene.");
            return;
        }

        Control toast = _toastScene.Instantiate<Control>();

        (Color iconCol, Color textCol) = GetPresetColor(preset);
        toast.Set("text", text);
        toast.Set("color_icon", iconCol);
        toast.Set("color_text", textCol);

        // 把内置图标交给 ToastItem（属性名按 ToastItem 实际暴露的名字来）
        if (_presetIcons.TryGetValue(preset, out Texture2D iconTex) && iconTex != null)
        {
            toast.Set("icon", customIcon ?? iconTex);
        }

        _toastLayer.AddChild(toast);

        var entry = new ToastEntry(toast);

        if(insertFirst) _toasts.Insert(0, entry);
        else  _toasts.Add(entry);

        var viewportWidth = GetViewport().GetVisibleRect().Size.X;
        toast.Position = new Vector2(viewportWidth, TopMargin);
        PrepareAndShowToast(entry);
    }

    private void BuildPresetIcons()
    {
        _presetIcons[ToastColorPreset.OK]      = CreateSvgTexture(SvgIconOk);
        _presetIcons[ToastColorPreset.Warning] = CreateSvgTexture(SvgIconWarning);
        _presetIcons[ToastColorPreset.Error]   = CreateSvgTexture(SvgIconError);
    }

    private static Texture2D CreateSvgTexture(string svg, float scale = 4.0f)
    {
        Image image = new Image();
        var err = image.LoadSvgFromString(svg, scale);
        if (err != Error.Ok || image == null || image.IsEmpty())
        {
            GD.PushError("ToastManager: failed to build built-in SVG icon.");
            return null;
        }
        
        return ImageTexture.CreateFromImage(image);
    }

    private (Color IconCol, Color TextCol) GetPresetColor(ToastColorPreset preset)
    {
        return preset switch
        {
            ToastColorPreset.OK => (_iconColorOk, _textColorOk),
            ToastColorPreset.Warning => (_iconColorWarning, _textColorWarning),
            ToastColorPreset.Error => (_iconColorErr, _textColorErr),
            _ => throw new NotImplementedException(),
        };
    }

    private async void PrepareAndShowToast(ToastEntry entry)
    {
        // 这里等待两个 ProcessFrame，目的是让 Godot 完成节点布局和最小尺寸计算
        // 因为刚 AddChild 时，文本尺寸可能还没更新
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (!IsInstanceValid(entry.Toast))
        {
            return;
        }

        entry.Toast.Size = entry.Toast.GetCombinedMinimumSize();
        entry.IsMeasured = true;
        ReflowToasts();

        AnimateToX(entry, GetToastX(entry.Toast), true);
    }

    private async void ExpireToast(ToastEntry entry)
    {
        await ToSignal(GetTree().CreateTimer(DisplayDuration), SceneTreeTimer.SignalName.Timeout);

        if (!IsInstanceValid(entry.Toast) || entry.IsExiting)
        {
            return;
        }

        entry.IsExiting = true;
        _toasts.Remove(entry);
        ReflowToasts();

        AnimateToX(entry, GetViewport().GetVisibleRect().Size.X);

        await ToSignal(entry.HorizontalTween, Tween.SignalName.Finished);
        if (IsInstanceValid(entry.Toast))
        {
            entry.Toast.QueueFree();
        }
    }

    /// <summary>
    /// 重新排列所有的Toast
    /// </summary>
    private void ReflowToasts()
    {
        var targetY = TopMargin;
        foreach (var entry in _toasts)
        {
            var toastHeight = entry.IsMeasured
                ? entry.Toast.Size.Y
                : entry.Toast.GetCombinedMinimumSize().Y;
            if (toastHeight <= 0.0f)
            {
                toastHeight = 55.0f;
            }

            if (entry.IsEntered || entry.IsEntering)
            {
                entry.VerticalTween?.Kill();
                entry.VerticalTween = CreateTween()
                    .SetTrans(Tween.TransitionType.Cubic)
                    .SetEase(Tween.EaseType.Out);
                entry.VerticalTween.TweenProperty(entry.Toast, "position:y", targetY, AnimationDuration);
            }
            else
            {
                entry.Toast.Position = new Vector2(entry.Toast.Position.X, targetY);
            }

            targetY += toastHeight + ToastSpacing;
        }
    }

    private float GetToastX(Control toast)
    {
        return GetViewport().GetVisibleRect().Size.X - RightMargin - toast.Size.X;
    }

    private void OnViewportSizeChanged()
    {
        foreach (var entry in _toasts)
        {
            if (!entry.IsMeasured || !IsInstanceValid(entry.Toast))
            {
                continue;
            }

            AnimateToX(entry, GetToastX(entry.Toast), !entry.IsEntered);
        }

        ReflowToasts();
    }

    private void AnimateToX(ToastEntry entry, float targetX, bool markEntered = false)
    {
        entry.HorizontalTween?.Kill();
        entry.HorizontalTween = CreateTween()
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        entry.HorizontalTween.TweenProperty(entry.Toast, "position:x", targetX, AnimationDuration);

        entry.IsEntering = true;

        if (markEntered)
        {
            entry.HorizontalTween.TweenCallback(Callable.From(() =>
            {
                if (!IsInstanceValid(entry.Toast) || entry.IsExiting)
                {
                    return;
                }

                entry.IsEntered = true;
                entry.IsEntering = false;
                ExpireToast(entry);
            }));
        }
    }

    private sealed class ToastEntry
    {
        public Control Toast { get; }
        public bool IsMeasured { get; set; }
        public bool IsEntering { get; set; } = false;
        public bool IsEntered { get; set; }
        public bool IsExiting { get; set; }
        public Tween HorizontalTween { get; set; }
        public Tween VerticalTween { get; set; }

        public ToastEntry(Control toast)
        {
            Toast = toast;
        }
    }
}
