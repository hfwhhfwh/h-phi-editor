using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class ChartEditService : Node
{
    public Chart EditingChart{ get; set; }

    public void SetNoteProperty(int lineId, int noteIndex, NotePropertyEnum property, object value)
    {
        Note note = EditingChart.JudgeLineList[lineId].Notes[noteIndex];

        switch (property)
        {
            case NotePropertyEnum.Above:
                note.Above = (int)value;
                break;

            case NotePropertyEnum.Alpha:
                note.Alpha = Convert.ToSingle(value);
                break;

            case NotePropertyEnum.StartTime:
                note.SetStartTime(((Beat)value).Values, EditingChart.BpmList, 
                    EditingChart.JudgeLineList[lineId]);
                break;

            case NotePropertyEnum.EndTime:
                note.SetEndTime(((Beat)value).Values, EditingChart.BpmList, 
                    EditingChart.JudgeLineList[lineId]);
                break;

            case NotePropertyEnum.IsFake:
                note.IsFake = (bool)value;
                break;

            case NotePropertyEnum.PosX:
                note.PositionX = Convert.ToSingle(value);
                break;

            case NotePropertyEnum.Size:
                note.Size = Convert.ToSingle(value); 
                break;

            case NotePropertyEnum.Type:
                note.Type = Convert.ToInt32(value);
                break;

            case NotePropertyEnum.VisibleTime:
                note.VisibleTime = Convert.ToSingle(value);
                break;

            case NotePropertyEnum.YOffset:
                note.YOffset = Convert.ToSingle(value);
                break;

            default:
                throw new ArgumentException($"未知的属性类型: {property}");
        }

        GD.Print($"[{this.Name}] 修改note(line{lineId}_{noteIndex})属性 {property} : {value}");
    }


    /// <summary>
    /// 删除一个note（如果要一次删除多个note，可直接调用DeleteNotes）
    /// </summary>
    /// <param name="lineId">判定线编号</param>
    /// <param name="note">note在列表中的索引</param>
    public void DeleteNote(int lineId, Note note)
    {
        DeleteNoteWithoutSignal(lineId, note);

        GD.Print($"[{this.Name}] 删除note(line{lineId}_{note})");

        ChartEventBus.NotifyNoteCountChanged(lineId);  // 广播
    }

    /// <summary>
    /// 添加一个note
    /// </summary>
    /// <param name="noteType">note的类型（枚举）</param>
    /// <param name="startBeat">起始拍</param>
    /// <param name="endBeat">结束拍（若非Hold，可以与起始拍相同，也可不设置）</param>
    /// <param name="posX">note的谱面X坐标</param>
    public void AddNote(int lineId, NoteType noteType, Beat startBeat, Beat endBeat, float posX)
    {
        JudgeLine line = EditingChart.JudgeLineList[lineId];
        Note note = new Note
        {
            Above = 1,
            Alpha = 255,
            IsFake = false,
            Size = 1f,
            Speed = 1f,
            VisibleTime = 999999f,
            YOffset = 0f,

            Type = (int) noteType,
            // StartTime = startBeat.values,
            // EndTime = endBeat.values,
            PositionX = posX,
        };

        // 设置note的时间，自动生成StartSec和EndSec和allDisplacement
        note.SetStartTime(startBeat.Values, EditingChart.BpmList, line);
        note.SetEndTime(endBeat.Values, EditingChart.BpmList, line);

        // 设置note的位移和
        note.RefreshDisplacement(line);
        
        if(line.Notes == null) // 添加第一个音符时notes可能为空
            line.Notes = new List<Note>();
        List<Note> notes = line.Notes;

        // 添加音符时可以考虑直接插入到合适的位置
        notes.Add(note);
        line.SortNotes();

        ChartEventBus.NotifyNoteCountChanged(lineId);  // 广播

        GD.Print($"[{this.Name}] 成功添加note:{lineId}_{notes.IndexOf(note)}");
    }

    /// <summary>
    /// 删除一个note，且不发出谱面数据修改的信号
    /// </summary>
    /// <param name="lineId">判定线编号</param>
    /// <param name="note">note在列表中的索引</param>
    private void DeleteNoteWithoutSignal(int lineId, Note note)
    {
        List<Note> notes = EditingChart.JudgeLineList[lineId].Notes;
        notes.Remove(note);
    }

    /// <summary>
    /// 一次删除多个note
    /// </summary>
    /// <param name="lineId">判定线编号</param>
    /// <param name="notes">所有需要删除的Note</param>
    public void DeleteNotes(int lineId, List<Note> notes)
    {
        foreach(Note note in notes)
        {
            DeleteNoteWithoutSignal(lineId, note);
        }

        //发出信号
        ChartEventBus.NotifyNoteCountChanged(lineId);  // 广播

        GD.Print($"[{this.Name}] 删除note:{Util.ListToString(notes)}");
    }

    /// <summary>
    /// 添加一条判定线
    /// </summary>
    /// <param name="judgeLines">所有判定线的列表</param>
    /// <param name="id">添加判定线的索引，-1代表添加至末尾</param>
    public void AddLine(List<JudgeLine> judgeLines, int id = -1)
    {
        List<EventLayer> eventLayers = new List<EventLayer>(5);
        for(int i = 0; i < 4; i++)
        {
            eventLayers[i] = new EventLayer
            {
                MoveXEvents = new List<LineEvent>(),
                MoveYEvents = new List<LineEvent>(),
                RotateEvents = new List<LineEvent>(),
                AlphaEvents = new List<LineEvent>(),
                SpeedEvents = new List<LineEvent>(),
            };
        }
        JudgeLine line = new JudgeLine
        {
            Texture = "line.png",
            EventLayers = eventLayers,
            Father = -1,
            Notes = new List<Note>(),
        };

        if(id < 0 || id > judgeLines.Count)
        {
            judgeLines.Add(line);
        }
        else
        {
            judgeLines.Insert(id, line);
        }
        
        ChartEventBus.NotifyLineCountChanged();
    }

    /// <summary>
    /// 删除一个判定线（不发出事件通知）
    /// </summary>
    /// <param name="judgeLines">所有判定线的列表</param>
    /// <param name="lineId">要删除的判定线索引</param>
    public void DeleteLineWithoutSignal(List<JudgeLine> judgeLines, int lineId)
    {
        if(lineId < 0 || lineId > judgeLines.Count - 1)
        {
            GD.PrintErr($"[{this.Name}] DeleteLine 索引越界:{lineId}");
            return;
        }

        judgeLines.RemoveAt(lineId);
    }

    /// <summary>
    /// 删除一个判定线
    /// </summary>
    /// <param name="judgeLines">所有判定线的列表</param>
    /// <param name="lineId">要删除的判定线索引</param>
    public void DeleteLine(List<JudgeLine> judgeLines, int lineId)
    {
        DeleteLineWithoutSignal(judgeLines, lineId);

        ChartEventBus.NotifyLineCountChanged();

        GD.Print($"[{this.Name}] 成功删除判定线:{lineId}");
    }

    /// <summary>
    /// 删除若干个判定线
    /// </summary>
    /// <param name="judgeLines">所有判定线的列表</param>
    /// <param name="linesId">要删除的判定线索引列表（无需有序）</param>
    public void DeleteLines(List<JudgeLine> judgeLines, List<int> linesId)
    {
        linesId.Sort();

        //倒序遍历删除
        for(int i = linesId.Count - 1; i >= 0; i--)
        {
            DeleteLineWithoutSignal(judgeLines, i);
        }

        ChartEventBus.NotifyLineCountChanged();

        GD.Print($"[{this.Name}] 成功删除判定线:{Util.ListToString(linesId)}");
    }

    public void AddEvent(int lineId, int layer, LineEventEnum lineEventEnum, Beat startBeat, Beat endBeat)
    {
        // 添加第一个事件时可能为空，但EventLayer内部实现了懒加载，返回新的空列表
        List<LineEvent> lineEvents = 
            EditingChart.JudgeLineList[lineId].EventLayers[layer].GetLineEvents(lineEventEnum);
        
        LineEvent lineEvent = new()
        {
            Bezier = false,
            EasingLeft = 0f,
            EasingRight = 1f,
            EasingType = 1, // fixed/linear
            Start = 0f, // 事件的默认值为上一个事件的末尾值
            End = 0f,
            StartTime = startBeat.Values,
            EndTime = endBeat.Values
        };

        // 设置Event的时间，自动生成StartSec和EndSec
        lineEvent.SetStartTime(startBeat.Values, EditingChart.BpmList);
        lineEvent.SetEndTime(endBeat.Values, EditingChart.BpmList);


        if(lineEvents.Count == 0) // 列表为空（但是不可能为null）
        {
            lineEvent.Start = 0;
            lineEvent.End = 0;
            
            lineEvents.Add(lineEvent);
        }
        else // 事件列表不为空列表
        {
            // 设置事件的默认值为上一个事件的末尾值
            int lastEventIndex = ChartDataHelper.BinarySearchLatestEvent(lineEvents, lineEvent.startSec);
            if(lastEventIndex == -1) // 前面没有时间，按照默认值
            {
                lineEvent.Start = 0;
                lineEvent.End = 0;
            }
            else
            {
                LineEvent lastEvent = lineEvents[lastEventIndex];
                lineEvent.Start = lastEvent.End;
                lineEvent.End = lastEvent.End;
            }

            // 添加事件时 必须 直接插入到合适的位置
            //InsertLineEventSorted(lineEvents, lineEvent); 
            lineEvents.Insert(lastEventIndex + 1, lineEvent);
        }
        
        if(lineEventEnum == LineEventEnum.Speed)
        {
            // 速度事件需要刷新前缀和
            EditingChart.JudgeLineList[lineId].EventLayers[0].RefreshSpeedEventsPrefix();

            //重新计算所有 Note 的累积位移
            EditingChart.JudgeLineList[lineId].RefreshAllNoteDisplacement();
        }

        // ChartEventBus.NotifyStructureChanged();

        GD.Print($"[{this.Name}] 成功添加event:{lineId}_{lineEventEnum}_{lineEvents.IndexOf(lineEvent)}");
    }

    /// <summary>
    /// <para> 将某一个LineEvent插入到LineEvent列表中，按照startTime的顺序 </para>
    /// <para> 注意：调用此方法前必须设置lineEvent的StartSec </para>
    /// <para> 此方法<b>不会</b>更新速度事件的前缀和位移 </para>
    /// <para> 此方法<b>不会</b>发出信号 </para>
    /// </summary>
    /// <param name="lineEvents">LineEvent列表</param>
    /// <param name="lineEvent">要插入的LineEvent</param>
    private void InsertLineEventSorted(List<LineEvent> lineEvents, LineEvent lineEvent)
    {
        // 最后一个 startSec <= time 的事件
        int index = ChartDataHelper.BinarySearchLatestEvent(lineEvents, lineEvent.startSec);

        lineEvents.Insert(index + 1, lineEvent);
    }

    public void SetEventProperty(
        int lineId, int layer, LineEventEnum lineEventEnum, int index,
        LineEventPropertyType property, object value)
    {
        List<LineEvent> lineEvents = EditingChart.JudgeLineList[lineId].EventLayers[layer].GetLineEvents(lineEventEnum);
        LineEvent lineEvent = lineEvents[index];

        switch (property)
        {
            case LineEventPropertyType.StartTime:
                lineEvent.SetStartTime(((Beat)value).Values, EditingChart.BpmList);
                break;
            case LineEventPropertyType.EndTime:
                lineEvent.SetEndTime(((Beat)value).Values, EditingChart.BpmList);
                break;
            case LineEventPropertyType.Start:
                lineEvent.Start = (float)value;
                break;
            case LineEventPropertyType.End:
                lineEvent.End = (float)value;
                break;
            case LineEventPropertyType.EasingType:
                lineEvent.EasingType = (int)value;
                break;
            case LineEventPropertyType.EasingLeft:
                lineEvent.EasingLeft = (float)value;
                break;
            case LineEventPropertyType.EasingRight:
                lineEvent.EasingRight = (float)value;
                break;
            case LineEventPropertyType.Bezier:
                lineEvent.Bezier = (bool)value;
                break;
            default:
                throw new ArgumentException($"未知的属性类型: {property}");
        }

        // 【关键】如果修改了时间，需要确保事件列表仍然按 startSec 有序
        // 因为用户可能将时间改到另一个事件之前/之后，破坏二分查找的前提
        if (property == LineEventPropertyType.StartTime || property == LineEventPropertyType.EndTime)
        {
            // 先移除再重新插入到正确位置
            lineEvents.Remove(lineEvent);
            InsertLineEventSorted(lineEvents, lineEvent);
            
        }

        // 如果是速度事件
        if(lineEventEnum == LineEventEnum.Speed)
        {
            // 速度事件需要刷新前缀和
            EditingChart.JudgeLineList[lineId].EventLayers[0].RefreshSpeedEventsPrefix();

            //重新计算所有 Note 的累积位移
            EditingChart.JudgeLineList[lineId].RefreshAllNoteDisplacement();
        }

        GD.Print($"[{this.Name}] 修改event(line{lineId}_{lineEventEnum}_{index})属性 {property} : {value}");
        
        // ChartEventBus.NotifyStructureChanged();  // 广播
    }

    /// <summary>
    /// 删除Event
    /// </summary>
    /// <param name="lineId">判定线编号</param>
    /// <param name="lineEventEnum">事件类型</param>
    /// <param name="index">事件索引</param>
    public void DeleteEvent(int lineId, LineEventEnum lineEventEnum, int index)
    {
        DeleteEventWithoutSignal(lineId, lineEventEnum, index);

        // 特殊处理速度事件
        if(lineEventEnum == LineEventEnum.Speed)
        {
            // 速度事件需要刷新前缀和
            EditingChart.JudgeLineList[lineId].EventLayers[0].RefreshSpeedEventsPrefix();

            //重新计算所有 Note 的累积位移
            EditingChart.JudgeLineList[lineId].RefreshAllNoteDisplacement();
        
        }

        // 发出信号
        // ChartEventBus.NotifyStructureChanged();

        GD.Print($"[{Name}] 删除Event:line{lineId}_{lineEventEnum}_{index}");
    }


    /// <summary>
    /// 删除Event（不发出信号）
    /// 此方法不会重新计算EventPrefix和NoteAllDisplacement
    /// </summary>
    /// <param name="lineId"></param>
    /// <param name="lineEventEnum"></param>
    /// <param name="index"></param>
    private void DeleteEventWithoutSignal(int lineId, LineEventEnum lineEventEnum, int index)
    {
        JudgeLine line = EditingChart.JudgeLineList[lineId];
        List<LineEvent> lineEvents = line.EventLayers[0].GetLineEvents(lineEventEnum);
        lineEvents.RemoveAt(index);

    }

    public void DeleteEvents(int lineId, LineEventEnum lineEventEnum, List<int> indexes)
    {
        // 递增排序
        indexes.Sort();

        // 倒序遍历索引
        for (int i = indexes.Count - 1; i >= 0; i--)
        {
            int index = indexes[i];

            DeleteEventWithoutSignal(lineId, lineEventEnum, index);
        }

        // 特殊处理速度事件
        if(lineEventEnum == LineEventEnum.Speed)
        {
            // 速度事件需要刷新前缀和
            EditingChart.JudgeLineList[lineId].EventLayers[0].RefreshSpeedEventsPrefix();

            //重新计算所有 Note 的累积位移
            EditingChart.JudgeLineList[lineId].RefreshAllNoteDisplacement();
        }

        //发出信号
        // ChartEventBus.NotifyStructureChanged();

        GD.Print($"[{Name}] 删除Event:line{lineId}_{lineEventEnum}_{Util.ListToString(indexes)}");
    }
}

