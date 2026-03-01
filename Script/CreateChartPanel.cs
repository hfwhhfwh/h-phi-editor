using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;


public partial class CreateChartPanel : PanelContainer
{

	public float bpm;
	public string musicPath;
	public string picPath;
	public float musicDuration;
	

	private LineEdit musicPathLineEdit;
	private LineEdit picPathLineEdit;

	[Export] private ChartList chartList;

	private Dictionary<string, string> infoDic = new Dictionary<string, string>();

	FileDialogManager fileDialogManager;
	
	public override void _Ready()
	{
		//文件窗口
        fileDialogManager = GetNode<FileDialogManager>("/root/FileDialogManager");
		//音乐路径输入框
		musicPathLineEdit = GetNode<LineEdit>("MarginContainer/VBoxContainer2/VBoxContainer/Music/LineEdit");
		//曲绘路径输入框
		picPathLineEdit = GetNode<LineEdit>("MarginContainer/VBoxContainer2/VBoxContainer/Pic/LineEdit");
		
		
	}

	public override void _Process(double delta)
	{
	}

	public void OnNameChanged(string s)
	{
		//name = s;
		infoDic["Name"] = s;
	}

	public void OnMusicChanged(string s)
	{
		musicPath = s;
	}

	public void OnPicChanged(string s)
	{
		picPath = s;
	}

	public void OnBPMChanged(string s)
	{
		float result;
		if(float.TryParse(s, out result))
		{
			bpm = result;
		}
	}

	public void OnCharterChanged(string s)
	{
		infoDic["Charter"] = s;
	}

	public void OnComposerChanged(string s)
	{
		//composer = s;
		infoDic["Composer"] = s;
	}

	public void OnCanceled()
	{
		Visible = false;
	}

	public void SelectMusicFile()
	{
		string[] filters = 
		{
			"*.mp3,*.wav,*.ogg,*.flac,*.aac,*.m4a,*.wma,*.aiff;音频文件;audio/mpeg,audio/x-wav,audio/ogg,audio/flac,audio/aac,audio/mp4,audio/x-ms-wma,audio/aiff"
		};
		fileDialogManager.ShowNativeOpenDialog(OnMusicSelected, filters);
	}

	public void SelectPicFile()
	{
		string[] filters = {"*.png,*.jpg,*.jpeg,*.bmp,*.webp;图像文件;image/png,image/jpg,image/jpeg,image/bmp,image/webp"};
		//string[] filters2 = {"*.png,*.jpg,*.jpeg;图像文件;image/png,image/jpeg"};
		fileDialogManager.ShowNativeOpenDialog(OnPicSelected, filters);
	}

	public void OnMusicSelected(string path)
	{
		musicPath = path;
		//获取音乐时长
		AudioStream audioStream = AudioStreamMP3.LoadFromFile(musicPath);
		musicDuration = (float)audioStream.GetLength();

		//更新输入框
		musicPathLineEdit.Text = path;

	}

	public void OnPicSelected(string path)
	{
		picPath = path;
		

		//更新输入框
		picPathLineEdit.Text = path;

	}

