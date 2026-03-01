using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;

public partial class ChartService : Node
{
    [Export] private ChartRepository _repository;
    [Export] private FileDialogManager _fileDialog;

    // 创建新谱面（返回生成的ChartInfo）
    public ChartInfo CreateNewChart(ChartInfo data)
    {
        string id = Util.GenerateRandomId();
        var info = new ChartInfo
        {
            Id = id,
            Name = data.Name,
            Composer = data.Composer,
            Charter = data.Charter,
            Bpm = data.Bpm,
            Duration = data.Duration,
            SongFileName = $"{id}.{Path.GetExtension(data.SongPath)}",
            PictureFileName = $"{id}.{Path.GetExtension(data.PicturePath)}",
            ChartFileName = $"{id}.json"
        };

        // 创建目录
        Util.EnsureDirectoryExists(info.FolderPath);

        // 复制音乐和曲绘
        Util.CopyFile(data.SongPath, info.SongPath);
        Util.CopyFile(data.PicturePath, info.PicturePath);

        // 生成谱面JSON（从模板复制并修改）
        string templatePath = "res://TemplateChart.json";
        Util.CopyFile(templatePath, info.ChartPath);
        var chart = ChartLoader.LoadChart(info.ChartPath);
        chart.BpmList[0].Bpm = info.Bpm;
        chart.Meta = new Meta
        {
            Background = data.PictureFileName,
            Charter = data.Charter,
            Composer = data.Composer,
            Duration = data.Duration,
            Id = id,
            Illustration = "", // TODO
            Level = "0", // TODO
            Name = data.Name,
            Offset = 0,
            Song = data.SongFileName
        };
        ChartLoader.SaveChart(chart, info.ChartPath);

        // 保存info.txt
        _repository.SaveChartInfo(info);

        return info;
    }

    // 删除谱面
    public void DeleteChart(string chartId)
    {
        _repository.DeleteChart(chartId);
    }

    // 导入谱面
    //TODO

    public List<ChartInfo> GetAllCharts()
    {
        //TODO
        return null;
    }
}
