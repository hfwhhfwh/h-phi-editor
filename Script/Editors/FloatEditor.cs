using Godot;
using HPhiEditorGame.Editor;

public partial class FloatEditor : PropertyEditorBase<float>
{
    private SpinBox _spinBox;
    private float _value;

    public override float Value
    {
        get => _value;
        set
        {
            _value = value;
            if (_spinBox != null) _spinBox.Value = value;
        }
    }

    protected override void BuildUI()
    {
        _spinBox = new SpinBox
        {
            Step = 0.01,
            AllowGreater = true,
            AllowLesser = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_spinBox);

        _spinBox.ValueChanged += val =>
        {
            _value = (float)val;
            NotifyChanged(_value);
        };
    }
}