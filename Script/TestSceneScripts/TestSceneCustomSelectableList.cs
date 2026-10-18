using Godot;
using System;

public partial class TestSceneCustomSelectableList : Node
{
    [Export] private CustomSelectableList _list;

    private Button _button;

    public override void _Ready()
    {
        base._Ready();

        _button = new Button
        {
            Text = "我是一个按钮"
        };
        _list.AddItem(_button);
        _list.AddItem(new ColorPickerButton());
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        base._UnhandledKeyInput(@event);

        if(@event is InputEventKey inputEventKey)
        {
            if(inputEventKey.Pressed && inputEventKey.Echo == false)
            {
                if(inputEventKey.Keycode == Key.Enter)
                {
                    _list.MultiSelectMode = !_list.MultiSelectMode;
                }
            }
        }
    }


}
