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
                ChartDataHelper.SetNoteStartTime(
                    note, 
                    ((Beat)value).Values, 
                    EditingChart.BpmList, EditingChart.JudgeLineList[lineId].EventLayers[0].SpeedEvents
                );
                break;

            case NotePropertyEnum.EndTime:
                ChartDataHelper.SetNoteEndTime(
                    note, 
                    ((Beat)value).Values, 
                    EditingChart.BpmList, EditingChart.JudgeLineList[lineId].EventLayers[0].SpeedEvents
                );
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
        
        ChartEventBus.NotifyDataChanged();  // 广播
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

        ChartEventBus.NotifyDataChanged();  // 广播
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

        // 调用ChratDataHelper设置note的时间，自动生成StartSec和EndSec和allDisplacement
        List<LineEvent> speedEvents = EditingChart.JudgeLineList[lineId].EventLayers[0].SpeedEvents;
        ChartDataHelper.SetNoteStartTime(note, startBeat.Values, EditingChart.BpmList, speedEvents);
        ChartDataHelper.SetNoteEndTime(note, endBeat.Values, EditingChart.BpmList, speedEvents);

        note.allDisplacement = ChartDataHelper.GetDisplacementAtTime(
            EditingChart.JudgeLineList[lineId].EventLayers[0].SpeedEvents,
            note.startSec
        );

        
        if(EditingChart.JudgeLineList[lineId].Notes == null) // 添加第一个音符时notes可能为空
            EditingChart.JudgeLineList[lineId].Notes = new List<Note>();
        List<Note> notes = EditingChart.JudgeLineList[lineId].Notes;

        notes.Add(note); // TODO 添加音符时可以考虑直接插入到合适的位置

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
        ChartEventBus.NotifyDataChanged();

        GD.Print($"[{this.Name}] 删除note:{Util.ListToString(notes)}");
    }

    /// <summary>
    /// 添加一条判定线
    /// </summary>
    /// <param name="judgeLines">所有判定线的列表</param>
    /// <param name="id">添加判定线的索引，-1代表添加至末尾</param>
    public void AddLine(List<JudgeLine> judgeLines, int id = -1)
    {
        // HACK AddLine考虑直接在数据模型中写构造函数，设置默认值
        EventLayer eventLayer = new EventLayer
        {
            MoveXEvents = new List<LineEvent>(),
            MoveYEvents = new List<LineEvent>(),
            RotateEvents = new List<LineEvent>(),
            AlphaEvents = new List<LineEvent>(),
            SpeedEvents = new List<LineEvent>(),
        };
        EventLayer[] eventLayers = new EventLayer[5];
        for(int i = 0; i < 5; i++)
        {
            eventLayers[i] = eventLayer;
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
        
        ChartEventBus.NotifyDataChanged();
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

        ChartEventBus.NotifyDataChanged();

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

        ChartEventBus.NotifyDataChanged();

        GD.Print($"[{this.Name}] 成功删除判定线:{Util.ListToString(linesId)}");
    }

    public void AddEvent(int lineId, LineEventEnum lineEventEnum, Beat startBeat, Beat endBeat)
    {
        // 添加第一个事件时可能为空，但EventLayer内部实现了懒加载，返回新的空列表
        List<LineEvent> lineEvents = 
            EditingChart.JudgeLineList[lineId].EventLayers[0].GetLineEvents(lineEventEnum);
        
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

        // 调用ChratDataHelper设置note的时间，自动生成StartSec和EndSec
        ChartDataHelper.SetEventStartTime(lineEvent, startBeat.Values, EditingChart.BpmList);
        ChartDataHelper.SetEventEndTime(lineEvent, endBeat.Values, EditingChart.BpmList);

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

        
        if(lineEventEnum == LineEventEnum.Speed)
        {
            // 速度事件需要刷新前缀和
            ChartDataHelper.RefreshEventPrefix(lineEvents);

            //重新计算所有 Note 的累积位移
            ChartDataHelper.RefreshNotesAllDisplacement(EditingChart.JudgeLineList[lineId]);
        }

        ChartEventBus.NotifyDataChanged();

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
        int lineId, LineEventEnum lineEventEnum, int index,
        LineEventPropertyType property, object value)
    {
        List<LineEvent> lineEvents = EditingChart.JudgeLineList[lineId].EventLayers[0].GetLineEvents(lineEventEnum);
        LineEvent lineEvent = lineEvents[index];

        switch (property)
        {
            case LineEventPropertyType.StartTime:
                // 【关键】重新计算秒数
                ChartDataHelper.SetEventStartTime(lineEvent, ((Beat)value).Values, EditingChart.BpmList);
                break;
            case LineEventPropertyType.EndTime:
                ChartDataHelper.SetEventEndTime(lineEvent, ((Beat)value).Values, EditingChart.BpmList);
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
            
            // 如果是速度事件，修改时间后前缀和也需要刷新
            if (lineEventEnum == LineEventEnum.Speed)
            {
                ChartDataHelper.RefreshEventPrefix(lineEvents);

                //重新计算所有 Note 的累积位移
                ChartDataHelper.RefreshNotesAllDisplacement(EditingChart.JudgeLineList[lineId]);
            }
        }

        GD.Print($"[{this.Name}] 修改event(line{lineId}_{lineEventEnum}_{index})属性 {property} : {value}");
        
        ChartEventBus.NotifyDataChanged();  // 广播
    }
}

