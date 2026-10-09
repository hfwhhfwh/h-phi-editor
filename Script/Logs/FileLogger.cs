using Godot;
using System;

public partial class FileLogger : Node, ILogger
{
    private const string LogDir = "user://GameLog";

    private readonly string _logFilePath;
    private readonly object _lock = new object();

    public static ILogger Instance;

    public override void _Ready()
    {
        // ========== 单例保护 ==========
        if (Instance != null)
        {
            GD.PushWarning($"[{Name}] 单例已存在（ILogger），销毁当前重复实例");
            QueueFree();  // 自杀，保留旧实例
            return;
        }

        Instance = this;
        // =============================

        // 确保目录存在
        DirAccess.MakeDirRecursiveAbsolute(LogDir);

        // 每次运行新建文件，写入启动头
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===== Log Started =====");
    }

    public override void _ExitTree()
    {
        // 先清自己的引用，再交给 base
        if (Instance == this)
        {
            Instance = null;
        }
        base._ExitTree();
    }

    public FileLogger()
    {
        // 文件名：GameLog_2026-10-07_14-30-25.log
        string fileName = $"GameLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
        _logFilePath = $"{LogDir}/{fileName}";
    }

    public void Print(string message)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}");
        GD.Print(message);
    }

    public void PrintErr(string message)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}");
        GD.PrintErr(message);
    }

    public void PrintWarning(string message)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARN] {message}");
        GD.PrintRich($"[color=yellow]{message}[/color]");
    }

    /// <summary>
    /// 同步写入一行日志。使用 lock 防止多线程并发写入冲突。
    /// </summary>
    private void WriteLine(string line)
    {
        lock (_lock)
        {
            using var file = FileAccess.Open(_logFilePath, FileAccess.ModeFlags.WriteRead);
            if (file == null)
            {
                GD.PrintErr($"无法打开日志文件: {FileAccess.GetOpenError()}");
                return;
            }
            file.SeekEnd();
            file.StoreLine(line);
        }
    }

    /// <summary>
    /// 获取当前日志文件的绝对路径，便于调试时定位。
    /// </summary>
    public string GetLogPath() => ProjectSettings.GlobalizePath(_logFilePath);
}