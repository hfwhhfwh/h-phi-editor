using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public partial class ChartList : VBoxContainer
{
    public struct Data
    {
        public string ChartId { get; set; }
        public string ChartName { get; set; }
        public string Composer { get; set; }
        public Texture2D Picture { get; set; }

    }

	[Signal] public delegate void ChartSelectedEventHandler(string chartId);

    private PackedScene itemScene;

    private ButtonGroup buttonGroup; // 用于存放谱面选项按钮

    public override void _Ready()
    {
        itemScene = ResourceLoader.Load<PackedScene>("res://Scene/chart_list_item.tscn");

        buttonGroup = new ButtonGroup();

        
    }

    /// <summary>
	/// 由外部调用，传入数据更新列表
	/// </summary>
	/// <param name="datas"></param>
    public void SetCharts(List<Data> datas)
    {
        // 清空现有项
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        // 创建新的按钮项
        foreach (var data in datas)
        {
            Button itemButton = itemScene.Instantiate() as Button;
            itemButton.SetMeta("chart_id", data.ChartId);
            itemButton.ToggleMode = true;
            itemButton.ButtonGroup = buttonGroup;
            
            // 设置按钮的显示（名称、作曲家、曲绘等）
            UpdateItemDisplay(itemButton, data);
            AddChild(itemButton);
            itemButton.Toggled += (pressed) => OnButtonToggled(pressed, itemButton);
            
        }
    }

    private void UpdateItemDisplay(Button item, Data data)
    {
        // 设置谱面名称
        Label nameLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/NameLabel");
        nameLabel.Text = data.ChartName;

        // 设置作曲家名称
    	Label composerLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/ComposerLabel");
		composerLabel.Text = data.Composer;

        // 设置曲绘图片
		TextureRect picTexture = item.GetNode<TextureRect>("MarginContainer/HBoxContainer/Icon");
		picTexture.Texture = data.Picture;
    }


    private void OnButtonToggled(bool pressed, Button button)
    {
        if (pressed)
        {
            string id = button.GetMeta("chart_id").AsString();
            GD.Print($"选中按钮 ID: {id}");
            EmitSignal(SignalName.ChartSelected, id);
        }
    }
}
