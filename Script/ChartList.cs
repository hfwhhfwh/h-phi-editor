using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class ChartList : VBoxContainer
{
	[Signal] public delegate void ChartSelectedEventHandler(string chartId);

    private PackedScene _itemScene;

    public override void _Ready()
    {
        _itemScene = ResourceLoader.Load<PackedScene>("res://Scene/chart_list_item.tscn");
    }

    /// <summary>
	/// 由外部调用，传入数据更新列表
	/// </summary>
	/// <param name="charts"></param>
    public void SetCharts(List<ChartInfo> charts)
    {
        // 清空现有项
        foreach (var child in GetChildren())
            child.QueueFree();

        // 创建新的按钮项
        foreach (var chart in charts)
        {
            var item = _itemScene.Instantiate() as Button;
            item.SetMeta("chart_id", chart.Id);
            // 设置按钮的显示（名称、作曲家、曲绘等）
            UpdateItemDisplay(item, chart);
            AddChild(item);
            item.Pressed += () => OnItemPressed(item);
        }
    }

    private void UpdateItemDisplay(Button item, ChartInfo chartInfo)
    {
        // 从item节点中找到Label和TextureRect并赋值
        var nameLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/NameLabel");
        nameLabel.Text = chartInfo.Name;

    	Label composerLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/ComposerLabel");
		composerLabel.Text = chartInfo.Composer;

		TextureRect picTexture = item.GetNode<TextureRect>("MarginContainer/HBoxContainer/Icon");
		Image textureImage = Image.LoadFromFile(chartInfo.PicturePath);
		picTexture.Texture = ImageTexture.CreateFromImage(textureImage);
    }

    private void OnItemPressed(Button item)
    {
        string chartId = item.GetMeta("chart_id").AsString();
        EmitSignal(SignalName.ChartSelected, chartId);
    }
}
