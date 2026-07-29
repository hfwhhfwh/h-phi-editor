using Godot;
using System;

public partial class NoteChooser : VBoxContainer
{
	[Export] private Button tapButton, dragButton, flickButton, holdButton;

	
	private Button selectedButton = null;


	public event Action<NoteType> OnNoteChoosed;
	public event Action OnDeselected;

    public override void _Ready()
    {
        base._Ready();

		//设置按钮Toggle模式
		tapButton.ToggleMode = true;
		dragButton.ToggleMode = true;
		flickButton.ToggleMode = true;
		holdButton.ToggleMode = true;

		// 为每个按钮连接 toggled 信号
        tapButton.Toggled += pressed => OnButtonToggled(pressed, tapButton, NoteType.Tap);
        dragButton.Toggled += pressed => OnButtonToggled(pressed, dragButton, NoteType.Drag);
        flickButton.Toggled += pressed => OnButtonToggled(pressed, flickButton, NoteType.Flick);
        holdButton.Toggled += pressed => OnButtonToggled(pressed, holdButton, NoteType.Hold);
    }

	private void OnButtonToggled(bool pressed, Button button, NoteType type)
    {
        if (pressed)
        {
			// 清空所有按钮选中状态
			ClearButtonPressed();

			// 高亮选择的按钮
			button.SetPressedNoSignal(true);

            // 触发选择事件
            OnNoteChoosed?.Invoke(type);
			selectedButton = button;
        }
        else
        {
			// 清空所有按钮选中状态
			ClearButtonPressed();

			// 按钮被取消选中 -> 触发取消事件
			OnDeselected?.Invoke();
			
			// selectedButton = null;
			
			// 否则意味着用户选择了另一个按钮，此按钮自动取消选择，不应该触发取消事件
			// GD.Print($"按钮被松开，不触发事件");
        }
    }
	
	/// <summary>
	/// 清空所有按钮选中状态
	/// </summary>
	private void ClearButtonPressed()
	{
		tapButton.SetPressedNoSignal(false);
		dragButton.SetPressedNoSignal(false);
		flickButton.SetPressedNoSignal(false);
		holdButton.SetPressedNoSignal(false);
	}

}
