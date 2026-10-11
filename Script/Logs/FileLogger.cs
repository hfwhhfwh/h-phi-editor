using Godot;
using System;

public partial class FileLogger : Node, ILogger
{
    private const string LogDir = "user://GameLog";

    private readonly string _logFilePath;
    private readonly object _lock = new object();

    public static ILogger Instance;

    // ==================== 静态快捷 API ====================
    // 约定：
    //  - 命名用 Log*/ 而不是 Print*，避免与实例方法（Print/PrintErr/PrintWarning）冲突
    //  - Instance 未就绪（_Ready 之前 / 已退出）时回退到 GD.Print*，保证不丢日志
    //  - 具体落盘/加锁由实例方法负责，静态层只做分发

    /// <summary>日志器是否已就绪（Instance 可用）。</summary>
    public static bool IsReady => Instance != null;

    /// <summary>普通信息日志。</summary>
    public static void Log(string message)
    {
        if (Instance != null) Instance.Print(message);
        else GD.Print(message); // 回退，避免丢日志
    }

    /// <summary>错误日志。</summary>
    public static void LogErr(string message)
    {
        if (Instance != null) Instance.PrintErr(message);
        else GD.PrintErr(message);
    }

    /// <summary>警告日志。</summary>
    public static void LogWarning(string message)
    {
        if (Instance != null) Instance.PrintWarning(message);
        else GD.PrintRich($"[color=yellow]{message}[/color]");
    }

    // 可选：格式化重载，省得调用方到处写 $"..."
    public static void Log(string format, params object[] args)
        => Log(string.Format(format, args));

    public static void LogErr(string format, params object[] args)
        => LogErr(string.Format(format, args));

    public static void LogWarning(string format, params object[] args)
        => LogWarning(string.Format(format, args));
    // ======================================================

    public override void _Ready()
    {
        // ========== 单例保护 ==========
        if (Instance != null)
        {
            FileLogger.LogWarning($"[{Name}] 单例已存在（ILogger），销毁当前重复实例");
            QueueFree();
            return;
        }

        Instance = this;
        // =============================

        DirAccess.MakeDirRecursiveAbsolute(LogDir);

        // 先用 Write 模式创建空文件（若已存在会截断，符合"每次运行新文件"的设计）
        using (var f = FileAccess.Open(_logFilePath, FileAccess.ModeFlags.Write))
        {
            if (f == null)
                GD.PrintErr($"无法创建日志文件: {FileAccess.GetOpenError()}");
        }

        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===== Log Started =====");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        base._ExitTree();
    }

    public FileLogger()
    {
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
        GD.PushWarning(message);
    }

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            using var file = FileAccess.Open(_logFilePath, FileAccess.ModeFlags.ReadWrite);
            if (file == null)
            {
                GD.PrintErr($"无法打开日志文件: {FileAccess.GetOpenError()}");
                return;
            }
            file.SeekEnd();
            file.StoreLine(line);
        }
    }

    public string GetLogPath() => ProjectSettings.GlobalizePath(_logFilePath);

    public static string GetCurrentLogPath()
    {
        return (Instance as FileLogger)?.GetLogPath();
    }
}