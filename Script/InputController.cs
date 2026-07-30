using Godot;
using System;

public partial class InputController
{
    // 定义统一事件
    public event Action<Vector2> PointerDown;
    public event Action<Vector2> PointerUp;
    public event Action<Vector2, Vector2> PointerDrag;

    public void ProcessEvent(InputEvent inputEvent)
    {
        // 只处理我们关心的输入：鼠标左键 或 触摸
        if (inputEvent is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (mouseBtn.Pressed)
                PointerDown?.Invoke(mouseBtn.Position);
            else
                PointerUp?.Invoke(mouseBtn.Position);
            
            // inputEvent.AcceptEvent(); // 阻止冒泡
        }
        else if (inputEvent is InputEventMouseMotion mouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            PointerDrag?.Invoke(mouseMotion.Position, mouseMotion.Relative);
            // inputEvent.AcceptEvent();
        }
        else if (inputEvent is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
                PointerDown?.Invoke(touch.Position);
            else
                PointerUp?.Invoke(touch.Position);
            
            // inputEvent.AcceptEvent();
        }
        else if (inputEvent is InputEventScreenDrag drag)
        {
            PointerDrag?.Invoke(drag.Position, drag.Relative);
            // inputEvent.AcceptEvent();
        }
    }
}
