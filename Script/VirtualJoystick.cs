using System;
using Godot;

public partial class VirtualJoystick : Control
{
	public enum JoystickDir
	{
		Round, // 任意方向
		Hor, // 仅水平方向
		Ver // 仅竖直方向
	}
    [Export] public float MaxRadius = 100.0f; // 摇杆手柄可拖动的最大半径
	[Export] public JoystickDir joystickDir;

    private Control baseNode;
    private Control tipNode;
    private bool isDragging = false;
    private int dragFingerIndex = -1;
    private Vector2 CenterGPosition // base中心世界坐标
	{
		get
		{
			return baseNode.GlobalPosition + baseNode.Size / 2;
		}
	} 

    // 输出值，x和y的范围均在 [-1, 1] 之间
    public Vector2 Output { get; private set; } = Vector2.Zero;

    public override void _Ready()
    {
        // 检查是否在触摸屏设备上运行，如果是，则显示；否则隐藏（便于调试）
        //Visible = DisplayServer.IsTouchscreenAvailable();

        baseNode = GetNode<Control>("Base");
        tipNode = GetNode<Control>("Tip");
        
		GD.Print($"[{this.Name}] centerGPosition:{CenterGPosition}");
    }

    // 处理所有输入事件
    public override void _Input(InputEvent @event)
    {
        // 只在摇杆可见时处理输入（例如当设备支持触摸时）
        if (!Visible) return;

        // --- 处理触摸事件 ---
        if (@event is InputEventScreenTouch touchEvent)
        {
			if ((GetGlobalRect().HasPoint(touchEvent.Position) || isDragging) && touchEvent.Index == dragFingerIndex)
			{
				if (touchEvent.Pressed)
				{
					isDragging = true;
					dragFingerIndex = touchEvent.Index;
					UpdateJoystick(touchEvent.Position);
				}
				else if (touchEvent.Index == dragFingerIndex)
				{
					// 当前拖动的手指抬起，重置摇杆
					ResetJoystick();
				}
			}
            
        }
        else if (@event is InputEventScreenDrag dragEvent && isDragging && dragEvent.Index == dragFingerIndex)
        {
            UpdateJoystick(dragEvent.Position);
        }

        // --- 处理鼠标事件 (用于PC端调试) ---
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
			// 检查触摸点是否在摇杆区域内
            if (GetGlobalRect().HasPoint(mouseEvent.Position) || isDragging)
			{
				if (mouseEvent.Pressed)
				{
					isDragging = true;
					UpdateJoystick(mouseEvent.Position);
				}
				else
				{
					ResetJoystick();
				}
			}
            
        }
        else if (@event is InputEventMouseMotion mouseMotion && isDragging && (mouseMotion.ButtonMask & MouseButtonMask.Left) != 0)
        {
            UpdateJoystick(mouseMotion.Position);
        }

        // 在编辑器中也需要重置，防止鼠标移出窗口导致状态卡住
        if (Engine.IsEditorHint() && isDragging && Input.IsMouseButtonPressed(MouseButton.Left) == false)
        {
            ResetJoystick();
        }
    }

    private void UpdateJoystick(Vector2 touchPos)
    {
        // 1. 计算手柄的新位置，并将手柄限制在指定区域内
        Vector2 direction = touchPos - CenterGPosition;
		Vector2 clampedDirection = direction.LimitLength(MaxRadius);
		switch (joystickDir)
		{
			case JoystickDir.Round:
				break;
			case JoystickDir.Hor:
				clampedDirection.Y = 0;
				break;
			case JoystickDir.Ver:
				clampedDirection.X = 0;
				break;
		}
        
        // 2. 更新手柄在UI中的位置
        tipNode.GlobalPosition = CenterGPosition + clampedDirection - tipNode.Size / 2;

        // 3. 计算输出值（范围映射到 [-1, 1]）
        // MaxRadius 的微小值用于防止除零错误
        Output = clampedDirection / (MaxRadius + 0.0001f);
    }

    private void ResetJoystick()
    {
        isDragging = false;
        dragFingerIndex = -1;
        // 将手柄重置回中心位置
        tipNode.GlobalPosition = CenterGPosition - tipNode.Size / 2;
        Output = Vector2.Zero;
    }
}