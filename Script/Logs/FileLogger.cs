using Godot;
using System;

public partial class FileLogger : Node, ILogger
{
    private const string LogDir = "user://GameLog";

    private readonly string _logFilePath;
    private readonly object _lock = new object();
    private FileAccess _file;

    public static FileLogger Instance { get; private set; }

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

    public static void Flush()
    {
        if (Instance != null) Instance.FlushInstance();
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
        var err = DirAccess.MakeDirRecursiveAbsolute(LogDir);
        if (err != Error.Ok)
        {
            GD.PrintErr($"创建日志目录失败: {err}");
            return;
        }

        // Write 模式：创建 + 清空（符合"每次运行新文件"）
        _file = FileAccess.Open(_logFilePath, FileAccess.ModeFlags.Write);
        if (_file == null)
        {
            GD.PrintErr($"无法创建日志文件: {FileAccess.GetOpenError()}");
            return;
        }

        Instance = this;

        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===== Log Started =====");
    }

    public override void _ExitTree()
    {
        Instance = null;

        lock (_lock)
        {
            _file?.Flush();
            _file?.Dispose();
            _file = null;
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

    /// <summary>
    /// Flush the log file to ensure all buffered log entries are written to disk.
    /// </summary>
    public void FlushInstance()
    {
        lock (_lock)
        {
            _file?.Flush();
        }
    }

    /// <summary>
    /// Writes a line to the log file.
    /// </summary>
    /// <param name="line">The line to write.</param>
    private void WriteLine(string line)
    {
        lock (_lock)
        {
            if (_file == null)
            {
                GD.PrintErr($"日志文件未正确初始化: {_logFilePath}");
                return;
            }
            _file.StoreLine(line);   // 句柄指针持续在末尾，天然追加
        }
    }

    public string GetLogPath() => ProjectSettings.GlobalizePath(_logFilePath);

    public static string GetCurrentLogPath()
    {
        return (Instance as FileLogger)?.GetLogPath();
    }
}