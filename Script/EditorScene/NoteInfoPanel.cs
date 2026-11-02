using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using HPhiEditorGame.Editor;

public partial class NoteInfoPanel : Panel
{
    [Export] private InfoEditPanel infoEditPanel;

    private int editingLineId;
    private int editingNoteIndex;
    private Note _note;

    /// <summary>根字段容器：只负责 View 布局、事件转发、ApplyToUI。</summary>
    private CompositeField _root;

    /// <summary>label → 该字段的变更处理器，由根事件按 Path[0] 分发。</summary>
    private readonly Dictionary<string, Action<EditorChange>> _fieldHandlers = new();

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

        infoEditPanel.OnConfirmed += () => EmitSignal(SignalName.OnConfirmed);
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

        ClearEditors();
        infoEditPanel.TitleLabel.Text = $"正在编辑: 音符{noteIndex}";

        _root = new CompositeField();
        _root.View.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _root.ValueChanged += OnRootValueChanged;
        infoEditPanel.FieldContainer.AddChild(_root.View);

        AddField("StartTime", () => new Beat(_note.StartTime), NotePropertyEnum.StartTime);
        AddField("EndTime",   () => new Beat(_note.EndTime),   NotePropertyEnum.EndTime);
        AddField("Type",      () => ConvertNoteType(_note.Type), NotePropertyEnum.Type);
        AddField("PositionX", () => _note.PositionX, NotePropertyEnum.PosX, v => Convert.ToSingle(v));
        AddField("Alpha",     () => _note.Alpha,     NotePropertyEnum.Alpha, v => Convert.ToInt32(v));
    }

    private static NoteType ConvertNoteType(int type) => type switch
    {
        1 => NoteType.Tap,
        2 => NoteType.Hold,
        3 => NoteType.Flick,
        4 => NoteType.Drag,
        _ => NoteType.Tap
    };

    /// <summary>根字段事件分发：Path[0] 是字段 label。</summary>
    private void OnRootValueChanged(EditorChange change)
    {
        if (change.Path == null || change.Path.Count == 0) return;
        if (_fieldHandlers.TryGetValue(change.Path[0], out var handler))
            handler(change);
    }

    private void AddField<T>(string label, Func<T> readValue, NotePropertyEnum propType,
        Func<object, object> convert = null)
    {
        IPropertyEditor<T> editor = PropertyEditorFactory.Create<T>();
        editor.Setup(label, () => readValue());
        editor.SetValueSilently(readValue());
        _root.AddField(editor);

        _fieldHandlers[label] = change =>
        {
            object value = convert != null ? convert(change.Value) : change.Value;
            _editService?.SetNoteProperty(editingLineId, editingNoteIndex, propType, value);
        };
    }

    private void OnHistoryChanged()
    {
        if (_refreshing || !Visible || _note == null) return;

        int index = IndexOfEditedNote();
        if (index < 0)
        {
            Visible = false;
            return;
        }

        editingNoteIndex = index;
        infoEditPanel.TitleLabel.Text = $"正在编辑: 音符{index}";

        _refreshing = true;
        try
        {
            _root?.ApplyToUI();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private int IndexOfEditedNote()
    {
        Chart chart = _context?.EditingChart;
        if (chart?.JudgeLineList == null) return -1;
        if (editingLineId < 0 || editingLineId >= chart.JudgeLineList.Count) return -1;

        List<Note> notes = chart.JudgeLineList[editingLineId].Notes;
        return notes?.IndexOf(_note) ?? -1;
    }

    private void ClearEditors()
    {
        _fieldHandlers.Clear();
        _root = null;

        foreach (Node child in infoEditPanel.FieldContainer.GetChildren())
        {
            infoEditPanel.FieldContainer.RemoveChild(child);
            child.QueueFree();
        }
    }
}