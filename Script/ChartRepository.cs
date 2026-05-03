using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;


public partial class ChartRepository : Node
{
    public const string SAVES_DIR = "user://ChartSaves";

    public List<ChartInfo> LoadAllCharts()
    {
        var list = new List<ChartInfo>();
        var dir = DirAccess.Open(SAVES_DIR);
        if (dir == null) return list;

        foreach (string subDir in dir.GetDirectories())
        {
            var info = LoadChartInfo(subDir);
            if (info != null) list.Add(info);
        }
        return list;
    }

    public ChartInfo LoadChartInfo(string chartId)
    {
        string folder = Path.Combine(SAVES_DIR, chartId);
        string infoPath = Path.Combine(folder, "info.txt");
        if (!Godot.FileAccess.FileExists(infoPath)) return null;

        var dict = Util.ReadInfoFile(infoPath);
        return new ChartInfo
        {
            Id = chartId,
            Name = dict.GetValueOrDefault("Name"),
            Composer = dict.GetValueOrDefault("Composer"),
            Charter = dict.GetValueOrDefault("Charter"),
            Level = dict.GetValueOrDefault("Level", "0"),
            Bpm = float.Parse(dict.GetValueOrDefault("Bpm", "0")),
            Offset = float.Parse(dict.GetValueOrDefault("Offset", "0")),
            Duration = float.Parse(dict.GetValueOrDefault("Duration", "0")),
            SongFileName = dict.GetValueOrDefault("Song"),
            PictureFileName = dict.GetValueOrDefault("Picture"),
            ChartFileName = dict.GetValueOrDefault("Chart")
        };
    }

    public void SaveChartInfo(ChartInfo info)
    {
        var dict = new Dictionary<string, string>
        {
            ["Name"] = info.Name,
            ["Composer"] = info.Composer,
            ["Charter"] = info.Charter,
            ["Level"] = info.Level,
            ["Bpm"] = info.Bpm.ToString(),
            ["Offset"] = info.Offset.ToString(),
            ["Duration"] = info.Duration.ToString(),
            ["Song"] = info.SongFileName,
            ["Picture"] = info.PictureFileName,
            ["Chart"] = info.ChartFileName
        };
        Util.WriteInfoFile(info.InfoFilePath, dict);
    }

    public void DeleteChart(string chartId)
    {
        string folder = Path.Combine(SAVES_DIR, chartId);
        if (DirAccess.DirExistsAbsolute(folder))
        {
            // 递归删除文件夹
            Util.DeleteDirectoryRecursive(folder);
        }
    }

    


    /// <summary>
    /// 获取谱面存档的目录路径
    /// </summary>
    /// <returns>谱面存档的目录路径</returns>
    public string GetSavesDir()
    {
        return SAVES_DIR;
    }

    
}
