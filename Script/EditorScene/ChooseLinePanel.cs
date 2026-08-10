using Godot;
using System;
using System.Collections.Generic;

public partial class ChooseLinePanel : Panel
{
	public class LineInfo
	{
		public int Id { get; set; }
		public int NoteCount { get; set; }
		public float NextEventTime { get; set; } 
	}

	private Theme theme;
	[Export] private VBoxContainer vBoxContainer;
	[Export] private ScrollContainer scrollContainer;
	private bool isDragging = false;
	[Export] private Button addButton;
	// [Export] private Button selectButton; // TODO 选择并批量操作判定线

	[Signal] public delegate void RefreshRequestedEventHandler();

	[Signal] public delegate void LineSelectedEventHandler(int id);
	[Signal] public delegate void DeleteLineRequestedEventHandler(int id);
	[Signal] public delegate void AddLineRequestedEventHandler();


    public override void _Ready()
    {
        base._Ready();
		theme = GD.Load<Theme>("res://theme_gray.tres");

		scrollContainer.ScrollStarted += () => isDragging = true;
		scrollContainer.ScrollEnded += () => isDragging = false;

		addButton.ButtonUp += () => EmitSignal(SignalName.AddLineRequested);
		// selectButton.ButtonUp += () => {};

		// 监听谱面数据变化
		ChartEventBus.OnChartDataChanged += RequestRefresh;
    }

    public override void _ExitTree()
    {
        base._ExitTree();

		ChartEventBus.OnChartDataChanged -= RequestRefresh;
    }



	public void ShowInfos(List<LineInfo> infos)
	{
		foreach(Node child in vBoxContainer.GetChildren())
		{
			//if(child.GetParent() == this && child != null)
			vBoxContainer.RemoveChild(child);
			child.QueueFree();
		}
		
		foreach(LineInfo info in infos)
		{
			CreateButton(info);
		}
	}

	public void CreateButton(LineInfo info)
	{
		// 行容器（水平）
		HBoxContainer row = new HBoxContainer();
		row.SizeFlagsHorizontal = SizeFlags.Fill; // 占满 VBoxContainer 宽度
		vBoxContainer.AddChild(row);


		// 创建主按钮
		Button button = new Button();
		button.Theme = theme;
		button.Text = $"id:{info.Id} 音符数量:{info.NoteCount} 下一个事件:{info.NextEventTime}";
		button.MouseFilter = MouseFilterEnum.Pass;
		button.SetMeta("id", info.Id);
		button.ButtonUp += () => 
		{
			OnMainButtonPressed(button);
		};
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(button);
		

		//右侧...按钮
		{
			Button otherButton = new Button();
			otherButton.Theme = GD.Load<Theme>("res://theme_gray.tres");
			otherButton.Text = "···";
			otherButton.MouseFilter = MouseFilterEnum.Pass;
			otherButton.SetMeta("id", info.Id);
			otherButton.ButtonUp += () =>
			{
				OnOtherButtonPressed(otherButton);
			};

			row.AddChild(otherButton);
		}
		
	}

	/// <summary>
	/// 发出事件，向上级请求刷新数据
	/// </summary>
	private void RequestRefresh()
	{
		EmitSignal(SignalName.RefreshRequested);
	}

	// private void OnButtonClicked(int id)
	// {
	// 	// GD.Print($"OnButtonClicked:{id}");
	// 	EmitSignal(SignalName.LineSelected, id);
	// }

	public void OnMainButtonPressed(Button button)
	{
		// OnButtonClicked((int)button.GetMeta("id"));
		// GD.Print($"OnButtonClicked:{id}");
		if(isDragging) return;
		
		EmitSignal(SignalName.LineSelected, (int)button.GetMeta("id"));
	}

	public void OnOtherButtonPressed(Button button)
	{
		if(isDragging) return;
		
		// 显示弹窗菜单
		List<PopupMenuItem> items = [
			new PopupMenuItem { 
				Text = "删除",
				Callback = () => EmitSignal(SignalName.DeleteLineRequested, (int)button.GetMeta("id"))
			}
		];

		//获取坐标
		Vector2 screenPos = button.GetScreenPosition();

		PopupMenuHelper.ShowPopupMenu(this, screenPos + new Vector2(30, 30), items);

		// GD.Print($"localPos:{localPos}, globalPos:{globalPos}, screenPos:{screenPos}");
	}
}
