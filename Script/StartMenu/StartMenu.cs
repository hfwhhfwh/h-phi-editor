using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class StartMenu : Node
{
    private ChartService _chartService;
    [Export] private ChartList _chartList;
    [Export] private CreateChartPanel _createPanel;
    [Export] private DeletePanel _deletePanel;
    [Export] private InfoPanel _infoPanel;
    [Export] private ExportPanel _exportPanel;
    [Export] private SettingsPanel _settingsPanel;
    [Export] private Button _multiSelectBtn;

    private FileDialogManager fileDialogManager;

    private string _currentSelectedChartId;

    [Export] private Label deviceLabel;
    [Export] private Label versionLabel;

    public override void _Ready()
    {
        _chartService = GetNode<ChartService>("/root/ChartService");
        fileDialogManager = GetNode<FileDialogManager>("/root/FileDialogManager");

        // 连接信号
        _chartList.ChartSelected += OnChartSelected;

        _createPanel.ChartCreated += CreateChart;
        _createPanel.Cancelled += () => _createPanel.Visible = false;

        _deletePanel.DeleteConfirmed += OnDeleteConfirmed;
        _deletePanel.Cancelled += () => _deletePanel.Visible = false;

        _infoPanel.Confirmed += OnInfoEdited;
        _infoPanel.Cancelled += () => _infoPanel.Visible = false;

        _exportPanel.Confirmed += OnExportConfirmed;
        _exportPanel.Cancelled += () => _exportPanel.Visible = false;

        _multiSelectBtn.ToggleMode = true;
        _multiSelectBtn.Toggled += OnSelectButtonToggled;

        // 初始化列表
        RefreshChartList();

        // 显示运行平台
        if (OS.HasFeature("ios"))
        {
            deviceLabel.Text = "运行平台: ios";
        }
        else if (OS.GetCmdlineUserArgs().Contains("simulate_ios"))
        {
            deviceLabel.Text = "运行平台: simulate_ios";
        }
        else if (OS.HasFeature("android"))
        {
            deviceLabel.Text = "运行平台: Android";
        }
        else
        {
            deviceLabel.Text = "运行平台: ";
        }

        // 显示版本号
        versionLabel.Text = $"版本: {GameVersion.Version}";
    }

    private void RefreshChartList()
    {
        List<ChartInfo> chartInfos = _chartService.GetAllCharts();

        // 生成数据
        List<ChartList.Data> datas = new();
        foreach(ChartInfo info in chartInfos)
        {
            // 加载曲绘图片
            Texture2D texture = FileUtil.LoadTextureFromFile(info.PicturePath, out string realFormat);

            if(!string.IsNullOrEmpty(realFormat) 
                && realFormat != "unknown" 
                && realFormat != info.PictureFileName.GetExtension())
            {
                // 后缀名有误，这里直接修改为正确的
                // 1. 替换图片文件
                string oPath = info.PicturePath;
                string dPath = Path.ChangeExtension(oPath, realFormat);
                //复制一份正确的文件，同时修改后缀名
                FileUtil.CopyFile(oPath, dPath);
                // 删除原来的错误文件
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(oPath));

                // 2. 修改info文件
                ChartInfo newInfo = info.Duplicate();
                newInfo.PictureFileName = dPath.GetFile();
                _chartService.SaveChartInfo(newInfo.Id, newInfo);

                FileLogger.Log($"[{Name}] 成功修正了曲绘文件格式:{oPath} -> {dPath}");
            }

            datas.Add(new ChartList.Data
            {
                ChartId = info.Id,
                ChartName = info.Name,
                Composer = info.Composer,
                Picture = texture,
            });
        }

        _chartList.SetCharts(datas);
    }

    private void OnChartSelected(string chartId)
    {
        _currentSelectedChartId = chartId;
    }

    public void OnCreateButtonPressed()
    {
        _createPanel.Visible = true;
    }

    public void OnImportButtonPressed()
    {
        string[] filters = {"*.*;所有文件;"};
        fileDialogManager.ShowOpenDialog(
            async (string[] paths) =>
            {
                if (paths == null || paths.Length == 0)
                {
                    return;
                }

                int successCount = 0;
                int unreportedFailureCount = 0;
                foreach(string path in paths)
                {
                    if(string.IsNullOrEmpty(path)) continue;

                    try
                    {
                        ChartImportResult result = await _chartService.ImportChartAsync(path);
                        if (result == ChartImportResult.Succeeded)
                        {
                            successCount++;
                        }
                        else if (result == ChartImportResult.Failed)
                        {
                            unreportedFailureCount++;
                        }
                    }
                    catch(Exception e)
                    {
                        FileLogger.LogErr($"[{Name}] 谱面无法正常导入，路径:{path}, 错误:{e.Message}");
                        unreportedFailureCount++;
                    }
                }

                ImageBlur.Instance.RemoveViewports(); // 节省渲染性能

                RefreshChartList();

                if(paths.Length == 1)
                {
                    string message = successCount > 0
                        ? "导入成功"
                        : "导入失败";
                    ToastManager.Instance?.ShowToast(
                        message,
                        successCount > 0 ? ToastColorPreset.OK : ToastColorPreset.Error);
                }
                else
                {
                    if (unreportedFailureCount > 0)
                    {
                        string message = successCount > 0
                            ? $"导入完成：成功 {successCount} 个，另有 {unreportedFailureCount} 个导入失败"
                            : $"{unreportedFailureCount} 个谱面导入失败";
                        ToastManager.Instance?.ShowToast(
                            message,
                            successCount > 0 ? ToastColorPreset.Warning : ToastColorPreset.Error);
                    }
                    else if (successCount > 0)
                    {
                        ToastManager.Instance?.ShowToast($"成功导入 {successCount} 个谱面");
                    }
                }
                
            },
            filters,
            multi: true
        );
    }

    // private void CreateChart(ChartInfo data, string songPath, string picPath)
    // {
    //     ImageTexture pic = FileUtil.LoadTextureFromFile(picPath, out string _);

    //     CreateChart(data, songPath, pic);
    // }

    private void CreateChart(ChartInfo data, string songPath, Texture2D texture)
    {
        _chartService.CreateNewChart(data, songPath, texture);
        RefreshChartList();
        _createPanel.Visible = false;
    }

    public void OnEditInfoPressed()
    {
        if (!string.IsNullOrEmpty(_currentSelectedChartId))
        {
            //获取谱面信息ChartInfo
            ChartInfo chartInfo = _chartService.GetChartInfo(_currentSelectedChartId);
            if(chartInfo == null)
            {
                FileLogger.LogErr($"{this.Name} OnEditInfoPressed() chartInfo == null");
                return;
            }
            //显示基本信息面板
            _infoPanel.Visible = true;
            _infoPanel.SetInfo(chartInfo);

        }
    }

    private void OnInfoEdited(ChartInfo data, string newSongPath, string newPicPath)
    {
        //修改曲绘和音乐 为空表示没有修改
        if(!string.IsNullOrEmpty(newPicPath))
        {
            _chartService.SetChartPic(_currentSelectedChartId, newPicPath);
        }
        if(!string.IsNullOrEmpty(newSongPath))
        {
            _chartService.SetChartSong(_currentSelectedChartId, newSongPath);
        }

        //修改info文件
        _chartService.SaveChartInfo(_currentSelectedChartId, data);
        

        RefreshChartList();
        _infoPanel.Visible = false;
    }

    public void OnDeleteButtonPressed()
    {
        if (_chartList.MultiSelectMode)
        {
            if(_chartList.SelectedIds.Count() == 0) return;

            PopupHelper.Instance.ShowConfirm("提示", $"是否要删除{_chartList.SelectedIds.Count()}个谱面",
            onConfirm: () =>
            {
                int count = _chartList.SelectedIds.Count();
                foreach(string id in _chartList.SelectedIds)
                {
                    _chartService.DeleteChart(id);
                }

                RefreshChartList();
                _chartList.DeselectAll();

                ToastManager.Instance.ShowToast($"成功删除{count}张谱面", ToastColorPreset.OK);
            });
        }
        else
        {
            if (string.IsNullOrEmpty(_currentSelectedChartId)) return;

            //获取谱面信息ChartInfo
            ChartInfo chartInfo = _chartService.GetChartInfo(_currentSelectedChartId);
            if (chartInfo == null)
            {
                FileLogger.LogErr($"{this.Name} OnDeleteButtonPressed() chartInfo == null");
                return;
            }

            //显示删除面板
            _deletePanel.Visible = true;
            _deletePanel.SetInfo(chartInfo);
        }


    }

    private void OnDeleteConfirmed(string chartId)
    {
        _chartService.DeleteChart(chartId);
        RefreshChartList();
        _deletePanel.Visible = false;

        ToastManager.Instance.ShowToast($"成功删除谱面", ToastColorPreset.OK);
    }

    public void OnExportPressed()
    {
        if (_chartList.MultiSelectMode)
        {
            List<string> selectedChartIds = _chartList.SelectedIds.ToList();
            if (selectedChartIds.Count == 0)
            {
                // PopupHelper.Instance.ShowAlert("提示", "请先选择要导出的谱面");
                ToastManager.Instance.ShowToast("请选择要导出的谱面", ToastColorPreset.Warning);
                return;
            }

            fileDialogManager.ShowSelectDirectoryDialog((string dirPath) =>
            {
                if (string.IsNullOrWhiteSpace(dirPath)) return;

                var result = _chartService.ExportCharts(selectedChartIds, dirPath);

                string title;
                ToastColorPreset toastColorPreset;
                
                title = result.FailureCount == 0 ? "导出全部完成" : $"导出成功{result.SuccessCount}张，失败{result.FailureCount}张";
                toastColorPreset = result.FailureCount == 0 ? ToastColorPreset.OK : ToastColorPreset.Warning;

                ToastManager.Instance.ShowToast(title, toastColorPreset);

                // PopupHelper.Instance.ShowAlert(
                //     result.FailureCount == 0 ? "导出完成" : "导出部分完成",
                //     $"成功：{result.SuccessCount} 张\n失败：{result.FailureCount} 张");
            });
            return;
        }

        if (!string.IsNullOrEmpty(_currentSelectedChartId))
        {
            //获取谱面信息ChartInfo
            ChartInfo chartInfo = _chartService.GetChartInfo(_currentSelectedChartId);
            if(chartInfo == null)
            {
                FileLogger.LogErr($"[{this.Name}] OnExportPressed() chartInfo == null");
                return;
            }
            //显示基本信息面板
            _exportPanel.Visible = true;
            _exportPanel.SetInfo(chartInfo);

        }
    }

    private void OnExportConfirmed(string chartId)
    {
        FileLogger.Log($"[{this.Name}] OnExportConfirmed(), chartID:{chartId}");

        _chartService.ExportChart(chartId, succeeded =>
        {
            ToastManager.Instance?.ShowToast(
                succeeded ? "谱面导出成功" : "谱面导出失败",
                succeeded ? ToastColorPreset.OK : ToastColorPreset.Error);
        });

        _exportPanel.Visible = false;
    }

    public void OnSettingsPressed()
    {
        _settingsPanel.Visible = true;
    }

    //打开谱面，进入编辑界面
    public void OnOpenPressed()
    {
        if (string.IsNullOrEmpty(_currentSelectedChartId))
        {
            FileLogger.Log($"[{Name}] 未选中任何谱面");
            return;
        }
        
        var global = GetNode<Global>("/root/Global");
        global.editingChartId = _currentSelectedChartId;
        global.GotoScene("res://Scene/editor_scene.tscn");
    }

    public void OnSelectButtonToggled(bool toggledOn)
    {
        _chartList.MultiSelectMode = toggledOn;

        FileLogger.Log($"[{Name}] 切换到 {(toggledOn ? "多选" : "单选")} 模式");
    }
}
