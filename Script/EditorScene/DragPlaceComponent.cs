using Godot;
using QuickType;
using System;

public class DragPlaceComponent
{
    /// <summary>用户开始拖动的beat</summary>
    public Beat beat1;
    /// <summary>用户结束拖动的beat</summary>
    public Beat beat2;

    public Beat StartBeat => beat1 < beat2 ? beat1 : beat2;
    public Beat EndBeat => beat1 < beat2 ? beat2 : beat1;

    public int verLineIndex;
    public bool IsDragging { get; private set; }

    /// <summary>
    /// 事件：拖拽更新时触发（用于重绘），参数为 (起点, 终点) 元组：(竖线索引, Beat)
    /// </summary>
    // public event Action<ValueTuple<int, Beat>, ValueTuple<int, Beat>> DragUpdated;

    /// <summary>
    /// 事件：框选结束时触发（用于执行操作），参数为 (竖线索引, 起始Beat, 结束Beat) 
    /// </summary>
    public event Action<int, Beat, Beat> DragEnded;

    public void StartDrag(int verLineIndex, Beat beat)
    {
        this.verLineIndex = verLineIndex;
        beat1 = beat;
        beat2 = beat;
        IsDragging = true;
        //DragUpdated?.Invoke(new (verLineIndex, beat), new (verLineIndex, beat));
    }

    public void Move(int verLineIndex, Beat beat)
    {
        this.verLineIndex = verLineIndex;
        beat2 = beat;
    }

    public void EndDrag(int verLineIndex, Beat beat2)
    {
        this.beat2 = beat2;
        DragEnded?.Invoke(verLineIndex, StartBeat, EndBeat);
        IsDragging = false;
    }
}
