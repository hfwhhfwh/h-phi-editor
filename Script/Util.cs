using Godot;
using System;
using System.Collections.Generic;
using System.IO;


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

	public static void EnsureDirectoryExists(string path)
	{
		//TODO
	}

	public static float GetMusicDuration(string path)
	{
		return 0f;
		//TODO
	}

}
