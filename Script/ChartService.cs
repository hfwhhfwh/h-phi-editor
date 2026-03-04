using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;

public partial class ChartService : Node
{
    private ChartRepository chartRepository;
    private FileDialogManager fileDialogManager;

    public override void _Ready()
    {
        base._Ready();

        chartRepository = GetNode<ChartRepository>("/root/ChartRepository");
        fileDialogManager = GetNode<FileDialogManager>("/root/FileDialogManager");

    }


    /// <summary>
    /// 创建新谱面（返回生成的ChartInfo）
    /// </summary>
    /// <param name="data"></param>
    /// <param name="songPath"></param>
    /// <param name="picPath"></param>
    /// <returns></returns>
    public ChartInfo CreateNewChart(ChartInfo data, string songPath, string picPath)
    {
        ChartInfo chartInfo = new ChartInfo
        {
            Id = data.Id,
            Name = data.Name,
            Composer = data.Composer,
            Charter = data.Charter,
            Level = "0",
            Bpm = data.Bpm,
            Duration = data.Duration,
            SongFileName = $"{data.Id}{Path.GetExtension(data.SongPath)}",
            PictureFileName = $"{data.Id}{Path.GetExtension(data.PicturePath)}",
            ChartFileName = $"{data.Id}.json"
        };

        //GD.Print($"data.Id:{data.Id}, Path.GetExtension(data.PicturePath):{Path.GetExtension(data.PicturePath)}, PictureFileName:{chartInfo.PictureFileName}");

        // 创建目录
        Util.EnsureDirectoryExists(chartInfo.FolderPath);

        // 复制音乐和曲绘
        Util.CopyFile(songPath, chartInfo.SongPath);
        Util.CopyFile(picPath, chartInfo.PicturePath);

        // 生成谱面JSON（从模板复制并修改）
        string templatePath = "res://TemplateChart.json";
        Util.CopyFile(templatePath, chartInfo.ChartPath);
        var chart = ChartLoader.LoadChart(chartInfo.ChartPath);
        chart.BpmList[0].Bpm = chartInfo.Bpm;
        chart.Meta = new Meta
        {
            Background = data.PictureFileName,
            Charter = data.Charter,
            Composer = data.Composer,
            Duration = data.Duration,
            Id = data.Id,
            Illustration = "", // TODO
            Level = data.Level,
            Name = data.Name,
            Offset = 0,
            Song = data.SongFileName
        };
        ChartLoader.SaveChart(chart, chartInfo.ChartPath);

        // 保存info.txt
        chartRepository.SaveChartInfo(chartInfo);

        return chartInfo;
    }

    // 删除谱面
    public void DeleteChart(string chartId)
    {
        chartRepository.DeleteChart(chartId);
    }

    // 导入谱面
    //TODO

    public List<ChartInfo> GetAllCharts()
    {
        return chartRepository.LoadAllCharts();
    }
}
