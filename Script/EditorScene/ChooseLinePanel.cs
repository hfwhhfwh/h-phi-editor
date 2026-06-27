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
	private VBoxContainer vBoxContainer;

	[Signal] public delegate void LineSelectedEventHandler(int id);


    public override void _Ready()
    {
        base._Ready();
		theme = GD.Load<Theme>("res://theme_gray.tres");
		vBoxContainer = GetNode<VBoxContainer>("MarginContainer/ScrollContainer/VBoxContainer");
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
		Button button = new Button();
		button.Theme = GD.Load<Theme>("res://theme_gray.tres");
		button.Text = $"id:{info.Id} 音符数量:{info.NoteCount} 下一个事件:{info.NextEventTime}";
		button.SetMeta("id", info.Id);
		button.ButtonUp += () =>
		{
			OnButtonClicked((int)button.GetMeta("id"));
		};
		vBoxContainer.AddChild(button);
	}

	public void OnButtonClicked(int id)
	{
		// GD.Print($"OnButtonClicked:{id}");
		EmitSignal(SignalName.LineSelected, id);
	}

	
}
