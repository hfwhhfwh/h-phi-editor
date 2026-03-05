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
        //_deletePanel.DeleteConfirmed += OnDeleteConfirmed;
        //_deletePanel.Cancelled += () => _deletePanel.Visible = false;

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

    public void OnDeleteButtonPressed()
    {
        if (!string.IsNullOrEmpty(_currentSelectedChartId))
        {
            //_deletePanel.SetChartId(_currentSelectedChartId);
            _deletePanel.Visible = true;
        }
    }

    private void OnDeleteConfirmed(string chartId)
    {
        _chartService.DeleteChart(chartId);
        RefreshChartList();
        _deletePanel.Visible = false;
    }
}
