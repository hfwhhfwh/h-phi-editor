using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class ChartEditService : Node
{
    public Chart EditingChart{get; set;}

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
    }

    public void DeleteNote(int lineId, int noteIndex)
    {
        // List<Note> notes = EditingChart.JudgeLineList[lineId].Notes;
        // notes.RemoveAt(noteIndex);
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
