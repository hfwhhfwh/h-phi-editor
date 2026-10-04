using Godot;
using QuickType;
using System;

/// <summary>
/// 编辑器场景级状态中心。
///
/// 设计约定：
/// 1. 这里是「当前正在编辑什么」以及「视图/时间状态」的唯一来源，EditorScene 与各面板都不再各自持有副本；
/// 2. EditorContext 只保存状态并发出来变更事件，不包含任何编辑逻辑；
/// 3. 对 Chart 的任何修改都必须通过 ChartEditService（面板直接持有它的引用）。
/// </summary>
public partial class EditorContext : Node
{
    // ==================== 谱面数据 ====================

    /// <summary>正在编辑的谱面 ID</summary>
    public string EditingChartId { get; private set; }

    /// <summary>正在编辑的谱面</summary>
    public Chart EditingChart { get; private set; }

    /// <summary>谱面是否已经加载完成</summary>
    public bool IsChartLoaded => EditingChart != null && EditingChart.JudgeLineList != null;

    private int _editingLineId;
    /// <summary>正在编辑的判定线编号</summary>
    public int EditingLineId
    {
        get => _editingLineId;
        set
        {
            if (_editingLineId == value) return;
            _editingLineId = value;
            EditingLineChanged?.Invoke(value);
        }
    }

    private int _editingLayer;
    /// <summary>正在编辑的事件层（0~4）</summary>
    public int EditingLayer
    {
        get => _editingLayer;
        set
        {
            if (_editingLayer == value) return;
            _editingLayer = value;
            EditingLayerChanged?.Invoke(value);
        }
    }

    // ==================== 视图状态 ====================

    /// <summary>竖直滚动量（像素）</summary>
    public float HorOffset { get; set; }

    /// <summary>平滑后的竖直滚动量，供面板渲染使用</summary>
    public float HorOffsetSmoothed { get; set; }

    /// <summary>竖直缩放（一个 beat 对应多少像素）</summary>
    public float HorSeparation { get; set; } = 100f;

    /// <summary>平滑后的竖直缩放，供面板渲染使用</summary>
    public float HorSeparationSmoothed { get; set; } = 100f;

    /// <summary>当前时间点在编辑面板上的 Y 坐标（向下偏移）</summary>
    public float GroundY { get; set; } = 450f;

    // ==================== 时间状态 ====================

    private float _beatValue;
    private double _chartTime;

    /// <summary>谱面当前所在的 beat</summary>
    public float BeatValue
    {
        get => _beatValue;
        set
        {
            // 谱面还没加载完成时，先只更新 beat 偏移，等加载完再由 EditorScene 同步时间
            if (EditingChart?.BpmList == null)
                return;

            _beatValue = value;
            _chartTime = TimeUtil.BeatToSecond(_beatValue, EditingChart.BpmList);
        }
    }

    /// <summary>谱面当前时间（秒）</summary>
    public double ChartTime
    {
        get => _chartTime;
        set
        {
            // 谱面还没加载完成时，先只更新 beat 偏移，等加载完再由 EditorScene 同步时间
            if (EditingChart?.BpmList == null)
                return;

            _chartTime = value;
            _beatValue = TimeUtil.SecondToBeat((float)_chartTime, EditingChart.BpmList);
            HorOffset = _beatValue * HorSeparation;
        }
    }

    // ==================== 面板共享的输入状态 ====================

    /// <summary>选择模式（单选/多选），由工具栏按钮统一设置，所有面板共享</summary>
    public BaseEditPanel.SelectModeEnum SelectMode { get; set; } = BaseEditPanel.SelectModeEnum.Single;

    /// <summary>是否处于框选模式，所有面板共享</summary>
    public bool IsBoxSelectMode { get; set; }

    // ==================== 变更事件 ====================

    /// <summary>谱面加载完成（或切换谱面）时触发</summary>
    public event Action<Chart> ChartLoaded;

    /// <summary>正在编辑的判定线发生变化时触发</summary>
    public event Action<int> EditingLineChanged;

    /// <summary>正在编辑的事件层发生变化时触发</summary>
    public event Action<int> EditingLayerChanged;

    /// <summary>
    /// 设置当前正在编辑的谱面。切换谱面会重置判定线、事件层与时间状态。
    /// </summary>
    public void SetChart(string chartId, Chart chart)
    {
        EditingChartId = chartId;
        EditingChart = chart;

        _editingLineId = 0;
        _editingLayer = 0;
        _beatValue = 0;
        _chartTime = 0;
        HorOffset = 0;

        ChartLoaded?.Invoke(chart);
    }
}
