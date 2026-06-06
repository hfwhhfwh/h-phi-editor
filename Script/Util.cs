using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;


public static class Util
{
    /// <summary>
    /// 将指定路径的ZIP文件解压到与其同名的文件夹中。
    /// </summary>
    /// <param name="zipPath">ZIP文件的完整路径，例如 "user://data.zip"</param>
    public static void UnzipFile(string zipPath)
    {
        var zipReader = new ZipReader();
        Error error = zipReader.Open(zipPath);

        if (error != Error.Ok)
        {
            GD.PrintErr($"无法打开ZIP文件: {zipPath}, 错误码: {error}");
            return;
        }

        // 获取ZIP文件所在的目录和文件名（不含扩展名）
        string zipDirectory = zipPath.GetBaseDir();
        string zipFileName = zipPath.GetFile().GetBaseName(); // 例如 "data"
        string extractBasePath = Path.Combine(zipDirectory, zipFileName);

        // 创建基础的解压目录
        DirAccess dir = DirAccess.Open(zipDirectory);
        if (dir == null)
        {
            GD.PrintErr($"无法访问目录: {zipDirectory}");
            zipReader.Close();
            return;
        }
        
        Error makeDirError = dir.MakeDir(zipFileName);
        if (makeDirError != Error.Ok && makeDirError != Error.AlreadyExists)
        {
            GD.PrintErr($"无法创建目录: {extractBasePath}, 错误码: {makeDirError}");
            zipReader.Close();
            return;
        }

        // 遍历ZIP内的所有文件
        string[] files = zipReader.GetFiles();
        foreach (string filePath in files)
        {
            // 计算文件的完整输出路径
            string fullOutputPath = Path.Combine(extractBasePath, filePath);
            string outputDirectory = fullOutputPath.GetBaseDir();

            // 确保文件的子目录存在
            DirAccess.MakeDirRecursiveAbsolute(outputDirectory);

            // 读取ZIP中的文件数据并写入磁盘
            byte[] fileData = zipReader.ReadFile(filePath);
            if (fileData != null)
            {
                using var file = Godot.FileAccess.Open(fullOutputPath, Godot.FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreBuffer(fileData);
                    GD.Print($"已解压: {filePath}");
                }
                else
                {
                    GD.PrintErr($"无法创建输出文件: {fullOutputPath}");
                }
            }
            else
            {
                GD.PrintErr($"无法从ZIP读取文件: {filePath}");
            }
        }

        zipReader.Close();
        GD.Print("解压完成！");
    }

