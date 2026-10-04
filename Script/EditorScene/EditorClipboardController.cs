using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 复制 / 粘贴控制器。
///
/// 职责：维护剪贴板内容（Note / LineEvent / BPM 三种），处理复制、粘贴、
/// 粘贴确认与取消，以及粘贴过程中按钮的显隐切换。
///
/// 粘贴的实际数据修改全部交给 ChartEditService。
/// </summary>
public partial class EditorClipboardController : Node
{
    private EditorContext _context;
    private ChartEditService _editService;

    private NoteEditPanel _noteEditPanel;
    private EventEditPanel _eventEditPanel;
    private BpmEditPanel _bpmEditPanel;

    private Button _copyBtn;
    private Button _pasteBtn;
    private Button _pasteConfirmBtn;
    private Button _pasteCancelBtn;

    /// <summary>当前正在粘贴的面板类型</summary>
    private EditPanelType _pasteFocusPanel;

    public void Initialize(
        EditorContext context,
        ChartEditService editService,
        NoteEditPanel noteEditPanel,
        EventEditPanel eventEditPanel,
        BpmEditPanel bpmEditPanel,
        Button copyBtn,
        Button pasteBtn,
        Button pasteConfirmBtn,
        Button pasteCancelBtn)
    {
        _context = context;
        _editService = editService;
        _noteEditPanel = noteEditPanel;
        _eventEditPanel = eventEditPanel;
        _bpmEditPanel = bpmEditPanel;
        _copyBtn = copyBtn;
        _pasteBtn = pasteBtn;
        _pasteConfirmBtn = pasteConfirmBtn;
        _pasteCancelBtn = pasteCancelBtn;
    }

    private EditorClipboard Clipboard => _context?.Clipboard;

    // ==================== 复制 ====================

    /// <summary>
    /// 复制当前选中面板中已选择的对象
    /// </summary>
    public void CopySelection()
    {
        if (_context == null || Clipboard == null) return;

        switch (_context.SelectFocusPanel)
        {
            case EditPanelType.NoteEdit:
                CopySelectedNotes();
                break;

            case EditPanelType.LineEventEdit:
                CopySelectedEvents();
                break;

            case EditPanelType.BpmEventEdit:
                CopySelectedBpms();
                break;

            default:
                break;
        }
    }

    private void CopySelectedNotes()
    {
        if (_noteEditPanel?.SelectedNotes == null || _noteEditPanel.SelectedNotes.Count == 0) return;

        Clipboard.noteClipBoard.SourceLineId = _noteEditPanel.EditingLineId;
        Clipboard.noteClipBoard.SourceStartBeat = new Beat(_noteEditPanel.SelectedNotes.First().StartTime);
        Clipboard.noteClipBoard.SourcePosX = _noteEditPanel.SelectedNotes.First().PositionX;
        Clipboard.noteClipBoard.Notes.Clear();

        foreach (Note note in _noteEditPanel.SelectedNotes)
        {
            Clipboard.noteClipBoard.Notes.Add(NoteSnapshot.Capture(note));
        }

        Clipboard.LatestClipBoard = EditPanelType.NoteEdit;
        GD.Print($"[{Name}] 成功复制{_noteEditPanel.SelectedNotes.Count}个Note");
    }

    private void CopySelectedEvents()
    {
        if (_eventEditPanel?.SelectedEventsWithType == null || _eventEditPanel.SelectedEventsWithType.Count == 0)
            return;

        var eventList = _eventEditPanel.SelectedEventsWithType;

        // 以最早的事件作为粘贴基准
        (LineEventEnum, LineEvent) earliestEvent = eventList
            .OrderBy(kvp => kvp.Item2.StartTime[0] + kvp.Item2.StartTime[1] * 1f / kvp.Item2.StartTime[2])
            .First();

        Clipboard.lineEventClipBoard = new LineEventClipBoard
        {
            SourceLineId = _context.EditingLineId,
            SourceLayer = _context.EditingLayer,
            SourceStartBeat = new Beat(earliestEvent.Item2.StartTime),
            Events = new List<LineEventClipBoardItem>()
        };

        foreach ((LineEventEnum type, LineEvent evt) in eventList)
        {
            Clipboard.lineEventClipBoard.Events.Add(
                new LineEventClipBoardItem(type, LineEventSnapshot.Capture(evt)));
        }

        Clipboard.LatestClipBoard = EditPanelType.LineEventEdit;
        GD.Print($"[{Name}] 成功复制{eventList.Count}个Event");
    }

    private void CopySelectedBpms()
    {
        IReadOnlyCollection<BpmEvent> selectedBpms = _bpmEditPanel?.SelectedEvents;
        if (selectedBpms == null || selectedBpms.Count == 0) return;

        BpmEvent earliestBpm = selectedBpms
            .OrderBy(bpm => bpm.StartTime[0] + bpm.StartTime[1] * 1f / bpm.StartTime[2])
            .First();

        Clipboard.bpmEventClipBoard = new BpmEventClipBoard
        {
            SourceStartBeat = new Beat(earliestBpm.StartTime),
            Bpms = new List<BpmEventSnapshot>()
        };

        foreach (BpmEvent bpmEvent in selectedBpms)
        {
            Clipboard.bpmEventClipBoard.Bpms.Add(BpmEventSnapshot.Capture(bpmEvent));
        }

        Clipboard.LatestClipBoard = EditPanelType.BpmEventEdit;
        GD.Print($"[{Name}] 成功复制{selectedBpms.Count}个BPM事件");
    }

