using Godot;
using System;

/// <summary>
/// 与谱面数据有关的事件总线
/// </summary>
public static class ChartEventBus
{
    public static event Action OnChartDataChanged;

    public static void NotifyDataChanged()
    {
        OnChartDataChanged?.Invoke();
    }
}
