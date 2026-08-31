using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayTestParent : Control
{

	private bool _mousePressed;
    private Vector2 _pressedMousePos;
    private Dictionary<int, Vector2> _pressedTouch = new();

    private Vector2 _prevMousePos;
    private Dictionary<int, Vector2> _prevTouchPos = new();

	public event Action<Vector2> Clicked;
	public event Action<Vector2> Touched;
	public event Action<Vector2> Flicked;

    public float FlickSpeedThreshold { get; set; } = 400f;


    public override void _Process(double delta)
    {
        base._Process(delta);

        // 鼠标
        if (_mousePressed)
        {
            Touched?.Invoke(_pressedMousePos);

            Vector2 velocity = (_pressedMousePos - _prevMousePos) / (float)delta;

            GD.Print($"滑动 Velocity:{velocity}");
            if (velocity.Length() >= FlickSpeedThreshold)
            {
                Flicked?.Invoke(_pressedMousePos);
            }
            _prevMousePos = _pressedMousePos;
        }

        // 触摸（每个点）
        foreach (var kvp in _pressedTouch)
        {
            int idx = kvp.Key;
            Vector2 currentPos = kvp.Value;
            Touched?.Invoke(currentPos);

            if (_prevTouchPos.TryGetValue(idx, out Vector2 prevPos))
            {
                Vector2 velocity = (currentPos - prevPos) / (float)delta;
                GD.Print($"滑动 Velocity:{velocity}");
                if (velocity.Length() >= FlickSpeedThreshold)
                {
                    Flicked?.Invoke(currentPos);
                }
                _prevTouchPos[idx] = currentPos;
            }
            else
            {
                // 安全兜底：如果 prev 丢失，用当前值初始化
                _prevTouchPos[idx] = currentPos;
            }
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
                _prevMousePos = mouseBtn.Position;  // 记录初始位置
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

                // GD.Print($"滑动速度:{mouseMotion.Velocity}");

                // if(mouseMotion.Velocity.Length() >= FlickSpeedThreshold)
                // {
                //     Flicked?.Invoke(mouseMotion.Position);
                // }
            }
        }

        // ---- 2. 屏幕输入 ----
        else if(@event is InputEventScreenTouch screenTouch)
        {
            if(screenTouch.Pressed)
            {
                Clicked?.Invoke(screenTouch.Position);

                _pressedTouch[screenTouch.Index] = screenTouch.Position;
                _prevTouchPos[screenTouch.Index] = screenTouch.Position; // 记录初始位置
            }
            else
            {
                _pressedTouch.Remove(screenTouch.Index);
                _prevTouchPos.Remove(screenTouch.Index);
            }
        }
        else if(@event is InputEventScreenDrag screenDrag)
        {
            if(_pressedTouch.ContainsKey(screenDrag.Index))
            {
                _pressedTouch[screenDrag.Index] = screenDrag.Position;
            }

            // GD.Print($"滑动速度:{screenDrag.Velocity}");

            // if(screenDrag.Velocity.Length() >= FlickSpeedThreshold)
            // {
            //     Flicked?.Invoke(screenDrag.Position);
            // }
        }
    }

}
