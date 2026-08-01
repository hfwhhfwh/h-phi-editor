using Godot;
using QuickType;
using System;

public class DragPlaceComponent
{
    public Beat startBeat, endBeat;
    public int verLineIndex;
    public bool IsDragging { get; private set; }

    /// <summary>
    /// 事件：拖拽更新时触发（用于重绘），参数为 (起点, 终点) 元组：(竖线索引, Beat)
    /// </summary>
    // public event Action<ValueTuple<int, Beat>, ValueTuple<int, Beat>> DragUpdated;

    /// <summary>
    /// 事件：框选结束时触发（用于执行操作），参数为 (起点, 终点) 元组：(竖线索引, Beat)
    /// </summary>
    public event Action<ValueTuple<int, Beat>, ValueTuple<int, Beat>> DragEnded;

    public void StartDrag(int verLineIndex, Beat beat)
    {
        this.verLineIndex = verLineIndex;
        startBeat = beat;
        endBeat = beat;
        IsDragging = true;
        //DragUpdated?.Invoke(new (verLineIndex, beat), new (verLineIndex, beat));
    }

    public void Move(int verLineIndex, Beat beat)
    {
        this.verLineIndex = verLineIndex;
        endBeat = beat;
    }

    public void EndDrag(int verLineIndex, Beat beat)
    {
        DragEnded?.Invoke(new (verLineIndex, beat), new (verLineIndex, beat));
        IsDragging = false;
    }
}
