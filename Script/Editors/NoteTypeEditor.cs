using Godot;
using QuickType;
using System;
using HPhiEditorGame.Editor;

public partial class NoteTypeEditor : PropertyEditorBase<NoteType>
{
    private NoteType _value;
    private OptionButton _option;

    private static readonly NoteType[] _values = (NoteType[])Enum.GetValues(typeof(NoteType));
    private static readonly string[] _names = Enum.GetNames(typeof(NoteType));

    public override NoteType Value
    {
        get => _value;
        set
        {
            _value = value;
            if (_option != null)
            {
                int idx = Array.IndexOf(_values, value);
                if (idx >= 0) _option.Select(idx);
            }
        }
    }

    protected override void BuildUI()
    {
        _option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string n in _names) _option.AddItem(n);
        AddChild(_option);

        _option.ItemSelected += idx =>
        {
            _value = _values[(int)idx];
            NotifyChanged(_value);
        };
    }
}