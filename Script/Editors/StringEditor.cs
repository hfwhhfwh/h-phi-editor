using Godot;
using System;
using HPhiEditorGame.Editor;

public partial class StringEditor : PropertyEditorBase<string>
{
    private string _value;
    public override string Value {
        get => _value;
        set {
            _value = value;
        }
    }

    private LineEdit _edit;

    protected override void BuildUI()
    {
        LineEdit edit = new LineEdit
        {
            CustomMinimumSize = new Vector2(50, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        edit.SetAnchorsPreset(LayoutPreset.FullRect); // 填满
        AddChild(edit);

        edit.TextSubmitted += (string text) =>
        {
            _value = text;
            NotifyChanged(_value);
        };
        _edit = edit;
    }

}
