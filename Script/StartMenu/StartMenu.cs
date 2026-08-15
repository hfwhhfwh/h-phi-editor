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

        _createPanel.ChartCreated += OnChartCreated;
        _createPanel.Cancelled += () => _createPanel.Visible = false;

        _deletePanel.DeleteConfirmed += OnDeleteConfirmed;
        _deletePanel.Cancelled += () => _deletePanel.Visible = false;

        _infoPanel.Confirmed += OnInfoEdited;
        _infoPanel.Cancelled += () => _infoPanel.Visible = false;

        _exportPanel.Confirmed += OnExportConfirmed;
        _exportPanel.Cancelled += () => _exportPanel.Visible = false;

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

                // 2. 修改info.txt
                ChartInfo newInfo = info.Duplicate();
                newInfo.PictureFileName = dPath.GetFile();
                _chartService.SaveChartInfo(newInfo);

                GD.Print($"[{Name}] 成功修正了曲绘文件格式:{oPath} -> {dPath}");
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
            (path) =>
            {
                if(string.IsNullOrEmpty(path)) return;

                _chartService.ImportChart(path);
                RefreshChartList();
            },
            filters
        );
    }

    private void OnChartCreated(ChartInfo data, string songPath, string picPath)
    {
        _chartService.CreateNewChart(data, songPath, picPath);
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
                GD.PrintErr($"{this.Name} OnEditInfoPressed() chartInfo == null");
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

        //修改info.txt
        _chartService.SetChartInfo(_currentSelectedChartId, data);
        

        RefreshChartList();
        _infoPanel.Visible = false;
    }

    public void OnDeleteButtonPressed()
    {
        if (!string.IsNullOrEmpty(_currentSelectedChartId))
        {
            //获取谱面信息ChartInfo
            ChartInfo chartInfo = _chartService.GetChartInfo(_currentSelectedChartId);
            if(chartInfo == null)
            {
                GD.PrintErr($"{this.Name} OnDeleteButtonPressed() chartInfo == null");
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
    }

    public void OnExportPressed()
    {
        if (!string.IsNullOrEmpty(_currentSelectedChartId))
        {
            //获取谱面信息ChartInfo
            ChartInfo chartInfo = _chartService.GetChartInfo(_currentSelectedChartId);
            if(chartInfo == null)
            {
                GD.PrintErr($"[{this.Name}] OnExportPressed() chartInfo == null");
                return;
            }
            //显示基本信息面板
            _exportPanel.Visible = true;
            _exportPanel.SetInfo(chartInfo);

        }
    }

    private void OnExportConfirmed(string chartId)
    {
        GD.Print($"[{this.Name}] OnExportConfirmed(), chartID:{chartId}");

        _chartService.ExportChart(chartId);

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
            GD.Print($"[{Name}] 未选中任何谱面");
            return;
        }
        
        var global = GetNode<Global>("/root/Global");
        global.editingChartId = _currentSelectedChartId;
        global.GotoScene("res://Scene/editor_scene.tscn");
    }
}
