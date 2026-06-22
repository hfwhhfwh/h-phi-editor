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

        var dict = FileUtil.ReadInfoFile(infoPath);
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
        /*
        #
        Name: Snow Desert
        Path: 9091515374590503
        Song: 9091515374590503.mp3
        Picture: 9091515374590503.jpg
        Chart: 9091515374590503.json
        Level: IN Lv.13
        Composer: WyvernP
        Charter: hfwh
        LastEditTime: 2026_2_21_22_6_36_
        Length: 142.341
        EditTime: 50462.879
        Group: Default
        */
        var dict = new Dictionary<string, string>
        {
            ["Name"] = info.Name,
            ["Path"] = info.Id,
            ["Song"] = info.SongFileName,
            ["Picture"] = info.PictureFileName,
            ["Chart"] = info.ChartFileName,
            ["Level"] = info.Level,
            ["Composer"] = info.Composer,
            ["Charter"] = info.Charter,
            //["LastEditTime"] = 
            ["Length"] = info.Duration.ToString(),
            //["EditTime"] = 
            //["Group"] = 
            ["Bpm"] = info.Bpm.ToString(),
            ["Offset"] = info.Offset.ToString(),
            
        };
        FileUtil.WriteInfoFile(info.InfoFilePath, dict);
    }

    public void DeleteChart(string chartId)
    {
        string folder = Path.Combine(SAVES_DIR, chartId);
        if (DirAccess.DirExistsAbsolute(folder))
        {
            // 递归删除文件夹
            FileUtil.DeleteDirectoryRecursive(folder);
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
