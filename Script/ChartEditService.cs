using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class ChartEditService : Node
{
    public Chart EditingChart{ get; set; }

    public void SetNoteProperty(int lineId, int noteIndex, NotePropertyType property, object value)
    {
        Note note = EditingChart.JudgeLineList[lineId].Notes[noteIndex];

        switch (property)
        {
            case NotePropertyType.Above:
                note.Above = (int)value;
                break;
            case NotePropertyType.Alpha:
                note.Alpha = Convert.ToSingle(value);
                break;
            case NotePropertyType.StartTime:
                note.StartTime = ((Beat)value).values;
                break;
            case NotePropertyType.EndTime:
                note.EndTime = ((Beat)value).values;
                break;
            case NotePropertyType.IsFake:
                note.IsFake = (bool)value;
                break;
            case NotePropertyType.PosX:
                note.PositionX = Convert.ToSingle(value);
                break;
            case NotePropertyType.Size:
                note.Size = Convert.ToSingle(value); 
                break;
            case NotePropertyType.Type:
                note.Type = Convert.ToInt32(value);
                break;
            case NotePropertyType.VisibleTime:
                note.VisibleTime = Convert.ToSingle(value);
                break;
            case NotePropertyType.YOffset:
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

        // 调用ChratDataHelper设置note的时间，自动生成StartSec和EndSec
        ChartDataHelper.SetNoteStartTime(note, startBeat.values, EditingChart.BpmList);
        ChartDataHelper.SetNoteEndTime(note, endBeat.values, EditingChart.BpmList);

        note.allDisplacement = ChartDataHelper.GetDisplacementAtTime(
            EditingChart.JudgeLineList[lineId].EventLayers[0].SpeedEvents,
            note.startSec
        );

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
}

/// <summary>
/// note的属性类型(枚举)
/// </summary>
public enum NotePropertyType
{
    Above,
    Alpha,
    StartTime,
    EndTime,
    IsFake,
    PosX,
    Size,
    Type,
    VisibleTime,
    YOffset
}
