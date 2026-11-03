using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using HPhiEditorGame.Editor;

public partial class NoteInfoPanel : Panel
{
    [Export] private InfoEditPanel _infoEditPanel;

    private int editingLineId;
    private int editingNoteIndex;
    private Note _note;

    private EditorContext _context;
    private ChartEditService _editService;
    private bool _refreshing;

    [Signal] public delegate void OnConfirmedEventHandler();

    public void Initialize(EditorContext context, ChartEditService editService)
    {
        _context = context;
        _editService = editService;
        if (_editService != null) _editService.HistoryChanged += OnHistoryChanged;
    }

    public override void _Ready()
    {
        base._Ready();
        _infoEditPanel.OnConfirmed += () => EmitSignal(SignalName.OnConfirmed);
    }

    public override void _ExitTree()
    {
        if (_editService != null) _editService.HistoryChanged -= OnHistoryChanged;
        base._ExitTree();
    }

    public void ShowInfo(Note note, int lineId, int noteIndex)
    {
        _note = note;
        editingLineId = lineId;
        editingNoteIndex = noteIndex;

        _infoEditPanel.BeginEdit($"音符{noteIndex}");

        _infoEditPanel.AddField("StartTime",
            () => new Beat(_note.StartTime),
            v => NotifyPropertyChanged(NotePropertyEnum.StartTime, v));

        _infoEditPanel.AddField("EndTime",
            () => new Beat(_note.EndTime),
            v => NotifyPropertyChanged(NotePropertyEnum.EndTime, v));

        _infoEditPanel.AddField("Type",
            () => ConvertNoteType(_note.Type),
            v => NotifyPropertyChanged(NotePropertyEnum.Type, v));

        _infoEditPanel.AddField("PositionX",
            () => _note.PositionX,
            v => NotifyPropertyChanged(NotePropertyEnum.PosX, Convert.ToSingle(v)),
            new NumericFieldOptions { MinValue = -675, MaxValue = 675, Step = 0.1 });

        _infoEditPanel.AddField("Alpha",
            () => _note.Alpha,
            v => NotifyPropertyChanged(NotePropertyEnum.Alpha, Convert.ToInt32(v)),
            new NumericFieldOptions { MinValue = 0, MaxValue = 255, Step = 1 });
    }

    private void NotifyPropertyChanged(NotePropertyEnum prop, object value)
    {
        _editService?.SetNoteProperty(editingLineId, editingNoteIndex, prop, value);
    }

    private static NoteType ConvertNoteType(int type) => type switch
    {
        1 => NoteType.Tap,
        2 => NoteType.Hold,
        3 => NoteType.Flick,
        4 => NoteType.Drag,
        _ => NoteType.Tap
    };

    private void OnHistoryChanged()
    {
        if (_refreshing || !Visible || _note == null) return;

        int index = IndexOfEditedNote();
        if (index < 0) { Visible = false; return; }

        editingNoteIndex = index;
        _infoEditPanel.UpdateTitle($"音符{index}");

        _refreshing = true;
        try { _infoEditPanel.ApplyToUI(); }
        finally { _refreshing = false; }
    }

    private int IndexOfEditedNote()
    {
        Chart chart = _context?.EditingChart;
        if (chart?.JudgeLineList == null) return -1;
        if (editingLineId < 0 || editingLineId >= chart.JudgeLineList.Count) return -1;
        List<Note> notes = chart.JudgeLineList[editingLineId].Notes;
        return notes?.IndexOf(_note) ?? -1;
    }
}