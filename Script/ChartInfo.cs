using Godot;
using System;
using System.IO;

public partial class ChartInfo : GodotObject
{
    public string Id { get; set; }          // 唯一标识（文件夹名）
    public string Name { get; set; }
    public string Composer { get; set; }
    public string Charter { get; set; }
    public string Level { get; set; }        // 预留
    public float Bpm { get; set; }
    public float Offset { get; set; }
    public float Duration { get; set; }
    public string SongFileName { get; set; } // 音乐文件名
    public string PictureFileName { get; set; } // 曲绘文件名
    public string ChartFileName { get; set; } // 谱面JSON文件名

    // 计算属性：完整路径
    public string FolderPath => Path.Combine("user://ChartSaves", Id);
    public string InfoFilePath => Path.Combine(FolderPath, "info.txt");
    public string SongPath => Path.Combine(FolderPath, SongFileName);
    public string PicturePath => Path.Combine(FolderPath, PictureFileName);
    public string ChartPath => Path.Combine(FolderPath, ChartFileName);

}
