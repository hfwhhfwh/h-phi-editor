using Godot;

[GlobalClass]
public partial class ScrollSafeButton : Button
{
    private Vector2 _pressPos;
    private bool _isTracking = false;

    private bool isDrag = false;

    [Export] public float DragThreshold { get; set; } = 20f; // 滑动判定阈值（像素）

    public override void _GuiInput(InputEvent @event)
    {
        bool isPress = false;
        bool isRelease = false;
        Vector2 pos = Vector2.Zero;

        // 统一处理鼠标和触摸
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            isPress = mb.Pressed;
            isRelease = !mb.Pressed;
            pos = mb.Position;
        }
        else if (@event is InputEventScreenTouch touch)
        {
            isPress = touch.Pressed;
            isRelease = !touch.Pressed;
            pos = touch.Position;
        }

        // 按下：记录位置，让基类处理以显示 pressed 视觉状态
        if (isPress)
        {
            _pressPos = pos;
            _isTracking = true;
            isDrag = false; // 重置
            base._GuiInput(@event);

            GD.Print("Pressed");
            return;
        }

        // 释放：判断是点击还是滑动
        if (isRelease && _isTracking)
        {
            _isTracking = false;

            // // 如果移动距离超过阈值 → 视为滑动
            // if (_pressPos.DistanceTo(pos) > DragThreshold)
            // {
            //     // 伪造一个"在按钮外部释放"的事件给基类
            //     // 这样 BaseButton 会取消 pressed 状态，但不会触发 Pressed 信号
            //     var fakeEvent = new InputEventMouseButton
            //     {
            //         ButtonIndex = MouseButton.Left,
            //         Pressed = false,
            //         Position = new Vector2(-9999, -9999) // 确保在按钮外部
            //     };
            //     // base._GuiInput(fakeEvent);
            //     GD.Print("超过阈值 → 视为滑动");
            //     AcceptEvent();
            //     return;
            // }

            if (isDrag)
            {
                AcceptEvent();
                GD.Print("超过阈值 → 视为滑动");
                return;
            }

            GD.Print("视为点击按钮");
            base._GuiInput(@event);
            return;
        }

        // 滑动过程中：如果已经判定为拖拽，不再更新按钮内部状态
        if (@event is InputEventMouseMotion mm 
            && mm.ButtonMask.HasFlag(MouseButtonMask.Left) 
            && _isTracking)
        {
            if (_pressPos.DistanceTo(mm.Position) > DragThreshold)
            {
                // 不调用 base，避免 BaseButton 认为鼠标仍在按钮内
                GD.Print("已经判定为拖拽，不再更新按钮内部状态");
                isDrag = true;
                return;
            }
        }

        if (@event is InputEventScreenDrag sd && _isTracking)
        {
            if (_pressPos.DistanceTo(sd.Position) > DragThreshold)
            {
                // 不调用 base，避免 BaseButton 认为鼠标仍在按钮内
                GD.Print("已经判定为拖拽，不再更新按钮内部状态");
                isDrag = true;
                return;
            }
        }

        base._GuiInput(@event);
    }
}