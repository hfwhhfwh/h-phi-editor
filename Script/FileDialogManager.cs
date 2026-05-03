using Godot;
using System;

public partial class FileDialogManager : Node
{   
    /// <summary>
    /// 打开原生文件窗口，选择一个文件
    /// </summary>
    /// <param name="onFileSelected">回调参数，当文件选择完成后调用该回调</param>
    public void ShowNativeOpenDialog(Action<string> onFileSelected, string[] filters)
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
        }));
    }

    public void ShowNativeOpenDialog(Action<string> onFileSelected)
    {
        string[] filters = {"*.*"};
        ShowNativeOpenDialog(onFileSelected, filters);
    }

    
    public void SaveFile(Action<string> onFileSelected, string[] filters)
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
        }));
    }

    
    
    // 回调函数接收选中状态、路径和索引
    private void OnNativeDialogCallback(bool status, string[] selectedPaths, int filterIndex)
    {
        if (status && selectedPaths.Length > 0)
        {
            string filePath = selectedPaths[0];
            GD.Print($"原生对话框选择的文件: {filePath}");
        }
        else
        {
            GD.Print("用户取消了选择或发生错误");
        }
    }
}
