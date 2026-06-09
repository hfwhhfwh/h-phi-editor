using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;


public static class Util
{
    

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

	

}
