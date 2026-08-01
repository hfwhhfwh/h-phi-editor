using Godot;
using System;

public class BoxSelectController
{
    private Vector2 startPos,endPos; // (chartPosX, beatValue)

    public bool IsDragging { get; private set; }

    /// <summary>
    /// 事件：框选更新时触发（用于重绘），参数为 (起点, 终点) 坐标系：(chartPosX, beatValue)
    /// </summary>
    public event Action<Vector2, Vector2> BoxUpdated;
    /// <summary>
    /// 事件：框选结束时触发（用于执行操作），参数为 (起点, 终点) 坐标系：(chartPosX, beatValue)
    /// </summary>
    public event Action<Vector2, Vector2> BoxEnded;

    public void StartDrag(Vector2 pos)
    {
        startPos = pos;
        endPos = pos;
        IsDragging = true;
        BoxUpdated?.Invoke(startPos, endPos);
    }

    public void Move(Vector2 pos)
    {
        endPos = pos;
        BoxUpdated?.Invoke(startPos, endPos);
    }

    public void EndDrag(Vector2 pos)
    {
        BoxEnded?.Invoke(startPos, pos);
        IsDragging = false;
    }

    


}
