using Godot;
using System;
using System.IO;

public partial class StartMenu : Node
{
    private ChartService _chartService;
    [Export] private ChartList _chartList;
    [Export] private CreateChartPanel _createPanel;
    [Export] private DeletePanel _deletePanel;

    private string _currentSelectedChartId;

    public override void _Ready()
    {
        _chartService = GetNode<ChartService>("/root/ChartService");

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
        var charts = _chartService.GetAllCharts(); // 需要实现
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
