using Godot;
using System;

/// <summary>
/// 与谱面数据有关的事件总线
/// </summary>
public static class ChartEventBus
{
    // /// <summary>
    // /// 当添加/删除判定线或音符时触发
    // /// </summary>
    // public static event Action ChartStructureChanged;

    // public static void NotifyStructureChanged()
    // {
    //     ChartStructureChanged?.Invoke();
    // }

    /// <summary>
    /// 某一条判定线中添加/删除note时触发
    /// </summary>
    public static event Action<int> NoteCountChanged;

    public static void NotifyNoteCountChanged(int lineId)
    {
        NoteCountChanged?.Invoke(lineId);
    } 

    /// <summary>
    /// 判定线数量改变时触发
    /// </summary>
    public static event Action LineCountChanged;
    public static void NotifyLineCountChanged()
    {
        LineCountChanged?.Invoke();
    }

    public static event Action<int, int> LineFatherChanged;
    public static void NotifyLineFatherChanged(int lineId, int father)
    {
        LineFatherChanged?.Invoke(lineId, father);
    }
}
