using Godot;
using System;
using System.Collections.Generic;

public partial class PlayTestParent : Control
{

	private bool _mousePressed;
    private Vector2 _pressedMousePos;
    private Dictionary<int, Vector2> _pressedTouch = new();

	public event Action<Vector2> Clicked;
	public event Action<Vector2> Touched;
	public event Action<Vector2> Flicked;

    public float FlickSpeedThreshold { get; set; } = 500f;


    public override void _Process(double delta)
    {
        base._Process(delta);

		// 处理触摸事件
		if (_mousePressed)
		{
			Touched?.Invoke(_pressedMousePos);
		}
		foreach(Vector2 pos in _pressedTouch.Values)
		{
			Touched?.Invoke(pos);
		}
    }


    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);

		// 不接受模拟输入
        if(@event.Device == -1) return;

        // ---- 1. 鼠标模拟输入 ----
        if(@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if(mouseBtn.Pressed)
            {
                Clicked?.Invoke(mouseBtn.Position);
                _mousePressed = true;
                _pressedMousePos = mouseBtn.Position;
            }
            else
            {
                _mousePressed = false;
            }
        }
        else if(@event is InputEventMouseMotion mouseMotion)
        {
            if(_mousePressed == true)
            {
                _pressedMousePos = mouseMotion.Position;

                if(mouseMotion.Velocity.Length() >= FlickSpeedThreshold)
                {
                    Flicked?.Invoke(mouseMotion.Position);
                }
            }
        }

        // ---- 2. 屏幕输入 ----
        else if(@event is InputEventScreenTouch screenTouch)
        {
            if(screenTouch.Pressed)
            {
                Clicked?.Invoke(screenTouch.Position);

                _pressedTouch[screenTouch.Index] = screenTouch.Position;
            }
            else
            {
                _pressedTouch.Remove(screenTouch.Index);
            }
        }
        else if(@event is InputEventScreenDrag screenDrag)
        {
            if(_pressedTouch.ContainsKey(screenDrag.Index))
            {
                _pressedTouch[screenDrag.Index] = screenDrag.Position;
            }

            if(screenDrag.Velocity.Length() >= FlickSpeedThreshold)
            {
                Flicked?.Invoke(screenDrag.Position);
            }
        }
    }

}
