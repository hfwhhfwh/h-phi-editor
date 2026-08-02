using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public static class RectUtil
{
    /// <summary>
    /// 根据两个点获得Rect2，这两个点的相对位置关系随意
    /// </summary>
    /// <param name="pos1"></param>
    /// <param name="pos2"></param>
    /// <returns></returns>
    public static Rect2 TwoPointsToRect(Vector2 pos1, Vector2 pos2)
    {
        Vector2 pos = new Vector2(
            Mathf.Min(pos1.X, pos2.X),
            Mathf.Min(pos1.Y, pos2.Y)
        );
        Vector2 size = new Vector2(
            Mathf.Abs(pos1.X - pos2.X),
            Mathf.Abs(pos1.Y - pos2.Y)
        );

        return new Rect2(pos, size); 
    }

    /// <summary>
    /// 判断note是否在一个矩形框内（坐标系：(ChartPosX, BeatValue)）
    /// </summary>
    /// <param name="note">note的原始数据（坐标系：(ChartPosX, BeatValue)）</param>
    /// <param name="rect">矩形框（坐标系：(ChartPosX, BeatValue)）</param>
    /// <returns>判断结果bool</returns>
    public static bool IsNoteInRect(Note note, Rect2 rect)
    {
        if(note.Type != 2)
        {
            float beatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
            Vector2 noteDataPos = new Vector2(note.PositionX, beatValue);
            return rect.HasPoint(noteDataPos);
        }
        else
        {
            float startBeatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
            float endBeatValue = note.EndTime[0] + note.EndTime[1] * 1f / note.EndTime[2];
            float posX = note.PositionX;
            
            // TODO Hold的框选可以选择多种模式，包括部分包含、完全包含、头部选中
            // 这里暂时用部分包含
            
            // 1. X 在矩形范围内
            bool xInRange = posX >= rect.Position.X && posX <= rect.End.X;
            // 2. Y 轴有重叠（矩形与 Hold 区间相交）
            bool yOverlap = !(rect.Position.Y > endBeatValue || rect.End.Y < startBeatValue);

            // GD.Print($"xInRange:{xInRange}, yOverlap:{yOverlap}");

            return xInRange && yOverlap;
        }
    }
    
    /// <summary>
    /// 获得矩形内的所有note（坐标系：(ChartPosX, BeatValue)）
    /// </summary>
    /// <param name="notes">note列表</param>
    /// <param name="rect">矩形（坐标系：(ChartPosX, BeatValue)）</param>
    /// <returns></returns>
    public static List<int> GetNotesInRect(List<Note> notes, Rect2 rect)
    {
        List<int> notesInRect = new();
        for (int i = 0; i < notes.Count; i++)
        {
            Note note = notes[i];
            if (RectUtil.IsNoteInRect(note, rect))
            {
                notesInRect.Add(i);
            }
        }

        return notesInRect;
    }

    public static bool IsEventInRect(LineEvent lineEvent, float chartPosX, Rect2 rect)
    {
        float startBeatValue = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
        float endBeatValue = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
        
        // TODO Event的框选可以选择多种模式，包括部分包含、完全包含、头部选中
        // 这里暂时用部分包含
        
        // 1. X 在矩形范围内
        bool xInRange = chartPosX >= rect.Position.X && chartPosX <= rect.End.X;
        // 2. Y 轴有重叠（矩形与 Hold 区间相交）
        bool yOverlap = !(rect.Position.Y > endBeatValue || rect.End.Y < startBeatValue);

        // GD.Print($"xInRange:{xInRange}, yOverlap:{yOverlap}");

        return xInRange && yOverlap;
    }

    public static List<ValueTuple<float, LineEvent>> GetEventsInRect(List<ValueTuple<float, LineEvent>> lineEvents, Rect2 rect)
    {
        List<ValueTuple<float, LineEvent>> eventsInRect = new();
        for (int i = 0; i < lineEvents.Count; i++)
        {
            ValueTuple<float, LineEvent> lineEvent = lineEvents[i];
            if (IsEventInRect(lineEvent.Item2, lineEvent.Item1, rect))
            {
                eventsInRect.Add(lineEvent);
            }
        }

        return eventsInRect;
    }

}
