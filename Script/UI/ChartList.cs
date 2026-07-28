using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class ChartList : VBoxContainer
{
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
	/// <param name="charts"></param>
    public void SetCharts(List<ChartInfo> charts)
    {
        // 清空现有项
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        // 创建新的按钮项
        foreach (var chart in charts)
        {
            Button itemButton = itemScene.Instantiate() as Button;
            itemButton.SetMeta("chart_id", chart.Id);
            itemButton.ToggleMode = true;
            itemButton.ButtonGroup = buttonGroup;
            
            // 设置按钮的显示（名称、作曲家、曲绘等）
            UpdateItemDisplay(itemButton, chart);
            AddChild(itemButton);
            itemButton.Toggled += (pressed) => OnButtonToggled(pressed, itemButton);
            
        }
    }

    private void UpdateItemDisplay(Button item, ChartInfo chartInfo)
    {
        // 从item节点中找到Label和TextureRect并赋值
        var nameLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/NameLabel");
        nameLabel.Text = chartInfo.Name;

    	Label composerLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/ComposerLabel");
		composerLabel.Text = chartInfo.Composer;

        //设置曲绘图片
		TextureRect picTexture = item.GetNode<TextureRect>("MarginContainer/HBoxContainer/Icon");
        string picPath = chartInfo.PicturePath.Replace('\\','/');
        GD.Print($"picPath:{picPath}");

        if (!Godot.FileAccess.FileExists(picPath))
        {
            GD.PrintErr($"{this.Name} UpdateItemDisplay() 文件不存在:{picPath}");
            return;
        }

        Image image = Image.LoadFromFile(picPath);
        if(image == null)
        {
            GD.PrintErr($"{this.Name} UpdateItemDisplay() textureImage == null picturePath:{picPath}");
            return;
        }

        Texture2D texture = ImageTexture.CreateFromImage(image);
        if(texture == null)
        {
            GD.PrintErr($"{this.Name} UpdateItemDisplay() texture == null picturePath:{picPath}");
            return;
        }

		picTexture.Texture = texture;
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
