using Godot;
using System;
using System.Linq;

public partial class FileDialogManager : Node
{   

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

    public void ShowOpenDialog(Action<string> onFileSelected, string[] filters = null)
    {
        // iOS 不支持原生文件对话框，使用内置的
        if (IsIOS())
        {
            ShowBuiltInOpenDialog(onFileSelected, filters);
        }
        else
        {
            ShowNativeOpenDialog(onFileSelected, filters ?? ["*.*"]);
        }
    }

    /// <summary>
    /// 打开原生文件窗口，选择一个文件
    /// </summary>
    /// <param name="onFileSelected">回调参数，当文件选择完成后调用该回调</param>
    private void ShowNativeOpenDialog(Action<string> onFileSelected, string[] filters)
    {
        DisplayServer.FileDialogShow(
            "选择一个文件",
            OS.GetSystemDir(OS.SystemDir.Documents),
            "",
            false,
            DisplayServer.FileDialogMode.OpenFile,
            filters:filters,
            Callable.From((bool status, string[] paths, int filterIndex) =>
            {
                if (status && paths.Length > 0)
                {
                    string filePath = paths[0];
                    GD.Print($"原生对话框选择的文件: {filePath}");
                    onFileSelected?.Invoke(filePath); // 调用回调
                }
                else
                {
                    GD.Print("用户取消了选择或发生错误");
                    onFileSelected?.Invoke(null); // 传递 null 表示取消
                }
            }
        ));
    }

    private void ShowNativeOpenDialog(Action<string> onFileSelected)
    {
        string[] filters = ["*.*"];
        ShowNativeOpenDialog(onFileSelected, filters);
    }

    private void ShowBuiltInOpenDialog(Action<string> onFileSelected, string[] filters)
    {
        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.UseNativeDialog = false;

        // 关键修复：iOS 上用 OS.GetUserDataDir() 代替 OS.GetSystemDir()
        dialog.CurrentPath = OS.GetUserDataDir() + "/";
        
        if (filters != null)
        {
            foreach (var f in filters) dialog.AddFilter(f);
        }
        
        dialog.FileSelected += path => { onFileSelected?.Invoke(path); dialog.QueueFree(); };
        dialog.Canceled += () => { onFileSelected?.Invoke(null); dialog.QueueFree(); };
        
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 700));
    }


    public void SaveFile(Action<string> onFileSelected, string[] filters)
    {
        if (IsIOS())
        {
            ShowBuiltInSaveDialog(onFileSelected, filters);
        }
        else
        {
            ShowNativeSaveDialog(onFileSelected, filters);
        }
    }

    public void SaveFile(Action<string> onFileSelected)
    {
        SaveFile(onFileSelected, new[] { "*.*" });
    }

    private void ShowNativeSaveDialog(Action<string> onFileSelected, string[] filters)
    {
        DisplayServer.FileDialogShow(
            "选择一个文件",
            OS.GetSystemDir(OS.SystemDir.Documents),
            "",
            false,
            DisplayServer.FileDialogMode.SaveFile,
            filters:filters,
            Callable.From((bool status, string[] paths, int filterIndex) =>
                {
                    if (status && paths.Length > 0)
                    {
                        string filePath = paths[0];
                        GD.Print($"原生对话框选择的文件: {filePath}");
                        onFileSelected?.Invoke(filePath); // 调用回调
                    }
                    else
                    {
                        GD.Print("用户取消了选择或发生错误");
                        onFileSelected?.Invoke(null); // 传递 null 表示取消
                    }
                }
            )
        );
    }

    private void ShowBuiltInSaveDialog(Action<string> onFileSelected, string[] filters)
    {
        var dialog = new FileDialog();
        dialog.FileMode = FileDialog.FileModeEnum.SaveFile;
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.UseNativeDialog = false;
        dialog.Title = "保存文件";

        // 关键修复：iOS 上用 OS.GetUserDataDir() 代替 OS.GetSystemDir()
        dialog.CurrentPath = OS.GetUserDataDir() + "/";

        if (filters != null)
        {
            foreach (var filter in filters)
            {
                dialog.AddFilter(filter);
            }
        }

        dialog.FileSelected += (path) =>
        {
            GD.Print($"内置对话框保存路径: {path}");
            onFileSelected?.Invoke(path);
            dialog.QueueFree();
        };

        dialog.Canceled += () =>
        {
            GD.Print("用户取消了保存");
            onFileSelected?.Invoke(null);
            dialog.QueueFree();
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 700));
    }
    
    // public void SaveFile(Action<string> onFileSelected, string[] filters)
    // {
    //     DisplayServer.FileDialogShow(
    //     "选择一个文件",
    //     OS.GetSystemDir(OS.SystemDir.Documents),
    //     "",
    //     false,
    //     DisplayServer.FileDialogMode.SaveFile,
    //     filters:filters,
    //     Callable.From((bool status, string[] paths, int filterIndex) =>
    //     {
    //         if (status && paths.Length > 0)
    //         {
    //             string filePath = paths[0];
    //             GD.Print($"原生对话框选择的文件: {filePath}");
    //             onFileSelected?.Invoke(filePath); // 调用回调
    //         }
    //         else
    //         {
    //             GD.Print("用户取消了选择或发生错误");
    //             onFileSelected?.Invoke(null); // 传递 null 表示取消
    //         }
    //     }));
    // }

    
    
    // // 回调函数接收选中状态、路径和索引
    // private void OnNativeDialogCallback(bool status, string[] selectedPaths, int filterIndex)
    // {
    //     if (status && selectedPaths.Length > 0)
    //     {
    //         string filePath = selectedPaths[0];
    //         GD.Print($"原生对话框选择的文件: {filePath}");
    //     }
    //     else
    //     {
    //         GD.Print("用户取消了选择或发生错误");
    //     }
    // }
}
