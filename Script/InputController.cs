using Godot;
using System;

public partial class InputController
{
    // 定义统一事件
    public event Action<Vector2> PointerDown;
    public event Action<Vector2> PointerUp;
    public event Action<Vector2, Vector2> PointerDrag;

    public Vector2 PointerDownPos { get; set; }

    public bool IsDragging { get; set; } = false;
    public float DragThreshold { get; set; } = 20f;

    public void ProcessEvent(InputEvent inputEvent)
    {
        // ==================== 鼠标输入 ====================
        if (inputEvent is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (mouseBtn.Pressed)
            {
                OnPointerDown(mouseBtn.Position);
            }
            else
            {
                OnPointerUp(mouseBtn.Position);
            }
                
            
            // inputEvent.AcceptEvent(); // 阻止冒泡
        }
        else if (inputEvent is InputEventMouseMotion mouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            OnPointerMove(mouseMotion.Position, mouseMotion.Relative);
            // inputEvent.AcceptEvent();
        }

        // ==================== 触摸输入 ====================
        else if (inputEvent is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                OnPointerDown(touch.Position);
            }
                
            else
            {
                OnPointerUp(touch.Position);
            }

            // inputEvent.AcceptEvent();

        }
        else if (inputEvent is InputEventScreenDrag drag)
        {
            OnPointerMove(drag.Position, drag.Relative);
            // inputEvent.AcceptEvent();
        }
    }

    // ==================== 整合事件 ====================
    private void OnPointerDown(Vector2 pos)
    {
        PointerDownPos = pos;
        IsDragging = false;
        PointerDown?.Invoke(pos);
    }

    private void OnPointerUp(Vector2 pos)
    {
        PointerUp?.Invoke(pos);

        // 事件触发之后再取消滑动，让订阅者知道这次抬起时滑动结束导致的
        IsDragging = false;
    }

    private void OnPointerMove(Vector2 pos, Vector2 relative)
    {
        if (!IsDragging)
        {
            if(pos.DistanceSquaredTo(PointerDownPos) > DragThreshold * DragThreshold)
            {
                // 视为滑动
                IsDragging = true;
                GD.Print($"开始滑动");
            }
        }
        PointerDrag?.Invoke(pos, relative);
    }
}
