// File: Editor/Commands/NoteCommands.cs
using System;
using System.Collections.Generic;
using QuickType;

public class AddNoteCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly NoteType _noteType;
    private readonly Beat _startBeat;
    private readonly Beat _endBeat;
    private readonly float _posX;

    private Note _created;

    public string Name => "添加音符";

    public AddNoteCommand(int lineId, NoteType noteType, Beat startBeat, Beat endBeat, float posX)
    {
        _lineId = lineId;
        _noteType = noteType;
        _startBeat = startBeat.Duplicate();
        _endBeat = endBeat.Duplicate();
        _posX = posX;
    }

    public void Execute(ChartEditService service)
    {
        var chart = service.EditingChart;
        var line = chart.JudgeLineList[_lineId];

        if (_created == null)
        {
            _created = new Note
            {
                Above = 1,
                Alpha = 255,
                IsFake = false,
                Size = 1f,
                Speed = 1f,
                VisibleTime = 999999f,
                YOffset = 0f,
                Type = (int)_noteType,
                PositionX = _posX,
            };

            _created.SetStartTime(_startBeat.Duplicate().Values, chart.BpmList, line);
            _created.SetEndTime(_endBeat.Duplicate().Values, chart.BpmList, line);
            _created.RefreshDisplacement(line);
        }

        if (line.Notes == null) line.Notes = new List<Note>();
        if (!line.Notes.Contains(_created))
            line.Notes.Add(_created);

        line.SortNotes();
        service.RefreshNoteMultiHold();
        ChartEventBus.NotifyNoteCountChanged(_lineId);
    }

    public void Undo(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        line.Notes.Remove(_created);
        service.RefreshNoteMultiHold();
        ChartEventBus.NotifyNoteCountChanged(_lineId);
    }
}

public class PasteNotesCommand : IEditCommand
{
    public string Name => "粘贴音符";

    private Beat _sourceStartBeat;
    private float _sourcePosX;
    private int _sourceLineId;
    private readonly List<NoteSnapshot> _noteSnapshots = new();

    private int _targetLineId;
    private Beat _targetBeat;
    private float _targetPosX;

    private List<Note> _created;

    public PasteNotesCommand(NoteClipBoard noteClipBoard, int targetLineId, Beat targetBeat, float targetPosX)
    {
        _sourceStartBeat = noteClipBoard.SourceStartBeat;
        _sourcePosX = noteClipBoard.SourcePosX;
        _sourceLineId = noteClipBoard.SourceLineId;
        
        for(int i = 0; i < noteClipBoard.Notes.Count; i++)
        {
            _noteSnapshots[i] = noteClipBoard.Notes[i];
        }


        _targetLineId = targetLineId;
        _targetBeat = targetBeat.Duplicate();
        _targetPosX = targetPosX;

    }

    public void Execute(ChartEditService service)
    {
        var chart = service.EditingChart;
        var line = chart.JudgeLineList[_targetLineId];

        if (line.Notes == null) line.Notes = new List<Note>();

        Beat deltaBeat = _targetBeat - _sourceStartBeat;
        float deltaPosX = _targetPosX - _sourcePosX;

        foreach(NoteSnapshot snapshot in _noteSnapshots)
        {
            Note note = snapshot.Create();

            // 保持相对位置
            Beat startBeat = new Beat(note.StartTime) + deltaBeat;
            Beat endBeat = new Beat(note.EndTime) + deltaBeat;
            float posX = note.PositionX + deltaPosX;

            note.SetStartTime(startBeat.Duplicate().Values, chart.BpmList, line);
            note.SetEndTime(endBeat.Duplicate().Values, chart.BpmList, line);
            note.PositionX = posX;

            note.RefreshDisplacement(line);

            line.Notes.Add(note);

        }

        line.SortNotes();
        service.RefreshNoteMultiHold();
        ChartEventBus.NotifyNoteCountChanged(_targetLineId);
    }

    public void Undo(ChartEditService service)
    {
        JudgeLine line = service.EditingChart.JudgeLineList[_targetLineId];



        
    }

}

public class DeleteNotesCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly List<Note> _notes = new();
    private readonly List<NoteSnapshot> _snapshots = new();

    public string Name => "删除音符";

    public DeleteNotesCommand(int lineId, IEnumerable<Note> notes)
    {
        _lineId = lineId;
        foreach (var n in notes)
        {
            if (n == null) continue;
            _notes.Add(n);
            _snapshots.Add(NoteSnapshot.Capture(n));
        }
    }

    public void Execute(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        if (line.Notes == null) return;
        foreach (var n in _notes) line.Notes.Remove(n);
        service.RefreshNoteMultiHold();
        ChartEventBus.NotifyNoteCountChanged(_lineId);
    }

    public void Undo(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        if (line.Notes == null) line.Notes = new List<Note>();
        for (int i = 0; i < _notes.Count; i++)
        {
            _snapshots[i].ApplyTo(_notes[i]);
            if (!line.Notes.Contains(_notes[i])) line.Notes.Add(_notes[i]);
        }
        line.SortNotes();
        service.RefreshNoteMultiHold();
        ChartEventBus.NotifyNoteCountChanged(_lineId);
    }
}

public class SetNotePropertyCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly Note _target;
    private readonly NotePropertyEnum _property;
    private readonly object _newValue;

    private NoteSnapshot _snapshot;
    private bool _captured;

    public string Name => "修改音符属性";

    public SetNotePropertyCommand(int lineId, Note target, NotePropertyEnum property, object newValue)
    {
        _lineId = lineId;
        _target = target;
        _property = property;
        _newValue = newValue;
    }

    public void Execute(ChartEditService service)
    {
        if (!_captured)
        {
            _snapshot = NoteSnapshot.Capture(_target);
            _captured = true;
        }
        ApplyValue(service, _newValue);

        if (_property == NotePropertyEnum.StartTime)
            service.RefreshNoteMultiHold();
    }

    public void Undo(ChartEditService service)
    {
        _snapshot.ApplyTo(_target);

        if (_property == NotePropertyEnum.StartTime || _property == NotePropertyEnum.EndTime)
            service.RefreshNoteMultiHold();
    }

    private void ApplyValue(ChartEditService service, object value)
    {
        var chart = service.EditingChart;
        var line = chart.JudgeLineList[_lineId];

        switch (_property)
        {
            case NotePropertyEnum.Above:       _target.Above = (int)value; break;
            case NotePropertyEnum.Alpha:       _target.Alpha = Convert.ToInt32(value); break;
            case NotePropertyEnum.IsFake:      _target.IsFake = (bool)value; break;
            case NotePropertyEnum.PosX:        _target.PositionX = Convert.ToSingle(value); break;
            case NotePropertyEnum.Size:        _target.Size = Convert.ToSingle(value); break;
            case NotePropertyEnum.Type:        _target.Type = Convert.ToInt32(value); break;
            case NotePropertyEnum.VisibleTime: _target.VisibleTime = Convert.ToSingle(value); break;
            case NotePropertyEnum.YOffset:     _target.YOffset = Convert.ToSingle(value); break;
            case NotePropertyEnum.StartTime:
                _target.SetStartTime(((Beat)value).Duplicate().Values, chart.BpmList, line);
                break;
            case NotePropertyEnum.EndTime:
                _target.SetEndTime(((Beat)value).Duplicate().Values, chart.BpmList, line);
                break;
            default:
                throw new ArgumentException($"未知的属性: {_property}");
        }
    }
}