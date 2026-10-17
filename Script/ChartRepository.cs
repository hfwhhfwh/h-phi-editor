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
        string infoTxtPath = Path.Combine(folder, "info.txt");
        string infoYmlPath = Path.Combine(folder, "info.yml");

        ChartInfo info;
        if (Godot.FileAccess.FileExists(infoYmlPath))
        {
            info = ChartInfoCodec.Load(infoYmlPath, ChartInfoFormat.Yml);
        }
        else if (Godot.FileAccess.FileExists(infoTxtPath))
        {
            info = ChartInfoCodec.Load(infoTxtPath, ChartInfoFormat.Txt);
        }
        else
        {
            return null;
        }

        // var dict = FileUtil.ReadInfoFile(infoPath);
        // return new ChartInfo
        // {
        //     Id = chartId,
        //     Name = dict.GetValueOrDefault("Name"),
        //     Composer = dict.GetValueOrDefault("Composer"),
        //     Charter = dict.GetValueOrDefault("Charter"),
        //     Level = dict.GetValueOrDefault("Level", "0"),
        //     Bpm = float.Parse(dict.GetValueOrDefault("Bpm", "0")),
        //     Offset = float.Parse(dict.GetValueOrDefault("Offset", "0")),
        //     Duration = float.Parse(dict.GetValueOrDefault("Duration", "0")),
        //     SongFileName = dict.GetValueOrDefault("Song"),
        //     PictureFileName = dict.GetValueOrDefault("Picture"),
        //     ChartFileName = dict.GetValueOrDefault("Chart")
        // };

        return info;
    }

    // public ChartInfo ReadChartInfoFromTxt(string path)
    // {
    //     var infoDic = FileUtil.ReadInfoFile(path);

    //     ChartInfo info = new ChartInfo
    //     {
    //         Id = infoDic.GetValueOrDefault("Path"),
    //         Name = infoDic.GetValueOrDefault("Name"),
    //         Composer = infoDic.GetValueOrDefault("Composer"),
    //         Charter = infoDic.GetValueOrDefault("Charter"),
    //         Level = infoDic.GetValueOrDefault("Level", "0"),
    //         Bpm = ParseFloatSafe(infoDic.GetValueOrDefault("Bpm", "0")),
    //         Offset = ParseFloatSafe(infoDic.GetValueOrDefault("Offset", "0")),
    //         Duration = ParseFloatSafe(infoDic.GetValueOrDefault("Length", "0")),
    //         SongFileName = infoDic.GetValueOrDefault("Song"),
    //         PictureFileName = infoDic.GetValueOrDefault("Picture"),
    //         ChartFileName = infoDic.GetValueOrDefault("Chart")
    //     };
    //     return info;
    // }
    

    // public ChartInfo ReadChartInfoFromYml(string path)
    // {
    //     // Phira info.yml属性:
    //     // "id", "uploader", "name", "difficulty", "level", "charter", "composer", "illustrator",
    //     // "chart", "format", "music", "illustration", "unlockVideo", "previewStart", "previewEnd",
    //     // "aspectRatio", "backgroundDim", "lineLength", "offset", "tip", "tags", "intro",
    //     // "holdPartialCover", "created", "updated", "chartUpdated"

    //     var infoDic = FileUtil.ReadInfoFile(path);

    //     ChartInfo info = new ChartInfo
    //     {
    //         // Id = infoDic.GetValueOrDefault("id"),
    //         // 注意：Phira info.yml中的id不是文件夹名，而是谱面唯一标识
    //         Name = infoDic.GetValueOrDefault("name"),
    //         Composer = infoDic.GetValueOrDefault("composer"),
    //         Charter = infoDic.GetValueOrDefault("charter"),
    //         Level = infoDic.GetValueOrDefault("level", "0"),
    //         Bpm = ParseFloatSafe(infoDic.GetValueOrDefault("Bpm", "0")),
    //         Offset = ParseFloatSafe(infoDic.GetValueOrDefault("offset", "0")),
    //         Duration = ParseFloatSafe(infoDic.GetValueOrDefault("Length", "0")),
    //         SongFileName = infoDic.GetValueOrDefault("music"),
    //         PictureFileName = infoDic.GetValueOrDefault("illustration"),
    //         ChartFileName = infoDic.GetValueOrDefault("chart"),
    //         Difficulty = ParseFloatSafe(infoDic.GetValueOrDefault("difficulty", "0")),
    //         Format = infoDic.GetValueOrDefault("format"),
    //         UnlockVideo = infoDic.GetValueOrDefault("unlockVideo"),
    //         PreviewStart = ParseFloatSafe(infoDic.GetValueOrDefault("previewStart", "0")),
    //         PreviewEnd = ParseFloatSafe(infoDic.GetValueOrDefault("previewEnd", "0")),
    //         AspectRatio = ParseFloatSafe(infoDic.GetValueOrDefault("aspectRatio", "0")),
    //         BackgroundDim = ParseFloatSafe(infoDic.GetValueOrDefault("backgroundDim", "0")),
    //         LineLength = ParseFloatSafe(infoDic.GetValueOrDefault("lineLength", "0")),
    //         Tip = infoDic.GetValueOrDefault("tip"),
    //         Intro = infoDic.GetValueOrDefault("intro")
    //     };
    //     return info;
    // }


    // private static float? ParseFloatSafe(string value, float? defaultValue = null)
    // {
    //     if (string.IsNullOrWhiteSpace(value) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
    //         return defaultValue;
    //     return float.TryParse(value, out float result) ? result : defaultValue;
    // }

    // public void WriteInfoTxt(ChartInfo info, string path)
    // {
    //     ArgumentNullException.ThrowIfNull(info);
    //     ArgumentException.ThrowIfNullOrEmpty(path);

    //     // "Name", "Path", "Song", "Picture", "Chart", "Level", "Composer", "Charter"
    //     Dictionary<string, string> properties = new()
    //     {
    //         {"Name", info.Name},
    //         {"Path", info.Id},
    //         {"Song", info.SongFileName},
    //         {"Picture", info.PictureFileName},
    //         {"Chart", info.ChartFileName},
    //         {"Level", info.Level},
    //         {"Composer", info.Composer},
    //         {"Charter", info.Charter},
    //     };

    //     FileUtil.WriteInfoTxtFile(path, properties);

    // }

    // public void WriteInfoYml(ChartInfo info, string path)
    // {
    //     ArgumentNullException.ThrowIfNull(info);
    //     ArgumentException.ThrowIfNullOrEmpty(path);

    //     // "id", "uploader", "name", "difficulty", "level", "charter", "composer", "illustrator",
    //     // "chart", "format", "music", "illustration", "unlockVideo", "previewStart", "previewEnd",
    //     // "aspectRatio", "backgroundDim", "lineLength", "offset", "tip", "tags", "intro",
    //     // "holdPartialCover", "created", "updated", "chartUpdated"
    //     Dictionary<string, string> properties = new()
    //     {
    //         {"id", info.Id},
    //         {"name", info.Name},
    //         {"difficulty", info.Difficulty.ToString()},
    //         {"level", info.Level},
    //         {"charter", info.Charter},
    //         {"composer", info.Composer},
    //         {"chart", info.ChartFileName},
    //         {"format", info.Format},
    //         {"music", info.SongFileName},
    //         {"illustration", info.PictureFileName},
    //         {"unlockVideo", info.UnlockVideo},
    //         {"previewStart", info.PreviewStart.HasValue ? info.PreviewStart.ToString() : "null"},
    //         {"previewEnd", info.PreviewEnd.HasValue ? info.PreviewEnd.ToString() : "null"},
    //         {"aspectRatio", info.AspectRatio.HasValue ? info.AspectRatio.ToString() : "null"},
    //         {"backgroundDim", info.BackgroundDim.HasValue ? info.BackgroundDim.ToString() : "null"},
    //         {"lineLength", info.LineLength.HasValue ? info.LineLength.ToString() : "null"},
    //         {"offset", info.Offset.HasValue ? info.Offset.ToString() : "null"},
    //         {"tip", info.Tip},
    //         {"intro", info.Intro}
    //     };

    //     FileUtil.WriteInfoYmlFile(path, properties);
    // }

    // public void SaveChartInfoTxt(ChartInfo info)
    // {
    //     /*
    //     #
    //     Name: Snow Desert
    //     Path: 9091515374590503
    //     Song: 9091515374590503.mp3
    //     Picture: 9091515374590503.jpg
    //     Chart: 9091515374590503.json
    //     Level: IN Lv.13
    //     Composer: WyvernP
    //     Charter: hfwh
    //     LastEditTime: 2026_2_21_22_6_36_
    //     Length: 142.341
    //     EditTime: 50462.879
    //     Group: Default
    //     */
    //     var dict = new Dictionary<string, string>
    //     {
    //         ["Name"] = info.Name,
    //         ["Path"] = info.Id,
    //         ["Song"] = info.SongFileName,
    //         ["Picture"] = info.PictureFileName,
    //         ["Chart"] = info.ChartFileName,
    //         ["Level"] = info.Level,
    //         ["Composer"] = info.Composer,
    //         ["Charter"] = info.Charter,
    //         //["LastEditTime"] = 
    //         ["Length"] = info.Duration.ToString(),
    //         //["EditTime"] = 
    //         //["Group"] = 
    //         ["Bpm"] = info.Bpm.ToString(),
    //         ["Offset"] = info.Offset.ToString(),
            
    //     };
    //     FileUtil.WriteInfoTxtFile(info.InfoTxtPath, dict);
    // }

    // public void SaveChartInfoYml(ChartInfo info)
    // {
    //     // Phira info.yml属性:
    //     // "id", "uploader", "name", "difficulty", "level", "charter", "composer", "illustrator",
    //     // "chart", "format", "music", "illustration", "unlockVideo", "previewStart", "previewEnd",
    //     // "aspectRatio", "backgroundDim", "lineLength", "offset", "tip", "tags", "intro",
    //     // "holdPartialCover", "created", "updated", "chartUpdated"

    //     var dict = new Dictionary<string, string>
    //     {
    //         ["id"] = info.Id,
    //         ["name"] = info.Name,
    //         ["difficulty"] = info.Difficulty.ToString(),
    //         ["level"] = info.Level,
    //         ["charter"] = info.Charter,
    //         ["composer"] = info.Composer,
    //         ["chart"] = info.ChartFileName,
    //         ["format"] = info.Format,
    //         ["music"] = info.SongFileName,
    //         ["illustration"] = info.PictureFileName,
    //         ["unlockVideo"] = info.UnlockVideo,
    //         ["previewStart"] = info.PreviewStart.HasValue ? info.PreviewStart.ToString() : "null",
    //         ["previewEnd"] = info.PreviewEnd.HasValue ? info.PreviewEnd.ToString() : "null",
    //         ["aspectRatio"] = info.AspectRatio.HasValue ? info.AspectRatio.ToString() : "null",
    //         ["backgroundDim"] = info.BackgroundDim.HasValue ? info.BackgroundDim.ToString() : "null",
    //         ["lineLength"] = info.LineLength.HasValue ? info.LineLength.ToString() : "null",
    //         ["offset"] = info.Offset.HasValue ? info.Offset.ToString() : "null",
    //         ["tip"] = info.Tip,
    //         ["intro"] = info.Intro
    //     };

    //     FileUtil.WriteInfoYmlFile(info.InfoYmlPath, dict);
    // }

    public void DeleteChart(string chartId)
    {
        string folder = Path.Combine(SAVES_DIR, chartId);
        string folderAbsolute = ProjectSettings.GlobalizePath(folder);
        if (DirAccess.DirExistsAbsolute(folderAbsolute))
        {
            // 递归删除文件夹
            FileUtil.DeleteDirectoryRecursive(folderAbsolute);
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
