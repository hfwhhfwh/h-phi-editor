using Godot;
using HPhiEditorGame.Editor;

public partial class LongEditor : PropertyEditorBase<long>
{
    private long _value;
    private SpinBox _spin;

    private readonly NumericFieldOptions _options;

    public override long Value
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

    public LongEditor(NumericFieldOptions options)
    {
        _spin.MinValue = options.MinValue;
        _spin.MaxValue = options.MaxValue;
        _spin.Step = options.Step;
    }
}