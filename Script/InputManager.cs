using Godot;
using System.Collections.Generic;

public partial class InputManager : Node
{
    // --- 电脑端 ---
    private bool isMiddleButtonPressed = false;
    private bool isCtrlPressed = false;
    private bool isShiftPressed = false;
    private bool isAltPressed = false;

    private Vector2 lastMousePosition;

    // --- 移动端 ---
    private int? slideTouchIndex = null;          // 用于单指滑动的触摸点索引
    private Vector2 lastTouchPosition;            // 上一帧单指位置
    private Dictionary<int, Vector2> activeTouches = new(); // 所有活动触摸点
    private bool isPinching = false;
    private float lastPinchDistance = 0f;

    // 信号：滑动增量 (delta Y) 和 缩放增量
    [Signal] public delegate void SlideEventHandler(float deltaY);
    [Signal] public delegate void ZoomEventHandler(float zoomDelta);

    public override void _Ready()
    {
        SetProcessInput(true); // 确保 _Input 会被调用
    }

    public override void _Input(InputEvent @event)
    {
        HandleMouseInput(@event);
        HandleKeyInput(@event);
        HandleTouchInput(@event);
        
    }

    private void HandleMouseInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseBtn)
        {
            // 鼠标中键滑动
            if (mouseBtn.ButtonIndex == MouseButton.Middle)
            {
                if (mouseBtn.Pressed)
                {
                    isMiddleButtonPressed = true;
                    lastMousePosition = mouseBtn.Position;
                }
                else
                {
                    isMiddleButtonPressed = false;
                }
            }

            // 鼠标滚轮 / 触摸板缩放
            if (mouseBtn.ButtonIndex == MouseButton.WheelUp || mouseBtn.ButtonIndex == MouseButton.WheelDown)
            {
                // 只处理滚轮脉冲 (Pressed 永为 false，但显式检查更安全)
                if (!mouseBtn.Pressed)
                {
                    //1. ctrl+滚轮：缩放
                    if (isCtrlPressed)
                    {
                        float zoomDelta = mouseBtn.Factor * 0.2f;
                        if(mouseBtn.ButtonIndex == MouseButton.WheelDown)
                        {
                            zoomDelta = -zoomDelta;
                        }
                        EmitSignal(SignalName.Zoom, zoomDelta);
                    }
                    //2. shift+滚轮：暂无

                    //3. alt+滚轮：暂无

                    //4. 只有滚轮
                    else
                    {
                        // 将 Factor 乘上自定义灵敏度，得到本次事件的移动量
                        float deltaY = mouseBtn.Factor * 0.2f;
                        if(mouseBtn.ButtonIndex == MouseButton.WheelDown)
                        {
                            deltaY = -deltaY;
                        }
                        EmitSignal(SignalName.Slide, deltaY);
                        //GD.Print($"[{Name}] Scroll deltaY: {deltaY}");
                    }
                    
                }
            }
        }
        // // 鼠标中键拖动 → 产生滑动
        // else if (@event is InputEventMouseMotion mouseMotion && isMiddleButtonPressed)
        // {
        //     Vector2 delta = mouseMotion.Position - lastMousePosition;
        //     lastMousePosition = mouseMotion.Position;
        //     if (delta.Y != 0)
        //         EmitSignal(SignalName.Slide, delta.Y);
        //         //GD.Print($"[{this.Name}] MiddleButtonDrag {delta}");
        // }
    }

    private void HandleKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey key)
        {
            if(key.Keycode == Key.Ctrl)
            {
                if (key.Pressed) isCtrlPressed = true;
                else isCtrlPressed = false;
            }
            if(key.Keycode == Key.Shift)
            {
                if (key.Pressed) isShiftPressed = true;
                else isShiftPressed = false;
            }
            if(key.Keycode == Key.Alt)
            {
                if (key.Pressed) isAltPressed = true;
                else isAltPressed = false;
            }
        }
    }

    private void HandleTouchInput(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch)
        {
            int idx = touch.Index;
            if (touch.Pressed)
            {
                activeTouches[idx] = touch.Position;
                UpdateGestureState();
            }
            else
            {
                activeTouches.Remove(idx);
                UpdateGestureState();
            }
        }
        else if (@event is InputEventScreenDrag drag)
        {
            int idx = drag.Index;
            if (!activeTouches.ContainsKey(idx)) return;

            Vector2 oldPos = activeTouches[idx];
            activeTouches[idx] = drag.Position;

            if (activeTouches.Count == 1 && slideTouchIndex == idx)
            {
                // 单指滑动
                float deltaY = drag.Position.Y - oldPos.Y;
                //if (deltaY != 0) EmitSignal(SignalName.Slide, deltaY);
            }
            else if (activeTouches.Count == 2 && isPinching)
            {
                // 双指缩放
                float currentDist = GetCurrentPinchDistance();
                float deltaDist = currentDist - lastPinchDistance;
                lastPinchDistance = currentDist;
                // 将距离差映射为缩放系数，0.005 是灵敏度，可按需调整
                float zoomDelta = deltaDist * 0.005f;
                //EmitSignal(SignalName.Zoom, zoomDelta);
            }
        }
    }

    private void UpdateGestureState()
    {
        if (activeTouches.Count == 1)
        {
            // 进入单指滑动模式
            var enumerator = activeTouches.GetEnumerator();
            enumerator.MoveNext();
            slideTouchIndex = enumerator.Current.Key;
            lastTouchPosition = enumerator.Current.Value;
            isPinching = false;
        }
        else if (activeTouches.Count == 2)
        {
            // 进入双指缩放模式
            slideTouchIndex = null;
            lastPinchDistance = GetCurrentPinchDistance();
            isPinching = true;
        }
        else
        {
            slideTouchIndex = null;
            isPinching = false;
        }
    }

    private float GetCurrentPinchDistance()
    {
        if (activeTouches.Count < 2) return 0f;
        var positions = new Vector2[2];
        int i = 0;
        foreach (var pos in activeTouches.Values)
            positions[i++] = pos;
        return positions[0].DistanceTo(positions[1]);
    }
}