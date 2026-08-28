using Godot;
using System;
using QuickType;
using System.Text.Json;
using Newtonsoft.Json;

public partial class ChartLoader : Node
{

    public override void _Ready()
    {
        base._Ready();
        

        // // 使用带路径的加载方法
        // string loadPath = "res://Chart.json";
        // chart = LoadChart(loadPath);

        // if (chart == null)
        // {
        //     GD.PrintErr("铺面导入失败");
        // }
        // else
        // {
        //     GD.Print("铺面导入成功");
        //     GD.Print(chart.ChartTime);
        // }

        // // 保存到另一个路径
        // string savePath = "res://Chart2.json";
        // SaveChart(chart, savePath);

        //zipExtractor.UnzipFileTo("res://Snow Desert.zip", "res://ChartImport");

    }

    /// <summary>
    /// 从指定路径加载 Chart 数据
    /// </summary>
    /// <param name="path">Godot 资源路径（如 res://Chart.json）或绝对路径</param>
    /// <returns>反序列化后的 Chart 对象，失败返回 null</returns>
    public static Chart LoadChart(string path)
    {
        // 1. 参数校验
        if (string.IsNullOrEmpty(path))
        {
            GD.PrintErr("文件路径不能为空");
            return null;
        }

        // 2. 检查文件是否存在
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"文件不存在: {path}");
            return null;
        }

        // 3. 打开文件
        var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"无法打开文件: {path}");
            return null;
        }

        // 4. 读取 JSON 字符串
        string jsonData = file.GetAsText();
        file.Dispose();

        // 5. 使用生成的 FromJson 方法反序列化（包含正确的转换器设置）
        try
        {
            Chart data = Chart.FromJson(jsonData);
            return data;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"反序列化失败: {ex.Message}\n路径: {path}");
            return null;
        }
    }

    /// <summary>
    /// 将 Chart 数据保存到指定路径
    /// </summary>
    /// <param name="data">要保存的 Chart 对象</param>
    /// <param name="path">保存路径（如 user://Chart2.json）</param>
    public static void SaveChart(Chart data, string path)
    {
        if (data == null)
        {
            GD.PrintErr("保存失败：Chart 数据为 null");
            return;
        }

        if (string.IsNullOrEmpty(path))
        {
            GD.PrintErr("保存路径不能为空");
            return;
        }

        // 使用生成的 ToJson 方法序列化（保持一致性）
        string jsonData = data.ToJson();

        var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"无法创建/写入文件: {path}");
            return;
        }

        file.StoreString(jsonData);
        file.Dispose();
        GD.Print($"Chart 已保存到: {path}");
    }
    
}