    // ==================== 单对象复制（右键菜单用） ====================

    public void CopySingleNote(int lineId, Note note)
    {
        if (Clipboard == null || note == null) return;

        Clipboard.noteClipBoard.Notes = [NoteSnapshot.Capture(note)];
        Clipboard.noteClipBoard.SourceLineId = lineId;
        Clipboard.noteClipBoard.SourceStartBeat = new Beat(note.StartTime);
        Clipboard.noteClipBoard.SourcePosX = note.PositionX;

        Clipboard.LatestClipBoard = EditPanelType.NoteEdit;
    }

    public void CopySingleEvent(int lineId, int layer, LineEventEnum lineEventEnum, LineEvent lineEvent)
    {
        if (Clipboard == null || lineEvent == null) return;

        Clipboard.lineEventClipBoard = new LineEventClipBoard
        {
            SourceLineId = lineId,
            SourceLayer = layer,
            SourceStartBeat = new Beat(lineEvent.StartTime),
            Events = [new LineEventClipBoardItem(lineEventEnum, LineEventSnapshot.Capture(lineEvent))]
        };

        Clipboard.LatestClipBoard = EditPanelType.LineEventEdit;
    }

    public void CopySingleBpm(BpmEvent bpmEvent)
    {
        if (Clipboard == null || bpmEvent == null) return;

        Clipboard.bpmEventClipBoard = new BpmEventClipBoard
        {
            SourceStartBeat = new Beat(bpmEvent.StartTime),
            Bpms = [BpmEventSnapshot.Capture(bpmEvent)]
        };

        Clipboard.LatestClipBoard = EditPanelType.BpmEventEdit;
    }

    // ==================== 粘贴 ====================

    public void StartPaste()
    {
        if (_context == null || Clipboard == null) return;

        bool isSuccess = true;

        switch (Clipboard.LatestClipBoard)
        {
            case EditPanelType.NoteEdit:
                _noteEditPanel.StartPaste(Clipboard.noteClipBoard);
                _pasteFocusPanel = EditPanelType.NoteEdit;
                break;

            case EditPanelType.LineEventEdit:
                _eventEditPanel.StartPaste(Clipboard.lineEventClipBoard);
                _pasteFocusPanel = EditPanelType.LineEventEdit;
                break;

            case EditPanelType.BpmEventEdit:
                if (Clipboard.bpmEventClipBoard?.Bpms == null || Clipboard.bpmEventClipBoard.Bpms.Count == 0)
                {
                    isSuccess = false;
                    break;
                }

                _bpmEditPanel.StartPaste(Clipboard.bpmEventClipBoard);
                _pasteFocusPanel = EditPanelType.BpmEventEdit;
                break;

            default:
                isSuccess = false;
                break;
        }

        if (isSuccess)
        {
            SetPasteApplyButtonVisibility(true);
        }
    }

    public void ConfirmPaste()
    {
        if (_context == null || Clipboard == null) return;

        SetPasteApplyButtonVisibility(false);

        // 应用粘贴
        switch (_pasteFocusPanel)
        {
            case EditPanelType.NoteEdit:
                _noteEditPanel.ExitPasteMode();
                _editService.PasteNotes(
                    Clipboard.noteClipBoard,
                    _context.EditingLineId,
                    _noteEditPanel.PasteTargetBeat,
                    _noteEditPanel.PasteTargetPosX
                );
                break;

            case EditPanelType.LineEventEdit:
                _eventEditPanel.ExitPasteMode();
                _editService.PasteEvents(
                    Clipboard.lineEventClipBoard,
                    _context.EditingLineId,
                    _context.EditingLayer,
                    _eventEditPanel.PasteTargetBeat
                );
                break;

            case EditPanelType.BpmEventEdit:
                _bpmEditPanel.ExitPasteMode();
                if (Clipboard.bpmEventClipBoard != null && Clipboard.bpmEventClipBoard.Bpms.Count > 0)
                {
                    _editService.PasteBpmEvents(Clipboard.bpmEventClipBoard, _bpmEditPanel.PasteTargetBeat);
                }
                break;

            default:
                break;
        }
    }

    public void CancelPaste()
    {
        SetPasteApplyButtonVisibility(false);

        _noteEditPanel?.ExitPasteMode();
        _eventEditPanel?.ExitPasteMode();
        _bpmEditPanel?.ExitPasteMode();
    }

    private void SetPasteApplyButtonVisibility(bool value)
    {
        if (_pasteBtn != null) _pasteBtn.Visible = !value;
        if (_copyBtn != null) _copyBtn.Visible = !value;

        if (_pasteConfirmBtn != null) _pasteConfirmBtn.Visible = value;
        if (_pasteCancelBtn != null) _pasteCancelBtn.Visible = value;
    }
}
