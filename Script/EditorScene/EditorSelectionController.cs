using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 选择 / 删除控制器。
///
/// 职责：
/// 1. 记录「当前哪一个面板持有选择焦点」，并让其它面板取消选择；
/// 2. 汇总各面板的选中/取消选中事件，维护删除按钮的显隐；
/// 3. 处理对象上的右键菜单（编辑 / 复制 / 删除）与删除按钮。
/// </summary>
public partial class EditorSelectionController : Node
{
    private EditorContext _context;
    private ChartEditService _editService;
    private EditorClipboardController _clipboard;

    private NoteEditPanel _noteEditPanel;
    private EventEditPanel _eventEditPanel;
    private BpmEditPanel _bpmEditPanel;

    private NoteInfoPanel _noteInfoPanel;
    private LineEventInfoPanel _eventInfoPanel;
    private BpmInfoPanel _bpmInfoPanel;

    private Button _deleteBtn;

    private bool _isSelecting;

    /// <summary>
    /// 是否正在选择某些对象（Note、Event等），单选多选都算
    /// </summary>
    private bool IsSelecting
    {
        get => _isSelecting;
        set
        {
            _isSelecting = value;
            if (_deleteBtn != null) _deleteBtn.Visible = value;
        }
    }

    public void Initialize(
        EditorContext context,
        ChartEditService editService,
        EditorClipboardController clipboard,
        NoteEditPanel noteEditPanel,
        EventEditPanel eventEditPanel,
        BpmEditPanel bpmEditPanel,
        NoteInfoPanel noteInfoPanel,
        LineEventInfoPanel eventInfoPanel,
        BpmInfoPanel bpmInfoPanel,
        Button deleteBtn)
    {
        _context = context;
        _editService = editService;
        _clipboard = clipboard;
        _noteEditPanel = noteEditPanel;
        _eventEditPanel = eventEditPanel;
        _bpmEditPanel = bpmEditPanel;
        _noteInfoPanel = noteInfoPanel;
        _eventInfoPanel = eventInfoPanel;
        _bpmInfoPanel = bpmInfoPanel;
        _deleteBtn = deleteBtn;

        // 各面板的「选中 / 取消选中」事件
        if (_noteEditPanel != null)
        {
            _noteEditPanel.OnNoteSelected += OnNoteSelected;
            _noteEditPanel.NoteMultiSelected += OnNoteMultiSelected;
            _noteEditPanel.AllDeselected += OnAllDeselected;
        }

        if (_eventEditPanel != null)
        {
            _eventEditPanel.EventSelected += OnEventSelected;
            _eventEditPanel.EventMultiSelected += OnEventMultiSelected;
            _eventEditPanel.AllDeselected += OnAllDeselected;
        }

        if (_bpmEditPanel != null)
        {
            _bpmEditPanel.EventSelected += OnBpmSelected;
            _bpmEditPanel.BpmMultiSelected += OnBpmMultiSelected;
            _bpmEditPanel.AllDeselected += OnAllDeselected;
        }

        if (_deleteBtn != null)
        {
            _deleteBtn.Visible = false; // 默认不显示
            _deleteBtn.Pressed += OnDeletePressed;
        }
    }

    /// <summary>
    /// 取消对各面板与删除按钮的订阅
    /// </summary>
    public override void _ExitTree()
    {
        if (_noteEditPanel != null)
        {
            _noteEditPanel.OnNoteSelected -= OnNoteSelected;
            _noteEditPanel.NoteMultiSelected -= OnNoteMultiSelected;
            _noteEditPanel.AllDeselected -= OnAllDeselected;
        }

        if (_eventEditPanel != null)
        {
            _eventEditPanel.EventSelected -= OnEventSelected;
            _eventEditPanel.EventMultiSelected -= OnEventMultiSelected;
            _eventEditPanel.AllDeselected -= OnAllDeselected;
        }

        if (_bpmEditPanel != null)
        {
            _bpmEditPanel.EventSelected -= OnBpmSelected;
            _bpmEditPanel.BpmMultiSelected -= OnBpmMultiSelected;
            _bpmEditPanel.AllDeselected -= OnAllDeselected;
        }

        if (_deleteBtn != null)
        {
            _deleteBtn.Pressed -= OnDeletePressed;
        }

        base._ExitTree();
    }

