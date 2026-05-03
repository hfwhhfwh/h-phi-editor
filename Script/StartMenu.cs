using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class StartMenu : Node
{
    private ChartService _chartService;
    [Export] private ChartList _chartList;
    [Export] private CreateChartPanel _createPanel;
    [Export] private DeletePanel _deletePanel;
    [Export] private InfoPanel _infoPanel;
    [Export] private ExportPanel _exportPanel;

    private FileDialogManager fileDialogManager;

    private string _currentSelectedChartId;

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
    }

    private void RefreshChartList()
    {
        List<ChartInfo> charts = _chartService.GetAllCharts();
        _chartList.SetCharts(charts);
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
        fileDialogManager.ShowNativeOpenDialog(
            (path) =>
            {
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
        //修改info.txt
        _chartService.SetChartInfo(_currentSelectedChartId, data);

        //修改曲绘和音乐 为空表示没有修改
        if(!string.IsNullOrEmpty(newPicPath))
        {
            _chartService.SetChartPic(_currentSelectedChartId, newPicPath);
        }
        if(!string.IsNullOrEmpty(newSongPath))
        {
            _chartService.SetChartSong(_currentSelectedChartId, newSongPath);
        }
        

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
}
