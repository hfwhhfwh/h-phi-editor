using Godot;
using QuickType;
using System;
using System.Collections.Generic;

/// <summary>
/// 判定线与事件层控制器。
///
/// 职责：
/// 1. 「选择判定线」面板的显隐、内容刷新；
/// 2. 切换正在编辑的判定线与事件层；
/// 3. 判定线的新增 / 删除（实际修改通过 ChartEditService）。
/// </summary>
public partial class EditorJudgeLineController : Node
{
    private EditorContext _context;
    private ChartEditService _editService;
    private InputManager _inputManager;
    private ChooseLinePanel _chooseLinePanel;

    private bool _subscribed;

    public void Initialize(
        EditorContext context,
        ChartEditService editService,
        InputManager inputManager,
        ChooseLinePanel chooseLinePanel)
    {
        _context = context;
        _editService = editService;
        _inputManager = inputManager;
        _chooseLinePanel = chooseLinePanel;

        if (_chooseLinePanel != null)
        {
            _chooseLinePanel.Visible = false;
            _chooseLinePanel.LineSelected += OnLineSelected;
            _chooseLinePanel.AddLineRequested += OnAddLineRequested;
            _chooseLinePanel.DeleteLineRequested += OnDeleteLineRequested;
            _chooseLinePanel.RefreshRequested += RefreshChooseLinePanel;
            _chooseLinePanel.LayerSelected += OnLayerSelected;
        }

        _subscribed = true;
    }

    public override void _ExitTree()
    {
        if (_subscribed && _chooseLinePanel != null)
        {
            _chooseLinePanel.LineSelected -= OnLineSelected;
            _chooseLinePanel.AddLineRequested -= OnAddLineRequested;
            _chooseLinePanel.DeleteLineRequested -= OnDeleteLineRequested;
            _chooseLinePanel.RefreshRequested -= RefreshChooseLinePanel;
            _chooseLinePanel.LayerSelected -= OnLayerSelected;
        }

        _subscribed = false;

        base._ExitTree();
    }

    /// <summary>
    /// 由「选择判定线」按钮触发：切换面板显隐。
    /// 面板打开时禁用 InputManager，避免滚轮同时滚动编辑面板。
    /// </summary>
    public void ToggleChooseLinePanel()
    {
        if (_chooseLinePanel == null) return;

        if (!_chooseLinePanel.Visible)
        {
            _chooseLinePanel.Visible = true;
            if (_inputManager != null) _inputManager.IsEnable = false;

            RefreshChooseLinePanel();
            _chooseLinePanel.SetEventLayer(_context.EditingLayer);
        }
        else
        {
            _chooseLinePanel.Visible = false;
            if (_inputManager != null) _inputManager.IsEnable = true;
        }
    }

    /// <summary>
    /// 重新构建判定线列表
    /// </summary>
    public void RefreshChooseLinePanel()
    {
        if (_chooseLinePanel == null) return;

        Chart chart = _context?.EditingChart;
        if (chart?.JudgeLineList == null) return;

        List<ChooseLinePanel.LineInfo> lineInfos = new();
        for (int i = 0; i < chart.JudgeLineList.Count; i++)
        {
            JudgeLine line = chart.JudgeLineList[i];

            lineInfos.Add(new ChooseLinePanel.LineInfo
            {
                Id = i, // 判定线的编号从0开始
                NoteCount = line.NumOfNotes,
                //NextEventTime = //TODO 在ChooseLinePanel显示下一个事件的时间
            });
        }

        _chooseLinePanel.ShowInfos(lineInfos);
    }

    private void OnLineSelected(int id)
    {
        GD.Print($"[{Name}] 用户选择了Line:{id}");

        // 判定线只写入 Context，各面板通过 Context 读取；标签由 EditingLineChanged 统一更新
        _context.EditingLineId = id;

        _chooseLinePanel.Visible = false;
        if (_inputManager != null) _inputManager.IsEnable = true;
    }

    private void OnAddLineRequested()
    {
        Chart chart = _context?.EditingChart;
        if (chart?.JudgeLineList == null) return;

        _editService.AddLine(chart.JudgeLineList, -1);
    }

    private void OnDeleteLineRequested(int id)
    {
        Chart chart = _context?.EditingChart;
        if (chart?.JudgeLineList == null) return;

        if (chart.JudgeLineList.Count <= 1)
        {
            GD.Print($"[{Name}] 最少保留一条判定线，删除失败");
            PopupHelper.Instance.ShowAlert("警告", "最少保留一条判定线，删除失败");
            return;
        }

        _editService.DeleteLine(chart.JudgeLineList, id);
    }

    private void OnLayerSelected(int index)
    {
        if (index < 0 || index > 4)
        {
            GD.PrintErr($"[{Name}] EventLayer索引越界:{index}");
            return;
        }

        Chart chart = _context?.EditingChart;
        if (chart?.JudgeLineList == null) return;

        List<EventLayer> eventLayers = chart.JudgeLineList[_context.EditingLineId].EventLayers;

        // 如果列表元素不够，用 null 填充到目标索引
        while (eventLayers.Count <= index)
        {
            eventLayers.Add(null);
        }

        if (eventLayers[index] == null)
        {
            eventLayers[index] = new();
        }

        _context.EditingLayer = index;

        GD.Print($"[{Name}] 切换到事件层:{index}");
    }
}
