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

    /// <summary>
    /// 导入谱面
    /// </summary>
    /// <param name="path">谱面文件（zip、pez等）的路径</param>
    public void ImportChart(string path)
    {
        //1. 创建临时导入目录
        string tempId = Util.GenerateRandomId(14);
        string tempDir = Path.Combine("user://temp_import", tempId);
        Util.EnsureDirectoryExists(tempDir);

        // 2. 解压 ZIP 到临时目录
        GD.Print($"开始解压: {path} -> {tempDir}");
        Util.UnzipFileTo(path, tempDir); // 解压完成会在内部打印

        //3. 寻找info.txt文件，读取其他3个文件的路径
        string songTempPath, picTempPath, jsonTempPath;
        string infoTempPath = Path.Combine(tempDir, "info.txt");
        Dictionary<string, string> infoDic = new Dictionary<string, string>();
        if (!Godot.FileAccess.FileExists(infoTempPath))
        {
            GD.PrintErr("无法找到info.txt文件");
            return;
        }
        infoDic = Util.ReadInfoFile(infoTempPath);
        jsonTempPath = Path.Combine(tempDir, infoDic["Chart"]);
        picTempPath = Path.Combine(tempDir, infoDic["Picture"]);
        songTempPath = Path.Combine(tempDir, infoDic["Song"]);
        

        //创建导入目录
        string id = Util.GenerateRandomId(14);
        string dir = Path.Combine(chartRepository.GetSavesDir(), id);
        Util.EnsureDirectoryExists(dir);

        //修改id信息
        infoDic["Chart"] = $"{id}.json";
        infoDic["Song"] = $"{id}.{songTempPath.GetExtension()}";
        infoDic["Picture"] = $"{id}.{picTempPath.GetExtension()}";
        Util.WriteInfoFile(infoTempPath, infoDic);

        //复制文件
        Util.CopyFile(infoTempPath, Path.Combine(dir, "info.txt"));
        Util.CopyFile(jsonTempPath, Path.Combine(dir, infoDic["Chart"]));
        Util.CopyFile(picTempPath, Path.Combine(dir, infoDic["Picture"]));
        Util.CopyFile(songTempPath, Path.Combine(dir, infoDic["Song"]));

        

    }
    
    /// <summary>
    /// 设置谱面信息
    /// </summary>
    /// <param name="chartId">谱面id</param>
    /// <param name="chartInfo">谱面信息</param>
    public void SetChartInfo(string chartId, ChartInfo chartInfo)
    {
        chartRepository.SaveChartInfo(chartInfo);
    }

    /// <summary>
    /// 修改谱面的曲绘文件
    /// </summary>
    /// <param name="chartId">要修改的铺面</param>
    /// <param name="path">新的曲绘文件的路径</param>
    public void SetChartPic(string chartId, string path)
    {
        if (!DirAccess.DirExistsAbsolute(path))
        {
            GD.PrintErr($"[ChartService] 曲绘路径不存在:{path}");
            return;
        }
        //找到谱面路径
        string dir = Path.Combine(chartRepository.GetSavesDir(), chartId);
        Util.EnsureDirectoryExists(dir);

        //读取info.txt
        ChartInfo chartInfo = chartRepository.LoadChartInfo(chartId);

        //删除原本的曲绘
        string picPath = chartInfo.PicturePath;
        DirAccess.RemoveAbsolute(picPath);

        //复制新的曲绘
        Util.CopyFile(path, picPath);
    }

    /// <summary>
    /// 修改谱面的音频文件
    /// </summary>
    /// <param name="chartId">要修改的铺面</param>
    /// <param name="path">新的音频文件的路径</param>
    public void SetChartSong(string chartId, string path)
    {
        if (!DirAccess.DirExistsAbsolute(path))
        {
            GD.PrintErr($"[ChartService] 音乐路径不存在:{path}");
            return;
        }
        //找到谱面路径
        string dir = Path.Combine(chartRepository.GetSavesDir(), chartId);
        Util.EnsureDirectoryExists(dir);

        //读取info.txt
        ChartInfo chartInfo = chartRepository.LoadChartInfo(chartId);

        //删除原本的音频
        string songPath = chartInfo.SongPath;
        DirAccess.RemoveAbsolute(songPath);

        //复制新的音频
        Util.CopyFile(path, songPath);
    }

    /// <summary>
    /// 获取所有谱面的基本信息
    /// </summary>
    /// <returns>所有谱面信息</returns>
    public List<ChartInfo> GetAllCharts()
    {
        return chartRepository.LoadAllCharts();
    }

    /// <summary>
    /// 获取谱面的基本信息
    /// </summary>
    /// <param name="chartId">谱面id</param>
    /// <returns>谱面信息</returns>
    public ChartInfo GetChartInfo(string chartId)
    {
        ChartInfo info = chartRepository.LoadChartInfo(chartId);
        if(info == null)
        {
            GD.PrintErr($"{this.Name} GetChartInfo(string chartId) chartInfo == null");
        }
        return info;
    }

    public void ExportChart(string chartId)
    {
        List<string> filePaths = new List<string>();//用于存储将要打包的文件的路径

        //1. 读取info
        ChartInfo chartInfo = chartRepository.LoadChartInfo(chartId);
        string infoPath = chartInfo.InfoFilePath;
        filePaths.Add(infoPath);

        //2. 找到json谱面文件
        string jsonPath = chartInfo.ChartPath;
        filePaths.Add(jsonPath);

        //3. 找到音乐文件
        string songPath = chartInfo.SongPath;
        filePaths.Add(songPath);

        //4，找到曲绘文件
        string picPath = chartInfo.PicturePath;
        filePaths.Add(picPath);

        //5. 调用原生文件对话框
        string[] filters = {"*.*;所有文件;"};
        fileDialogManager.SaveFile(
            (zipPath) =>
            {
                Util.CreateZip(filePaths, zipPath);
                GD.Print($"[{this.Name}] 成功创建zip文件:{zipPath}");
            },
            filters
        );
    }

}
