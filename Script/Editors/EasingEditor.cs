using Godot;
using QuickType;
using System;
using HPhiEditorGame.Editor;

public partial class EasingEditor : PropertyEditorBase<EasingData>
{
    private OptionButton _funcBtn, _ioBtn;
    private LineEdit _leftEdit, _rightEdit;
    private EasingData _value;

    public override EasingData Value
    {
        get => _value;
        set { _value = value; RefreshUI(); }
    }

    protected override void BuildUI()
    {
        var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(vbox);

        // 第一行：Func + IO
        var row1 = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddChild(row1);

        _funcBtn = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        for (int i = 0; i < 11; i++) _funcBtn.AddItem($"{(EasingFunc)i}");
        row1.AddChild(_funcBtn);

        _ioBtn = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        for (int i = 0; i < 3; i++) _ioBtn.AddItem($"{(EasingIO)i}");
        row1.AddChild(_ioBtn);

        // 第二行：Left + Right
        var row2 = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddChild(row2);

        _leftEdit = new LineEdit { PlaceholderText = "Left", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row2.AddChild(_leftEdit);

        _rightEdit = new LineEdit { PlaceholderText = "Right", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row2.AddChild(_rightEdit);

        // 事件
        _funcBtn.ItemSelected += idx => { _value.EasingFunc = (EasingFunc)idx; NotifyChanged(_value); };
        _ioBtn.ItemSelected   += idx => { _value.EasingIO   = (EasingIO)idx;   NotifyChanged(_value); };

        _leftEdit.TextSubmitted += text =>
        {
            if (float.TryParse(text, out float v))
            {
                _value.EasingLeft = Math.Clamp(v, 0f, 1f);
                _leftEdit.Text = _value.EasingLeft.ToString();
                NotifyChanged(_value);
            }
            else _leftEdit.Text = _value.EasingLeft.ToString();
        };

        _rightEdit.TextSubmitted += text =>
        {
            if (float.TryParse(text, out float v))
            {
                _value.EasingRight = Math.Clamp(v, 0f, 1f);
                _rightEdit.Text = _value.EasingRight.ToString();
                NotifyChanged(_value);
            }
            else _rightEdit.Text = _value.EasingRight.ToString();
        };
    }

    private void RefreshUI()
    {
        if (_funcBtn == null) return;
        _funcBtn.Select((int)_value.EasingFunc);
        _ioBtn.Select((int)_value.EasingIO);
        _leftEdit.Text = _value.EasingLeft.ToString();
        _rightEdit.Text = _value.EasingRight.ToString();
    }
}