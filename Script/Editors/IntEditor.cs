using Godot;
using HPhiEditorGame.Editor;

public partial class IntEditor : PropertyEditorBase<int>
{
    private int _value;
    private SpinBox _spin;
    private readonly NumericFieldOptions _options;

    public IntEditor(NumericFieldOptions options = null) { _options = options; }

    public override int Value
    {
        get => _value;
        set
        {
            _value = value;
            if (_spin != null) _spin.SetValueNoSignal(value);
        }
    }

    protected override void BuildUI()
    {
        _spin = new SpinBox
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AllowGreater = true,
            AllowLesser = true,
            Step = 1
        };
        if (_options != null)
        {
            _spin.MinValue = _options.MinValue;
            _spin.MaxValue = _options.MaxValue;
        }
        AddChild(_spin);
        _spin.ValueChanged += v =>
        {
            _value = (int)v;
            NotifyChanged(_value);
        };
    }
}