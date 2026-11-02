using Godot;
using HPhiEditorGame.Editor;
using QuickType;
using System;
using System.Collections.Generic;

public partial class BpmInfoPanel : PanelContainer
{
    [Export] private VBoxContainer _container;
    [Export] private Label _titleLabel;
    [Export] private Button _confirmButton;

    private BpmEvent _bpmEvent;

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

    public override void _ExitTree()
    {
        if (_editService != null) _editService.HistoryChanged -= OnHistoryChanged;
        base._ExitTree();
    }

    public override void _Ready()
    {
        _confirmButton.ButtonUp += () => EmitSignal(SignalName.OnConfirmed);
    }

    public void Edit(BpmEvent bpmEvent, int index)
    {
        _bpmEvent = bpmEvent;

        ClearEditors();
        _titleLabel.Text = $"正在编辑: BPM事件{index}";

        _root = new CompositeField();
        _root.View.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _root.ValueChanged += OnRootValueChanged;
        _container.AddChild(_root.View);

        AddField("Bpm", () => _bpmEvent.Bpm,
            new FloatEditorOptions { MinValue = 0.01f, MaxValue = 1000f, Step = 0.1f });
        AddField("StartTime", () => new Beat(_bpmEvent.StartTime));
    }

    /// <summary>根字段事件分发：Path[0] 是字段 label。</summary>
    private void OnRootValueChanged(EditorChange change)
    {
        if (change.Path == null || change.Path.Count == 0) return;
        if (_fieldHandlers.TryGetValue(change.Path[0], out var handler))
            handler(change);
    }

    private void AddField<T>(string label, Func<T> readValue, FloatEditorOptions floatOptions = null)
    {
        IPropertyEditor<T> editor = PropertyEditorFactory.Create<T>(floatOptions);
        editor.Setup(label, () => readValue());
        editor.SetValueSilently(readValue());
        _root.AddField(editor);

        _fieldHandlers[label] = change =>
        {
            _editService?.SetBpmProperty(_bpmEvent, label, change.Value);
        };
    }

    private void OnHistoryChanged()
    {
        if (_refreshing || !Visible || _bpmEvent == null) return;

        List<BpmEvent> bpmList = _context?.EditingChart?.BpmList;
        int index = bpmList?.IndexOf(_bpmEvent) ?? -1;
        if (index < 0)
        {
            Visible = false;
            return;
        }

        _titleLabel.Text = $"正在编辑: BPM事件{index}";

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

    private void ClearEditors()
    {
        _fieldHandlers.Clear();
        _root = null;

        foreach (Node child in _container.GetChildren())
        {
            _container.RemoveChild(child);
            child.QueueFree();
        }
    }
}