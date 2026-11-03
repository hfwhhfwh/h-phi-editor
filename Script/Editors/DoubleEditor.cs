using Godot;
using HPhiEditorGame.Editor;

public partial class DoubleEditor : PropertyEditorBase<double>
{
    private double _value;
    private SpinBox _spin;
    private readonly NumericFieldOptions _options;

    public DoubleEditor(NumericFieldOptions options)
    {
        _options = options;
    }

    public override double Value
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
            MinValue = _options.MinValue,
            MaxValue = _options.MaxValue,
            Step = _options.Step
        };
        AddChild(_spin);

        _spin.ValueChanged += v =>
        {
            _value = (long)v;
            NotifyChanged(_value);
        };
    }
}