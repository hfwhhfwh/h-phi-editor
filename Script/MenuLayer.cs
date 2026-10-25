using Godot;
using System;
using System.Diagnostics.CodeAnalysis;

public partial class MenuLayer : CanvasLayer
{
	[Export] private ColorRect _bgColorRect;
	[Export] private Control _panel;
	[Export] private Button _closeBtn;
	
	public override void _Ready()
	{
		Visible = false;

		_closeBtn.Pressed += () =>
		{
			Close();
		};

		_bgColorRect.MouseFilter = Control.MouseFilterEnum.Stop;
		_bgColorRect.GuiInput += (InputEvent @event) =>
		{
			if (@event is InputEventMouseButton mb
				&& mb.Pressed
				&& mb.ButtonIndex == MouseButton.Left)
			{
				Close();
				_bgColorRect.AcceptEvent(); // 标记事件已处理
			}
			else if(@event is InputEventScreenTouch st && st.Pressed)
			{
				Close();
				_bgColorRect.AcceptEvent(); // 标记事件已处理
			}
		};

		_panel.MouseFilter = Control.MouseFilterEnum.Stop;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Open()
	{
		Visible = true;
		_bgColorRect.SelfModulate = new Color(1, 1, 1, 1);

		Tween tween = CreateTween();

		tween.TweenProperty(_panel, "position:x", 0, 0.3)
			.From(-_panel.Size.X)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		

	}

	public void Close()
	{
		Visible = false;
	}

	private void SetMouseFilterRecursively(Node node, Control.MouseFilterEnum filter)
	{
		if(node is Control control) control.MouseFilter = filter;

		foreach(Node child in node.GetChildren())
		{
			SetMouseFilterRecursively(child, filter);
		}
	}
}
