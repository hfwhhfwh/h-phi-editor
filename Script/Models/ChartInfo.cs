using Godot;
using System;
using System.IO;

// Phira info.yml属性:
// "id", "uploader", "name", "difficulty", "level", "charter", "composer", "illustrator",
// "chart", "format", "music", "illustration", "unlockVideo", "previewStart", "previewEnd",
// "aspectRatio", "backgroundDim", "lineLength", "offset", "tip", "tags", "intro",
// "holdPartialCover", "created", "updated", "chartUpdated"

public partial class ChartInfo : GodotObject
{
    public string Id { get; set; }          // 唯一标识（文件夹名）
    public string Name { get; set; }
    public string Composer { get; set; }
    public string Charter { get; set; }
    public string Level { get; set; }        // 预留
    public float? Bpm { get; set; }
    public float? Offset { get; set; }
    public float? Duration { get; set; }
    public string SongFileName { get; set; } // 音乐文件名
    public string PictureFileName { get; set; } // 曲绘文件名
    public string ChartFileName { get; set; } // 谱面JSON文件名


    // ---- Phira info.yml 属性 ----
    public float? Difficulty { get; set; } // 难度（Phira info.yml）
    public string Format { get; set; } // 格式（Phira info.yml）
    public string UnlockVideo { get; set; } // 解锁视频（Phira info.yml）
    public float? PreviewStart { get; set; } // 预览开始时间（Phira info.yml）
    public float? PreviewEnd { get; set; } // 预览结束时间（Phira info.yml）
    public float? AspectRatio { get; set; } // 纵横比（Phira info.yml）
    public float? BackgroundDim { get; set; } // 背景暗度（Phira info.yml）
    public float? LineLength { get; set; } // 线条长度（Phira info.yml）
    public string Tip { get; set; } // 提示（Phira info.yml）
    public string Intro { get; set; } // 简介（Phira info.yml）

    // 计算属性：完整路径
    public string FolderPath => Path.Combine("user://ChartSaves", Id);
    public string InfoTxtPath => Path.Combine(FolderPath, "info.txt");
    public string InfoYmlPath => Path.Combine(FolderPath, "info.yml");
    public string SongPath => Path.Combine(FolderPath, SongFileName);
    public string PicturePath => Path.Combine(FolderPath, PictureFileName);
    public string ChartPath => Path.Combine(FolderPath, ChartFileName);
    
    /// <summary>
    /// 深拷贝
    /// </summary>
    /// <returns>拷贝的ChartInfo</returns>
    public ChartInfo Duplicate()
    {
        return new ChartInfo
        {
            Id = this.Id,
            Name = this.Name,
            Composer = this.Composer,
            Charter = this.Charter,
            Level = this.Level,
            Bpm = this.Bpm,
            Offset = this.Offset,
            Duration = this.Duration,
            SongFileName = this.SongFileName,
            PictureFileName = this.PictureFileName,
            ChartFileName = this.ChartFileName,

            Difficulty = this.Difficulty,
            Format = this.Format,
            UnlockVideo = this.UnlockVideo,
            PreviewStart = this.PreviewStart,
            PreviewEnd = this.PreviewEnd,
            AspectRatio = this.AspectRatio,
            BackgroundDim = this.BackgroundDim,
            LineLength = this.LineLength,
            Tip = this.Tip,
            Intro = this.Intro
        };
    }
}
