using Godot;
using System.IO; // 用于 Path 类

public partial class ZipExtractor : Node
{
    

    public override void _Ready()
    {
        base._Ready();
        
    }


    /// <summary>
    /// 将指定路径的ZIP文件解压到与其同名的文件夹中。
    /// </summary>
    /// <param name="zipPath">ZIP文件的完整路径，例如 "user://data.zip"</param>
    public void UnzipFile(string zipPath)
    {
        var zipReader = new ZipReader();
        Error error = zipReader.Open(zipPath);

        if (error != Error.Ok)
        {
            GD.PrintErr($"无法打开ZIP文件: {zipPath}, 错误码: {error}");
            return;
        }

        // 获取ZIP文件所在的目录和文件名（不含扩展名）
        string zipDirectory = zipPath.GetBaseDir();
        string zipFileName = zipPath.GetFile().GetBaseName(); // 例如 "data"
        string extractBasePath = Path.Combine(zipDirectory, zipFileName);

        // 创建基础的解压目录
        DirAccess dir = DirAccess.Open(zipDirectory);
        if (dir == null)
        {
            GD.PrintErr($"无法访问目录: {zipDirectory}");
            zipReader.Close();
            return;
        }
        
        Error makeDirError = dir.MakeDir(zipFileName);
        if (makeDirError != Error.Ok && makeDirError != Error.AlreadyExists)
        {
            GD.PrintErr($"无法创建目录: {extractBasePath}, 错误码: {makeDirError}");
            zipReader.Close();
            return;
        }

        // 遍历ZIP内的所有文件
        string[] files = zipReader.GetFiles();
        foreach (string filePath in files)
        {
            // 计算文件的完整输出路径
            string fullOutputPath = Path.Combine(extractBasePath, filePath);
            string outputDirectory = fullOutputPath.GetBaseDir();

            // 确保文件的子目录存在
            DirAccess.MakeDirRecursiveAbsolute(outputDirectory);

            // 读取ZIP中的文件数据并写入磁盘
            byte[] fileData = zipReader.ReadFile(filePath);
            if (fileData != null)
            {
                using var file = Godot.FileAccess.Open(fullOutputPath, Godot.FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreBuffer(fileData);
                    GD.Print($"已解压: {filePath}");
                }
                else
                {
                    GD.PrintErr($"无法创建输出文件: {fullOutputPath}");
                }
            }
            else
            {
                GD.PrintErr($"无法从ZIP读取文件: {filePath}");
            }
        }

        zipReader.Close();
        GD.Print("解压完成！");
    }

    /// <summary>
    /// 将指定路径的ZIP文件解压到指定路径的文件夹中。
    /// </summary>
    /// <param name="zipPath">ZIP文件的完整路径，例如 "res://ZipFile.zip"</param>
    /// <param name="extractBasePath">解压基础路径，例如 "res://ChartImport/"</param>
    public void UnzipFileTo(string zipPath, string extractBasePath)
    {
        var zipReader = new ZipReader();
        Error error = zipReader.Open(zipPath);

        if (error != Error.Ok)
        {
            GD.PrintErr($"无法打开ZIP文件: {zipPath}, 错误码: {error}");
            return;
        }

        // 遍历ZIP内的所有文件
        string[] files = zipReader.GetFiles();
        foreach (string filePath in files)// 例如 Chart.json
        {
            // 计算文件的完整输出路径
            string fullOutputPath = Path.Combine(extractBasePath, filePath); // 例如 res://ChartImport/Chart.json
            string outputDirectory = fullOutputPath.GetBaseDir(); // 例如 res://ChartImport/

            // 确保文件的子目录存在
            DirAccess.MakeDirRecursiveAbsolute(outputDirectory);

            // 读取ZIP中的文件数据并写入磁盘
            byte[] fileData = zipReader.ReadFile(filePath);
            if (fileData != null)
            {
                using var file = Godot.FileAccess.Open(fullOutputPath, Godot.FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreBuffer(fileData);
                    GD.Print($"已解压: {filePath}");
                }
                else
                {
                    GD.PrintErr($"无法创建输出文件: {fullOutputPath}");
                }
            }
            else
            {
                GD.PrintErr($"无法从ZIP读取文件: {filePath}");
            }
        }

        zipReader.Close();
        GD.Print("解压完成！");
    }

    
    

}