using Godot;
using System;
using System.Linq;

public partial class FileDialogManager : Node
{   
    // 单例实例
    public static FileDialogManager Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        base._Ready();
    }

    public override void _ExitTree()
    {
        Instance = null;
        
        base._ExitTree();
    }
    
    /// <summary>
    /// 判断是否处于 iOS 环境（真机或模拟）
    /// </summary>
    private bool IsIOS()
    {
        if (OS.HasFeature("ios"))
        {
            return true;
        }
        if (OS.GetCmdlineUserArgs().Contains("simulate_ios"))
        {
            return true;
        }

        return false;
    }

    public void ShowOpenDialog(Action<string[]> onFileSelected, string[] filters = null, bool multi = false)
    {
        // iOS 不支持原生文件对话框，使用内置的
        if (IsIOS())
        {
            ShowBuiltInOpenDialog(onFileSelected, filters, multi);
        }
        else
        {
            ShowNativeOpenDialog(onFileSelected, filters ?? ["*.*"], multi);
        }
    }

    /// <summary>
    /// 打开原生文件窗口，选择一个文件
    /// </summary>
    /// <param name="onFileSelected">回调参数，当文件选择完成后调用该回调</param>
    private void ShowNativeOpenDialog(Action<string[]> onFileSelected, string[] filters, bool multi = false)
    {
        DisplayServer.FileDialogShow(
            "选择一个文件",
            OS.GetSystemDir(OS.SystemDir.Documents),
            "",
            false,
            multi ? DisplayServer.FileDialogMode.OpenFiles : DisplayServer.FileDialogMode.OpenFile,
            filters:filters,
            Callable.From((bool status, string[] paths, int filterIndex) =>
            {
                if (status && paths.Length > 0)
                {
                    FileLogger.Log($"原生对话框选择的文件: {Util.ListToString(paths)}");
                    onFileSelected?.Invoke(paths); // 调用回调
                }
                else
                {
                    FileLogger.Log("用户取消了选择或发生错误");
                    onFileSelected?.Invoke(null); // 传递 null 表示取消
                }
            }
        ));
    }

    private void ShowNativeOpenDialog(Action<string[]> onFileSelected, bool multi = false)
    {
        string[] filters = ["*.*"];
        ShowNativeOpenDialog(onFileSelected, filters, multi);
    }

    private void ShowBuiltInOpenDialog(Action<string[]> onFileSelected, string[] filters, bool multi)
    {
        var dialog = new FileDialog();
        dialog.FileMode = multi ? FileDialog.FileModeEnum.OpenFiles : FileDialog.FileModeEnum.OpenFile;
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.UseNativeDialog = false;
        

        // 关键修复：iOS 上用 OS.GetUserDataDir() 代替 OS.GetSystemDir()
        dialog.CurrentPath = OS.GetUserDataDir() + "/";
        
        if (filters != null)
        {
            foreach (var f in filters) dialog.AddFilter(f);
        }
        
        dialog.FileSelected += path => { onFileSelected?.Invoke([path]); dialog.QueueFree(); };
        dialog.FilesSelected += (string[] paths) =>
        {
            onFileSelected?.Invoke(paths);
            dialog.QueueFree();
        };
        dialog.Canceled += () => { onFileSelected?.Invoke(null); dialog.QueueFree(); };
        
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 700));
    }

    public void ShowSelectDirectoryDialog(Action<string> onDirectorySelected, string title = "选择目录")
    {
        if (IsIOS())
        {
            ShowBuiltInSelectDirectoryDialog(onDirectorySelected, title);
            return;
        }

        DisplayServer.FileDialogShow(
            title: title,
            OS.GetSystemDir(OS.SystemDir.Documents),
            fileName: "",
            showHidden: false,
            DisplayServer.FileDialogMode.OpenDir,
            filters: [],
            Callable.From((bool status, string[] paths, int filterIndex) =>
            {
                if (status && paths.Length > 0)
                {
                    onDirectorySelected?.Invoke(paths[0]);
                }
                else
                {
                    onDirectorySelected?.Invoke(null);
                }
            })
        );
    }

    private void ShowBuiltInSelectDirectoryDialog(Action<string> onDirectorySelected, string title = "选择目录")
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = title,
            CurrentPath = OS.GetUserDataDir() + "/"
        };

        dialog.DirSelected += path =>
        {
            onDirectorySelected?.Invoke(path);
            dialog.QueueFree();
        };
        dialog.Canceled += () =>
        {
            onDirectorySelected?.Invoke(null);
            dialog.QueueFree();
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 700));
    }


    public void ShowSaveDialog(Action<string> onFileSelected, 
        Action onCalceled = null, 
        string[] filters = null, 
        string defaultName = "请输入文件名称",
        string openDir = null)
    {
        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.SaveFile;
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.UseNativeDialog = true;
        dialog.Title = "保存文件";

        openDir ??= OS.GetUserDataDir() + "/";
        dialog.CurrentPath = openDir;

        dialog.CurrentFile = defaultName;

        if (filters != null)
        {
            foreach (var filter in filters)
            {
                dialog.AddFilter(filter);
            }
        }

        dialog.FileSelected += (path) =>
        {
            FileLogger.Log($"对话框保存路径: {path}");
            onFileSelected?.Invoke(path);
            dialog.QueueFree();
        };

        dialog.Canceled += () =>
        {
            FileLogger.Log("用户取消了保存");
            onCalceled?.Invoke();
            dialog.QueueFree();
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 700));
        
    }

    public void ShowNativeSaveDialog(Action<string> onFileSelected, 
        Action onCalceled = null, 
        string[] filters = null, 
        string defaultName = "请输入文件名称",
        string openDir = null)
    {
        DisplayServer.FileDialogShow(
            "保存文件",
            openDir ?? OS.GetSystemDir(OS.SystemDir.Documents),
            defaultName,
            false,
            DisplayServer.FileDialogMode.SaveFile,
            filters:filters,
            Callable.From((bool status, string[] paths, int filterIndex) =>
                {
                    if (status && paths.Length > 0)
                    {
                        string filePath = paths[0];
                        FileLogger.Log($"原生对话框选择的文件: {filePath}");
                        onFileSelected?.Invoke(filePath); // 调用回调
                    }
                    else
                    {
                        FileLogger.Log("用户取消了选择或发生错误");
                        onCalceled?.Invoke(); // 取消
                    }
                }
            )
        );
    }

    public void ShowBuiltInSaveDialog(Action<string> onFileSelected, 
        Action onCalceled = null, 
        string[] filters = null, 
        string defaultName = "请输入文件名称",
        string openDir = null)
    {
        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.SaveFile;
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.UseNativeDialog = false;
        dialog.Title = "保存文件";

        openDir ??= OS.GetUserDataDir() + "/";
        dialog.CurrentPath = openDir;

        dialog.CurrentFile = defaultName;

        if (filters != null)
        {
            foreach (var filter in filters)
            {
                dialog.AddFilter(filter);
            }
        }

        dialog.FileSelected += (path) =>
        {
            FileLogger.Log($"对话框保存路径: {path}");
            onFileSelected?.Invoke(path);
            dialog.QueueFree();
        };

        dialog.Canceled += () =>
        {
            FileLogger.Log("用户取消了保存");
            onCalceled?.Invoke();
            dialog.QueueFree();
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 700));
    }
    
}