    /// <summary>
    /// 将指定路径的ZIP文件解压到指定路径的文件夹中。
    /// </summary>
    /// <param name="zipPath">ZIP文件的完整路径，例如 "res://ZipFile.zip"</param>
    /// <param name="extractBasePath">解压基础路径，例如 "res://ChartImport/"</param>
    public static void UnzipFileTo(string zipPath, string extractBasePath)
    {
        var zipReader = new ZipReader();
        Error error = zipReader.Open(zipPath);

        if (error != Error.Ok)
        {
            GD.PrintErr($"无法打开ZIP文件: {zipPath}, 错误码: {error}");
            return;
        }

        // 遍历ZIP内的所有文件
        string[] files = zipReader.GetFiles();
        foreach (string filePath in files)// 例如 Chart.json
        {
            // 计算文件的完整输出路径
            string fullOutputPath = Path.Combine(extractBasePath, filePath); // 例如 res://ChartImport/Chart.json
            string outputDirectory = fullOutputPath.GetBaseDir(); // 例如 res://ChartImport/

            // 确保文件的子目录存在
            DirAccess.MakeDirRecursiveAbsolute(outputDirectory);

            // 读取ZIP中的文件数据并写入磁盘
            byte[] fileData = zipReader.ReadFile(filePath);
            if (fileData != null)
            {
                using var file = Godot.FileAccess.Open(fullOutputPath, Godot.FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreBuffer(fileData);
                    GD.Print($"已解压: {filePath}");
                }
                else
                {
                    GD.PrintErr($"无法创建输出文件: {fullOutputPath}");
                }
            }
            else
            {
                GD.PrintErr($"无法从ZIP读取文件: {filePath}");
            }
        }

        zipReader.Close();
        GD.Print("解压完成！");
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
	/// 读取info.txt谱面信息文件
	/// </summary>
	/// <param name="filePath">info.txt的路径</param>
	/// <returns>一个属性字典</returns>
	public static Dictionary<string, string> ReadInfoFile(string filePath)
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

    /// <summary>
	/// 将文件复制到指定路径
	/// </summary>
	/// <param name="sourcePath">源文件的完整路径（支持 res://、user:// 或绝对路径）</param>
	/// <param name="destinationPath">目标文件的完整路径（包含文件名）</param>
	/// <returns>复制成功返回 true，否则返回 false</returns>
	public static bool CopyFile(string sourcePath, string destinationPath)
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

	/// <summary>
	/// 确保目录存在，若存在则不执行任何操作，若不存在则创建目录
	/// </summary>
	/// <param name="path">目录路径</param>
	public static void EnsureDirectoryExists(string directory)
	{
		if (!string.IsNullOrEmpty(directory) && !DirAccess.DirExistsAbsolute(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}
	}

	/// <summary>
	/// 递归删除目录
	/// </summary>
	/// <param name="path">要删除的目录</param>
	public static void DeleteDirectoryRecursive(string path)
    {
        // 打开目录
        using var dir = DirAccess.Open(path);
        if (dir == null)
        {
            GD.PrintErr($"无法打开目录: {path}");
            return;
        }

        // 遍历目录内容
        dir.ListDirBegin();  // 开始列出目录
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName == "." || fileName == "..")  // 跳过特殊目录
            {
                fileName = dir.GetNext();
                continue;
            }

            string fullPath = path + "/" + fileName;
            if (dir.CurrentIsDir())
            {
                // 递归删除子目录
                DeleteDirectoryRecursive(fullPath);
            }
            else
            {
                // 删除文件
                DirAccess.RemoveAbsolute(fullPath);  // 静态方法删除文件
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();  // 结束列出目录

        // 删除当前空目录
        DirAccess.RemoveAbsolute(path);
    }

	public static float GetMusicDuration(string path)
	{
		// 尝试加载指定路径的音频流资源
		AudioStream audioStream = AudioStreamMP3.LoadFromFile(path);
		// 或使用 GD.Load<AudioStream>(path);

		if (audioStream == null)
		{
			// 资源加载失败时输出错误并返回 0
			GD.PrintErr($"无法加载音频文件: {path}");
			return 0f;
		}

		// 获取音频时长（秒），转换为 float 返回
		return (float)audioStream.GetLength();
	}

	/// <summary>
    /// 将多个文件打包进一个 ZIP 压缩包。
    /// </summary>
    /// <param name="filePaths">需要打包的文件路径列表（支持 user:// 或绝对路径）</param>
    /// <param name="zipPath">生成的 ZIP 文件的路径</param>
    /// <returns>如果成功返回 true，否则返回 false。</returns>
    public static bool CreateZip(List<string> filePaths, string zipPath)
    {
        // 使用 using 确保 ZipPacker 正确释放资源
        using var packer = new ZipPacker();

        // 1. 打开或创建 ZIP 文件
        // APPEND_CREATE 表示创建一个新的压缩包
        var err = packer.Open(zipPath, ZipPacker.ZipAppend.Create);
        if (err != Error.Ok)
        {
            GD.PrintErr($"无法创建 ZIP 文件 '{zipPath}'，错误代码: {err}");
            return false;
        }

        // 2. 逐个将文件添加到压缩包
        foreach (var filePath in filePaths)
        {
            // 检查源文件是否存在
            if (!Godot.FileAccess.FileExists(filePath))
            {
                GD.PrintErr($"错误：文件未找到，已跳过 '{filePath}'");
                continue;
            }

            // 2.1. 读取整个文件内容
            byte[] fileData;
            try
            {
                fileData = Godot.FileAccess.GetFileAsBytes(filePath);
            }
            catch (Exception e)
            {
                GD.PrintErr($"读取文件 '{filePath}' 时出错: {e.Message}");
                continue;
            }

            // 2.2. 生成在压缩包内的存储路径
            // 使用文件名作为存储路径，如需保留目录结构可修改此逻辑
            var targetPath = Path.GetFileName(filePath);

            // 2.3. 开始写入文件
            // 可在此处指定压缩级别，ZipPacker.CompressionLevel
            err = packer.StartFile(targetPath);
            if (err != Error.Ok)
            {
                GD.PrintErr($"无法在压缩包中开始写入文件 '{targetPath}'，错误代码: {err}");
                continue;
            }

            // 2.4. 写入文件数据
            err = packer.WriteFile(fileData);
            if (err != Error.Ok)
            {
                GD.PrintErr($"写入文件数据 '{targetPath}' 时出错，错误代码: {err}");
            }

            // 2.5. 关闭当前文件（必须配对调用）
            err = packer.CloseFile();
            if (err != Error.Ok)
            {
                GD.PrintErr($"关闭文件 '{targetPath}' 时出错，错误代码: {err}");
            }
        }

        // 3. 关闭并最终化 ZIP 文件
        err = packer.Close();
        if (err != Error.Ok)
        {
            GD.PrintErr($"最终化 ZIP 文件 '{zipPath}' 时出错，错误代码: {err}");
        	return false;
        }
		
		GD.Print($"成功创建 ZIP 文件: '{zipPath}'");
        return true;

        
    }


    /// <summary>
    /// 时间转换：将Beat（int[]）转换为秒
    /// </summary>
    /// <param name="beat">当前节拍数</param>
    /// <param name="BpmList">谱面的所有bpm事件</param>
    /// <returns></returns>
    public static float BeatToSecond(int[] beat, BpmEvent[] BpmList)
    {
        if (BpmList == null || BpmList.Length == 0)
            return 0;

        // 将Beat转为以拍为单位的总拍数： beat[0] + beat[1]/beat[2]
        float totalBeats = beat[0] + (float)beat[1] / beat[2];

        // 找到当前Beat所在的BPM段并累积时间
        float elapsedSeconds = 0;
        float lastBpmBeat = 0; // 上一个BPM事件的总拍数
        float currentBpm = BpmList[0].Bpm; // 默认第一个BPM

        for (int i = 0; i < BpmList.Length; i++)
        {
            var bpmEvent = BpmList[i];
            float eventBeat = bpmEvent.StartTime[0] + (float)bpmEvent.StartTime[1] / bpmEvent.StartTime[2];

            if (totalBeats >= eventBeat)
            {
                // 累加从上一个BPM点到这个BPM点的时间
                if (i > 0)
                {
                    float beatDiff = eventBeat - lastBpmBeat;
                    elapsedSeconds += beatDiff * 60f / (float)currentBpm;
                }
                lastBpmBeat = eventBeat;
                currentBpm = (float)bpmEvent.Bpm;
            }
            else
            {
                break;
            }
        }

        // 加上从最后一个BPM点到目标Beat的时间
        float remainingBeats = totalBeats - lastBpmBeat;
        elapsedSeconds += remainingBeats * 60f / currentBpm;

        return elapsedSeconds;
    }

    /// <summary>
    /// 时间转换：将Beat（int[]）转换为秒
    /// </summary>
    /// <param name="beatValue">当前节拍数</param>
    /// <param name="BpmList">谱面的所有bpm事件</param>
    /// <returns></returns>
    public static float BeatToSecond(float beatValue, BpmEvent[] BpmList)
    {
        // 找到当前Beat所在的BPM段并累积时间
        float elapsedSeconds = 0;
        float lastBpmBeat = 0; // 上一个BPM事件的总拍数
        float currentBpm = BpmList[0].Bpm; // 默认第一个BPM

        for (int i = 0; i < BpmList.Length; i++)
        {
            var bpmEvent = BpmList[i];
            float eventBeat = bpmEvent.StartTime[0] + (float)bpmEvent.StartTime[1] / bpmEvent.StartTime[2];

            if (beatValue >= eventBeat)
            {
                // 累加从上一个BPM点到这个BPM点的时间
                if (i > 0)
                {
                    float beatDiff = eventBeat - lastBpmBeat;
                    elapsedSeconds += beatDiff * 60f / (float)currentBpm;
                }
                lastBpmBeat = eventBeat;
                currentBpm = (float)bpmEvent.Bpm;
            }
            else
            {
                break;
            }
        }

        // 加上从最后一个BPM点到目标Beat的时间
        float remainingBeats = beatValue - lastBpmBeat;
        elapsedSeconds += remainingBeats * 60f / currentBpm;

        return elapsedSeconds;
    }

    /// <summary>
    /// 时间转换：将秒转换为Beat
    /// </summary>
    /// <param name="secondValue">当前秒数</param>
    /// <param name="BpmList">谱面的所有bpm事件</param>
    /// <returns>对应的节拍数</returns>
    public static float SecondToBeat(float secondValue, BpmEvent[] BpmList)
    {
        // 处理空列表或无效输入
        if (BpmList == null || BpmList.Length == 0 || secondValue < 0)
        {
            GD.PrintErr($"[Util] SecondToBeat() 输入不合法");
            return 0f;
        }

        // 辅助函数：计算事件的绝对节拍（与BeatToSecond中的计算方式一致）
        float GetEventBeat(BpmEvent e) => e.StartTime[0] + (float)e.StartTime[1] / e.StartTime[2];

        // 累积已处理的时间（秒）
        float elapsedSeconds = 0f;
        // 当前BPM段的起始节拍
        float currentBeat = GetEventBeat(BpmList[0]);

        // 遍历BPM段（除最后一个事件外，每个段由当前事件到下一个事件构成）
        for (int i = 0; i < BpmList.Length - 1; i++)
        {
            BpmEvent curEvent = BpmList[i];
            BpmEvent nextEvent = BpmList[i + 1];

            float startBeat = GetEventBeat(curEvent);
            float endBeat = GetEventBeat(nextEvent);
            float bpm = (float)curEvent.Bpm;

            // 当前段的节拍跨度
            float beatDiff = endBeat - startBeat;
            // 当前段的持续时间（秒）
            float segmentSeconds = beatDiff * 60f / bpm;

            if (secondValue <= elapsedSeconds + segmentSeconds)
            {
                // 目标秒数落在当前段内
                float offsetSeconds = secondValue - elapsedSeconds;
                float offsetBeats = offsetSeconds * bpm / 60f;
                return startBeat + offsetBeats;
            }

            // 否则，累加时间并移动到下一段的起始节拍
            elapsedSeconds += segmentSeconds;
            currentBeat = endBeat;
        }

        // 处理最后一个BPM段（从最后一个事件到无限远）
        BpmEvent lastEvent = BpmList[BpmList.Length - 1];
        float lastBeat = GetEventBeat(lastEvent);
        float lastBpm = (float)lastEvent.Bpm;
        float remainingSeconds = secondValue - elapsedSeconds;
        float remainingBeats = remainingSeconds * lastBpm / 60f;
        return lastBeat + remainingBeats;
    }

    /// <summary>
    /// 将谱面坐标映射到Container的坐标
    /// </summary>
    /// <param name="pos">谱面坐标</param>
    /// <param name="containerSize">Container大小</param>
    /// <returns></returns>
    public static Vector2 ChartPosToViewportPos(Vector2 pos, Vector2 containerSize)
    {
        //获取屏幕大小
        // Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        // //[-675,675] -> [0,X]
        // float newX = (viewportSize.X/1350f) * pos.X + (viewportSize.X/2f);
        // //[-450,450] -> [Y,0]
        // float newY = (-viewportSize.Y / 900f) * pos.Y + (viewportSize.Y/2f);
        
        //根据容器大小调整
        float ratioX = (pos.X + 675f) / 1350f;
        float newX = ratioX * containerSize.X;

        float ratioY = (pos.Y + 450f) / 900f;
        float newY = (1 - ratioY) * containerSize.Y;

        return new Vector2(newX,newY);
    }

    /// <summary>
    /// 将note在谱面中相对于判定线的坐标映射到Godot中相对于判定线的的坐标
    /// </summary>
    /// <param name="pos">note在谱面中相对于判定线的坐标</param>
    /// <param name="containerSize">Container大小</param>
    /// <returns>Godot中相对于判定线的的坐标</returns>
    public static Vector2 ChartPosToLocalPos(Vector2 pos, Vector2 containerSize)
    {
        float localY = -pos.Y * (containerSize.Y / 900f);
        float localX = pos.X * (containerSize.X / 1350f);

        return new Vector2(localX, localY);
    }

}
