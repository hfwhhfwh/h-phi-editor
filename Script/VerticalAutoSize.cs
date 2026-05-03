using Godot;
using System;

public partial class VerticalAutoSize : Button
{
	
	private Control child;

	public override void _Ready()
	{
		//默认获取第一个子节点
		child = GetChild<Control>(0);
		child.Resized += OnChildResized;
	}

	private void OnChildResized()
	{
		Vector2 newSize = CustomMinimumSize;
		newSize.Y = child.Size.Y;
		CustomMinimumSize = newSize;

		//GD.Print($"{Name} newSize.Y:{newSize.Y}, child.Size.Y:{child.Size.Y}");
	}

	
}