	public void OnOK()
	{
		string chartSavesDir = "user://ChartSaves";
		string newChartSaveDir;
		//生成一个唯一的id  16位
		string id = GenerateRandomId(15);
		infoDic["Path"] = id;
		//创建文件夹
		{
			DirAccess dir = DirAccess.Open(chartSavesDir);
			Error makeDirError = dir.MakeDir(id);
			if (makeDirError != Error.Ok && makeDirError != Error.AlreadyExists)
			{
				GD.PrintErr($"无法创建目录: {Path.Combine("user://ChartSaves", id)}, 错误码: {makeDirError}");
				return;
			}
			newChartSaveDir = Path.Combine("user://ChartSaves", id);
		}

		//补充info属性
		{
			infoDic["Song"] = $"{id}.{musicPath.GetExtension()}";
			infoDic["Picture"] = $"{id}.{picPath.GetExtension()}";
			infoDic["Chart"] = $"{id}.json";
		}
		
		//添加info.txt
		{
			string txtPath = Path.Combine(newChartSaveDir, "info.txt");
			//写入属性
			WriteInfoFile(txtPath, infoDic);
			//TODO
		}

		//添加音乐文件
		{
			string fileName = $"{id}.{musicPath.GetExtension()}";
			string musicCopyPath = Path.Combine(newChartSaveDir, fileName);
			CopyFile(musicPath, musicCopyPath);
		}

		//添加曲绘文件
		{
			string fileName = $"{id}.{picPath.GetExtension()}";
			string picCopyPath = Path.Combine(newChartSaveDir, fileName);
			CopyFile(picPath, picCopyPath);
		}

		//创建谱面文件
		{
			//先从模版复制
			string fileName = $"{id}.json";
			string chartCopyPath = Path.Combine(newChartSaveDir, fileName);
			CopyFile("res://TemplateChart.json", chartCopyPath);

			//修改信息
			Chart chart = ChartLoader.LoadChart(chartCopyPath);
			chart.BpmList[0].Bpm = bpm;
            chart.Meta = new Meta
            {
                Background = infoDic["Picture"],
                Charter = infoDic["Charter"],
                Composer = infoDic["Composer"],
                Duration = musicDuration,
				Id = id,
				Illustration = "", // TODO
				Level = "0", // TODO
				Name = infoDic["Name"],
				Offset = 0,
				Song = infoDic["Song"]
            };

			//生成json
			ChartLoader.SaveChart(chart,chartCopyPath);
            
		}
		
		//刷新谱面列表
		chartList.LoadChartList();

		//隐藏自己
		Visible = false;
	}

	/// <summary>
	/// 根据属性字典生成谱面信息文件
	/// </summary>
	/// <param name="filePath">info.txt的路径</param>
	/// <param name="properties">属性字典</param>
	public static void WriteInfoFile(string filePath, Dictionary<string, string> properties)
	{
		// 参数校验
		if (properties == null)
		{
			GD.PrintErr("属性字典为 null, 无法写入文件。");
			return;
		}

		// 确保文件所在的目录存在
		string directory = filePath.GetBaseDir();
		if (!string.IsNullOrEmpty(directory) && !DirAccess.DirExistsAbsolute(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		// 打开文件（写入模式），如果文件已存在则覆盖
		using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PrintErr($"无法打开文件进行写入: {filePath}");
			return;
		}

		// 写入可选的注释头（读取时会自动跳过）
		file.StoreLine("# Generated by WriteInfoFile");

		// 写入所有键值对
		foreach (var kvp in properties)
		{
			string line = $"{kvp.Key}: {kvp.Value}";
			file.StoreLine(line);
		}

		// 文件会在 using 块结束时自动关闭
		GD.Print($"成功写入 {properties.Count} 条属性到文件: {filePath}");
	}

	/// <summary>
	/// 随机生成一个数字ID
	/// </summary>
	/// <param name="length">ID的位数</param>
	/// <returns>字符串，ID</returns>
	public static string GenerateRandomId(int length = 16)
	{
		var rng = new RandomNumberGenerator();
		// 设置随机种子
		rng.Seed = (ulong)Time.GetTicksMsec();
		char[] chars = new char[length];
		for (int i = 0; i < length; i++)
		{
			// 生成 0-9 的数字
			chars[i] = (char)('0' + rng.RandiRange(0, 9));
		}
		return new string(chars);
	}

	/// <summary>
	/// 将文件复制到指定路径
	/// </summary>
	/// <param name="sourcePath">源文件的完整路径（支持 res://、user:// 或绝对路径）</param>
	/// <param name="destinationPath">目标文件的完整路径（包含文件名）</param>
	/// <returns>复制成功返回 true，否则返回 false</returns>
	public bool CopyFile(string sourcePath, string destinationPath)
	{
		// 检查源文件是否存在
		if (!Godot.FileAccess.FileExists(sourcePath))
		{
			GD.PrintErr($"源文件不存在: {sourcePath}");
			return false;
		}

		// 确保目标目录存在
		string destDir = destinationPath.GetBaseDir();
		if (!string.IsNullOrEmpty(destDir) && !DirAccess.DirExistsAbsolute(destDir))
		{
			DirAccess.MakeDirRecursiveAbsolute(destDir);
		}

		try
		{
			// 以读取模式打开源文件
			using var sourceFile = Godot.FileAccess.Open(sourcePath, Godot.FileAccess.ModeFlags.Read);
			if (sourceFile == null)
			{
				GD.PrintErr($"无法打开源文件: {sourcePath}");
				return false;
			}

			// 以写入模式打开目标文件（如果目标文件已存在，会被覆盖）
			using var destFile = Godot.FileAccess.Open(destinationPath, Godot.FileAccess.ModeFlags.Write);
			if (destFile == null)
			{
				GD.PrintErr($"无法创建或打开目标文件: {destinationPath}");
				return false;
			}

			// 逐块复制文件内容（避免大文件占用过多内存）
			const int bufferSize = 8192; // 8 KB 缓冲区
			byte[] buffer = new byte[bufferSize];
			long bytesRemaining = (long)sourceFile.GetLength();
			while (bytesRemaining > 0)
			{
				long bytesToRead = (long)Math.Min(bufferSize, bytesRemaining);
				buffer = sourceFile.GetBuffer(bytesToRead);
				//if (bytesRead <= 0) break; // 读取错误或 EOF

				destFile.StoreBuffer(buffer);
				bytesRemaining -= bytesToRead;
			}

			GD.Print($"文件复制成功: {sourcePath} -> {destinationPath}");
			return true;
		}
		catch (Exception e)
		{
			GD.PrintErr($"复制文件时发生异常: {e.Message}");
			return false;
		}
	}


	
}
