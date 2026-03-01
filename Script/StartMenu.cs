using Godot;
using System;
using System.IO;

public partial class StartMenu : Node
{
    private FileDialogManager fileDialogManager;
    private CreateChartPanel createChartPanel;
    private DeletePanel deletePanel;
    private ChartList chartList;

    //当前选择的铺面在列表中的id
    private int choosedChartId;

    public override void _Ready()
    {
        base._Ready();
        //文件窗口
        fileDialogManager = GetNode<FileDialogManager>("/root/FileDialogManager");
        //新建谱面的面板
        createChartPanel = GetNode<CreateChartPanel>("PanelContainer/SafeArea/CreateChartPanel");
        createChartPanel.Visible = false;
        //删除谱面的面板
        deletePanel = GetNode<DeletePanel>("PanelContainer/SafeArea/DeletePanel");
        deletePanel.Visible = false;
        //谱面列表
        chartList = GetNode<ChartList>("PanelContainer/SafeArea/VBoxContainer/HBoxContainer/ScrollContainer/ChartList");
        chartList.OptionSelected += OnOptionSelected;
    }

    public void OnOptionSelected(int id)
    {
        choosedChartId = id;
    }

    

    public void OnCreateButtonPressed()
    {
        //弹出窗口
        createChartPanel.Visible = true;
    }

    public void OnDeleteButtonPressed()
    {
        //弹出窗口
        deletePanel.Visible = true;
        //传入选中谱面的信息
        deletePanel.SetInfo(choosedChartId);
        //TODO

    }
}
