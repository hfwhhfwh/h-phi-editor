using Godot;
using HPhiEditorGame.Editor;

public partial class BoolEditor : PropertyEditorBase<bool>
{
    private CheckButton _check;
    private bool _value;

    public override bool Value
    {
        get => _value;
        set
        {
            _value = value;
            if (_check != null) _check.ButtonPressed = value;
        }
    }

    protected override void BuildUI()
    {
        _check = new CheckButton { Text = "启用" };
        AddChild(_check);

        _check.Toggled += v =>
        {
            _value = v;
            NotifyChanged(_value);
        };
    }
}