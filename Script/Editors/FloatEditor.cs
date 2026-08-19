using Godot;
using HPhiEditorGame.Editor;
using System;

public sealed class FloatEditorOptions
{
    public double MinValue { get; set; } = -1;
    public double MaxValue { get; set; } = 1;
    public double Step { get; set; } = 0.01;
}

public partial class FloatEditor : PropertyEditorBase<float>
{
    private readonly FloatEditorOptions _options;
    private HSlider _slider;
    private SpinBox _spinBox;
    private float _value;

    public FloatEditor(FloatEditorOptions options = null)
    {
        _options = options ?? new FloatEditorOptions();

        if (_options.MinValue >= _options.MaxValue)
            throw new ArgumentException("FloatEditor 的最小值必须小于最大值");
        if (_options.Step <= 0)
            throw new ArgumentException("FloatEditor 的步长必须大于 0");
    }

    public override float Value
    {
        get => _value;
        set
        {
            _value = value;
            if (_spinBox == null) return;

            _spinBox.SetValueNoSignal(value);
            _slider.SetValueNoSignal(Math.Clamp((double)value, _options.MinValue, _options.MaxValue));
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
            Step = _options.Step,
            AllowGreater = true,
            AllowLesser = true,
            CustomMinimumSize = new Vector2(90, 0)
        };
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
        _value = (float)value;

        if (fromSpinBox)
            _slider.SetValueNoSignal(Math.Clamp(value, _options.MinValue, _options.MaxValue));
        else
            _spinBox.SetValueNoSignal(value);

        NotifyChanged(_value);
    }
}