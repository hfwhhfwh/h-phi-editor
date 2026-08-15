using Godot;

/// <summary>
/// 挂载在 Node2D（如 Sprite2D）上，要求父节点必须是 Control。
/// 按父节点尺寸的 RatioX/RatioY 比例定位自身，父节点大小或 Ratio 变化时自动更新。
/// </summary>
[Tool]
public partial class RatioPositioner : Node2D
{
    private float _ratioX;
    private float _ratioY;
    private Control _parentControl;

    [Export(PropertyHint.Range, "0,1,0.001")]
    public float RatioX
    {
        get => _ratioX;
        set
        {
            _ratioX = value;
            UpdatePosition();
        }
    }

    [Export(PropertyHint.Range, "0,1,0.001")]
    public float RatioY
    {
        get => _ratioY;
        set
        {
            _ratioY = value;
            UpdatePosition();
        }
    }

    private Vector2 _offset = Vector2.Zero;
    /// <summary>像素级微调偏移</summary>
    [Export]
    public Vector2 Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            UpdatePosition();
        }
    }

    public override void _EnterTree()
    {
        if (GetParent() is Control control)
        {
            _parentControl = control;
            _parentControl.Resized += OnParentResized;
        }
        else
        {
            GD.PushError($"[{Name}] 父节点必须是 Control 类型，当前: {GetParent()?.GetType()?.Name ?? "null"}");
        }
    }

    public override void _Ready()
    {
        UpdatePosition();
    }

    public override void _ExitTree()
    {
        if (_parentControl != null)
        {
            _parentControl.Resized -= OnParentResized;
            _parentControl = null;
        }
    }

    private void OnParentResized()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_parentControl == null || !IsInsideTree()) return;

        Vector2 parentSize = _parentControl.Size;
        Position = new Vector2(
            parentSize.X * _ratioX + Offset.X,
            parentSize.Y * _ratioY + Offset.Y
        );
    }

    /// <summary>
    /// 关键兜底：Godot 编辑器有时会绕过 C# setter 直接修改字段，
    /// _Set 是底层钩子，编辑器修改属性时一定会触发。
    /// </summary>
    public override bool _Set(StringName property, Variant value)
    {
        bool result = base._Set(property, value);

        // 无论 base 是否已调用 setter，强制同步并刷新
        if (property == "RatioX" || property == "RatioY" || property == "Offset")
        {
            if (property == "RatioX") _ratioX = value.AsSingle();
            else if (property == "RatioY") _ratioY = value.AsSingle();
            else if (property == "Offset") _offset = value.AsVector2();

            UpdatePosition();
            return true;
        }

        return result;
    }
}