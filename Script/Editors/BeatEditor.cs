using Godot;
using QuickType;
using HPhiEditorGame.Editor;

public partial class BeatEditor : PropertyEditorBase<Beat>
{
    private LineEdit[] _edits = new LineEdit[3];
    private Beat _value;

    public override Beat Value
    {
        get => _value;
        set
        {
            _value = value;
            if (_edits[0] != null)
                for (int i = 0; i < 3; i++)
                    _edits[i].Text = value.Values[i].ToString();
        }
    }

    protected override void BuildUI()
    {
        var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(hbox);

        for (int i = 0; i < 3; i++)
        {
            int idx = i; // 【关键】闭包捕获，否则 i 会变成 3
            var edit = new LineEdit
            {
                CustomMinimumSize = new Vector2(50, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            hbox.AddChild(edit);

            edit.TextSubmitted += text =>
            {
                if (int.TryParse(text, out int val))
                {
                    var arr = new int[] { _value[0], _value[1], _value[2] };
                    arr[idx] = val;
                    _value = new Beat(arr);
                    NotifyChanged(_value);
                }
                else
                {
                    edit.Text = _value.Values[idx].ToString();
                }
            };
            _edits[i] = edit;
        }
    }
}