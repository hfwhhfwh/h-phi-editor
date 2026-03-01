using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class ChartList : VBoxContainer
{
	[Signal]
    public delegate void OptionSelectedEventHandler(int id);

    private ButtonGroup _buttonGroup;

	private PackedScene chartListItemScene;

	//谱面信息
	private Dictionary<string, string> infoDic;

	public void LoadChartList()
	{
		//先删除所有子节点
		foreach(Node child in GetChildren())
		{
			child.QueueFree();
			RemoveChild(child);
		}
		//从user://ChartSaves 读取项目列表
		DirAccess dir = DirAccess.Open("user://ChartSaves");
		if (dir == null)
		{
			GD.PrintErr($"[ChartList] 无法打开目录: user://ChartSaves");
			return;
		}
		//遍历每一个子文件夹（即一个项目）
		foreach(string subDir in dir.GetDirectories())
		{
			string subDirPath = Path.Combine("user://ChartSaves", subDir);
			//读取info.txt
			string infoFilePath = Path.Combine(subDirPath, "info.txt");
			infoDic = ReadInfoFile(infoFilePath);

			string chartName = infoDic["Name"];
			string chartComposer = infoDic["Composer"];

			//创建选项子节点(Button)
			Button item = chartListItemScene.Instantiate() as Button;
			AddChild(item);
			

			//读取并设置曲绘图片
			string chartPicturePath = Path.Combine(subDirPath, infoDic["Picture"]);
			Image picImage = Image.LoadFromFile(chartPicturePath);
			TextureRect pictureNode = item.GetNode<TextureRect>("MarginContainer/HBoxContainer/Icon");
			pictureNode.Texture = ImageTexture.CreateFromImage(picImage);

			//GD.Print($"chartName:{chartName}, chartComposer:{chartComposer}");

			//设置名称和作曲家
			Label nameLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/NameLabel");
			Label composerLabel = item.GetNode<Label>("MarginContainer/HBoxContainer/Info/ComposerLabel");
			nameLabel.Text = chartName;
			composerLabel.Text = chartComposer;

		}
	}


	/// <summary>
	/// 读取info.txt谱面信息文件
	/// </summary>
	/// <param name="filePath">info.txt的路径</param>
	/// <returns>一个属性字典</returns>
	private Dictionary<string, string> ReadInfoFile(string filePath)
	{
		var properties = new Dictionary<string, string>();

		// 检查文件是否存在
		if (!Godot.FileAccess.FileExists(filePath))
		{
			GD.PrintErr($"文件不存在: {filePath}");
			return properties;
		}

		// 以只读文本模式打开文件
		using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
		
		// 逐行读取
		while (file.GetPosition() < file.GetLength())
		{
			string line = file.GetLine().Trim(); // 去除首尾空白

			// 跳过空行和注释行（以#开头）
			if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
				continue;

			// 查找第一个冒号的位置
			int colonIndex = line.IndexOf(':');
			if (colonIndex == -1)
				continue; // 没有冒号的行跳过

			// 提取键和值，并去除各自两端的空白
			string key = line.Substring(0, colonIndex).Trim();
			string value = line.Substring(colonIndex + 1).Trim();

			// 存入字典（如果键重复，后面的会覆盖前面的，根据需求可调整）
			properties[key] = value;
		}

		return properties;
	}

    public override void _Ready()
    {
        _buttonGroup = new ButtonGroup();
		chartListItemScene = ResourceLoader.Load<PackedScene>("res://Scene/chart_list_item.tscn");

		LoadChartList();
        
        foreach (var child in GetChildren())
        {
            if (child is Button button && button is not CheckBox)
            {
                button.ToggleMode = true;
                SetupButton(button);
            }
        }

        _buttonGroup.Pressed += OnGroupPressed;
    }

    private void SetupButton(BaseButton button)
    {
        button.ButtonGroup = _buttonGroup;
        button.SetMeta("unique_id", button.GetIndex());
        button.Toggled += (pressed) => OnButtonToggled(pressed, button);
    }

    private void OnButtonToggled(bool pressed, BaseButton button)
    {
        if (pressed)
        {
            int id = button.GetMeta("unique_id").AsInt32();
            GD.Print($"选中按钮 ID: {id}");
            EmitSignal(SignalName.OptionSelected, id);
        }
    }

    private void OnGroupPressed(BaseButton baseButton)
    {
		if(baseButton is Button button)
		{
			int id = button.GetMeta("unique_id").AsInt32();
        	GD.Print($"当前选中: {button.Text}, ID: {id}");
		}
    }

    public int GetSelectedId()
    {
        var selectedBtn = _buttonGroup.GetPressedButton();
        return selectedBtn != null ? selectedBtn.GetMeta("unique_id").AsInt32() : -1;
    }
}