    /// <summary>
    /// 切换选择焦点：焦点面板保留选择，其它面板取消选择。
    /// </summary>
    private void SetSelectFocus(EditPanelType type)
    {
        if (_context != null) _context.SelectFocusPanel = type;

        List<BaseEditPanel> editPanels = [ _noteEditPanel, _eventEditPanel, _bpmEditPanel ];
        List<EditPanelType> editPanelTypes = [ EditPanelType.NoteEdit, EditPanelType.LineEventEdit, EditPanelType.BpmEventEdit ];

        for (int i = 0; i < editPanels.Count; i++)
        {
            BaseEditPanel panel = editPanels[i];
            if (panel == null) continue;

            if (editPanelTypes[i] != type) panel.DeselectAll();
        }
    }

    private void OnAllDeselected()
    {
        IsSelecting = false;
    }

    /// <summary>谱面是否已经加载完成（加载过程中面板事件可能提前触发）</summary>
    private bool IsChartReady()
    {
        return _context?.EditingChart?.JudgeLineList != null;
    }

    // ==================== Note ====================

    private void OnNoteSelected(int lineId, int noteIndex, Vector2 popupViewportPos)
    {
        if (!IsChartReady()) return;

        SetSelectFocus(EditPanelType.NoteEdit);
        IsSelecting = true;

        Note note = _context.EditingChart.JudgeLineList[lineId].Notes[noteIndex];

        // 构建菜单项（使用闭包捕获当前音符信息）
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnNoteEdit(lineId, noteIndex) },
            new PopupMenuItem { Text = "复制", Callback = () => OnNoteCopy(lineId, noteIndex) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnNoteDelete(lineId, noteIndex) }
        };

        PopupMenuHelper.Instance.ShowPopupMenu(this, popupViewportPos, items);
    }

    private void OnNoteEdit(int lineId, int noteIndex)
    {
        if (_noteInfoPanel == null || !IsChartReady()) return;

        _noteInfoPanel.Visible = true;
        Note note = _context.EditingChart.JudgeLineList[lineId].Notes[noteIndex];
        _noteInfoPanel.ShowInfo(note, lineId, noteIndex);
    }

    private void OnNoteCopy(int lineId, int noteIndex)
    {
        if (!IsChartReady()) return;

        Note note = _context.EditingChart.JudgeLineList[lineId].Notes[noteIndex];
        _clipboard?.CopySingleNote(lineId, note);

        GD.Print($"[{Name}] 复制Note: Line{lineId}_{noteIndex} {(NoteType)note.Type}");
    }

    private void OnNoteDelete(int lineId, int noteIndex)
    {
        if (!IsChartReady()) return;

        Note note = _context.EditingChart.JudgeLineList[lineId].Notes[noteIndex];
        _editService.DeleteNote(lineId, note);
    }

    private void OnNoteMultiSelected()
    {
        SetSelectFocus(EditPanelType.NoteEdit);
        IsSelecting = true;
    }

    // ==================== LineEvent ====================

    private void OnEventSelected(int lineId, int layer, LineEventEnum lineEventEnum, int eventIndex, Vector2 popupViewportPos)
    {
        if (!IsChartReady()) return;

        SetSelectFocus(EditPanelType.LineEventEdit);
        IsSelecting = true;

        // 构建菜单项
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnEventEdit(lineId, layer, lineEventEnum, eventIndex) },
            new PopupMenuItem { Text = "复制", Callback = () => OnEventCopy(lineId, layer, lineEventEnum, eventIndex) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnEventDelete(lineId, layer, lineEventEnum, eventIndex) }
        };

        PopupMenuHelper.Instance.ShowPopupMenu(this, popupViewportPos, items);
    }

    private void OnEventEdit(int lineId, int layer, LineEventEnum lineEventEnum, int index)
    {
        GD.Print($"[{Name}] 编辑事件 line:{lineId}, type:{lineEventEnum}, index:{index}");

        if (_eventInfoPanel == null || !IsChartReady()) return;

        _eventInfoPanel.Visible = true;
        _eventEditPanel.DeselectAll();
        IsSelecting = false;

        LineEvent lineEvent = _context.EditingChart.JudgeLineList[lineId].EventLayers[layer].GetLineEvents(lineEventEnum)[index];

        _eventInfoPanel.Edit(lineEvent, lineId, layer, lineEventEnum, index);
    }

    private void OnEventCopy(int lineId, int layer, LineEventEnum lineEventEnum, int index)
    {
        if (!IsChartReady()) return;

        LineEvent lineEvent = _context.EditingChart.JudgeLineList[lineId].EventLayers[layer].GetLineEvents(lineEventEnum)[index];
        _clipboard?.CopySingleEvent(lineId, layer, lineEventEnum, lineEvent);

        GD.Print($"[{Name}] 复制事件 line:{lineId}, type:{lineEventEnum}, index:{index}");
    }

    private void OnEventDelete(int lineId, int layer, LineEventEnum lineEventEnum, int index)
    {
        if (!IsChartReady()) return;

        _editService.DeleteEvent(lineId, layer, lineEventEnum, index);
    }

    private void OnEventMultiSelected()
    {
        SetSelectFocus(EditPanelType.LineEventEdit);
        IsSelecting = true;
    }

    // ==================== Bpm ====================

    private void OnBpmSelected(int index, Vector2 popupViewportPos)
    {
        if (_context.EditingChart?.BpmList == null || index < 0 || index >= _context.EditingChart.BpmList.Count)
        {
            return;
        }

        SetSelectFocus(EditPanelType.BpmEventEdit);
        IsSelecting = true;

        BpmEvent bpmEvent = _context.EditingChart.BpmList[index];
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnBpmEdit(bpmEvent) },
            new PopupMenuItem { Text = "复制", Callback = () => OnBpmCopy(bpmEvent) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnBpmDelete(bpmEvent) }
        };

        PopupMenuHelper.Instance.ShowPopupMenu(this, popupViewportPos, items);
    }

    private void OnBpmMultiSelected()
    {
        SetSelectFocus(EditPanelType.BpmEventEdit);
        IsSelecting = true;
    }

    private void OnBpmEdit(BpmEvent bpmEvent)
    {
        if (bpmEvent == null || !_context.EditingChart.BpmList.Contains(bpmEvent))
        {
            return;
        }

        _bpmEditPanel.DeselectAll();
        IsSelecting = false;

        if (_bpmInfoPanel == null) return;

        _bpmInfoPanel.Visible = true;
        _bpmInfoPanel.Edit(bpmEvent, _context.EditingChart.BpmList.IndexOf(bpmEvent));
    }

    private void OnBpmCopy(BpmEvent bpmEvent)
    {
        _clipboard?.CopySingleBpm(bpmEvent);
        GD.Print($"[{Name}] 复制 BPM:{bpmEvent?.Bpm}");
    }

    private void OnBpmDelete(BpmEvent bpmEvent)
    {
        _editService.DeleteBpms(new List<BpmEvent> { bpmEvent });
        _bpmEditPanel.DeselectAll();
        IsSelecting = false;
    }

    // ==================== 删除 ====================

    /// <summary>
    /// 当删除按钮被按下时调用
    /// </summary>
    private void OnDeletePressed()
    {
        bool isSuccess = true;

        // 由于删除按钮只有一个，需要判断当前选中的对象位于哪个面板
        switch (_context.SelectFocusPanel)
        {
            case EditPanelType.NoteEdit:
                if (_noteEditPanel.SelectedNotes != null && _noteEditPanel.SelectedNotes.Count != 0)
                {
                    _editService.DeleteNotes(_context.EditingLineId, _noteEditPanel.SelectedNotes);
                }
                break;

            case EditPanelType.LineEventEdit:
                if (_eventEditPanel.SelectedEventsWithType != null &&
                    _eventEditPanel.SelectedEventsWithType.Count != 0)
                {
                    List<(LineEventEnum Type, LineEvent Evt)> eventsToDelete = _eventEditPanel.SelectedEventsWithType
                        .ToList();
                    _editService.DeleteEvents(_context.EditingLineId, _context.EditingLayer, eventsToDelete);
                }
                break;

            case EditPanelType.BpmEventEdit:
                if (_bpmEditPanel.SelectedEvents != null && _bpmEditPanel.SelectedEvents.Count != 0)
                {
                    _editService.DeleteBpms(_bpmEditPanel.SelectedEvents.ToList());
                }
                break;

            default:
                GD.PrintErr($"[{Name}] 未知的选中面板类型:{_context.SelectFocusPanel}");
                isSuccess = false;
                break;
        }

        if (isSuccess)
        {
            IsSelecting = false;
            _noteEditPanel.DeselectAll();
            _eventEditPanel.DeselectAll();
            _bpmEditPanel.DeselectAll();
        }
    }
}
