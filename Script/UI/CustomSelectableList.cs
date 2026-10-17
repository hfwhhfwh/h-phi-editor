using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CustomSelectableList : VBoxContainer
{
	private readonly List<(CheckBox Btn, Control Control)> _children = new();
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void AddChild(Control control)
	{
		_children.Add((new CheckBox(), control));
	}
}
