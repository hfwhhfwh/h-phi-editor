using System;
using Godot;
using HPhiEditorGame.Editor;

public partial class IntEditor : PropertyEditorBase<int>
{
    private int _value;
    private SpinBox _spinBox;
    private HSlider _slider;
    private readonly NumericFieldOptions _options;

    public IntEditor(NumericFieldOptions options = null) { _options = options; }

    public override int Value
    {
        get => _value;
        set
        {
            _value = value;
            if (_spinBox != null) _spinBox.SetValueNoSignal(value);
            if (_slider != null) _slider.SetValueNoSignal(Math.Clamp(value, _options.MinValue, _options.MaxValue));
        }
    }

    protected override void BuildUI()
    {
        VBoxContainer vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        vbox.AddThemeConstantOverride("Separation", 10);
        AddChild(vbox);

        _spinBox = new SpinBox
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AllowGreater = true,
            AllowLesser = true,
            Step = 1
        };
        if (_options != null)
        {
            _spinBox.MinValue = _options.MinValue;
            _spinBox.MaxValue = _options.MaxValue;
        }
        vbox.AddChild(_spinBox);

        _slider = new HSlider
        {
            MinValue = _options.MinValue,
            MaxValue = _options.MaxValue,
            Step = _options.Step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        vbox.AddChild(_slider);


        _slider.ValueChanged += value => UpdateFromUser(value, false);
        _spinBox.ValueChanged += value => UpdateFromUser(value, true);
    }

    private void UpdateFromUser(double value, bool fromSpinBox)
    {
        _value = (int)value;

        if (fromSpinBox)
            _slider.SetValueNoSignal(Math.Clamp(value, _options.MinValue, _options.MaxValue));
        else
            _spinBox.SetValueNoSignal(value);

        NotifyChanged(_value);
    }
}