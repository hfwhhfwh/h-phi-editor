// File: ChartEditService.cs
using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class ChartEditService : Node
{
    private Chart _editingChart;
    public Chart EditingChart
    {
        get => _editingChart;
        set
        {
            if (_editingChart != value)
            {
                _editingChart = value;
                _history.Clear();    // 切换谱面，历史作废
            }
        }
    }

    private readonly CommandHistory _history = new();

    // 拖动事务的临时命令：
    // 1) 拖动开始时记录原始快照；
    // 2) 拖动过程中直接修改共享 Chart，保证 ChartPlayer/Renderer 能实时显示；
    // 3) 拖动结束时再统一提交一次撤销命令，避免每次吸附都压栈。
    private IEditCommand _pendingDragCommand;

    public event Action HistoryChanged
    {
        add => _history.Changed += value;
        remove => _history.Changed -= value;
    }

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public int MaxHistorySteps
    {
        get => _history.MaxSteps;
        set => _history.MaxSteps = value;
    }

    public void Undo() => _history.Undo(this);
    public void Redo() => _history.Redo(this);
    public void ClearHistory() => _history.Clear();
    public void ExecuteCommand(IEditCommand cmd) => _history.Execute(cmd, this);

    // ------------- 拖动事务开始 / 结束 -------------
    // 设计目标：
    // - 拖动中仍然同步写入同一份 Chart 数据，因此 ChartPlayer / Renderer / 预览都能实时刷新；
    // - 但撤销栈只在一次拖动结束时压入一条命令，从而实现“拖动一次，撤销一次”。
    public void BeginNoteDrag(int lineId, Note note)
    {
        if (note == null) return;
        // 只记录原始快照，不立即写入历史栈；后续移动完全直接修改对象。
        _pendingDragCommand = new NoteDragCommand(lineId, note, NoteSnapshot.Capture(note));
    }

    public void EndNoteDrag(int lineId, Note note)
    {
        if (_pendingDragCommand is not NoteDragCommand dragCommand) return;
        if (note == null) { _pendingDragCommand = null; return; }

        // 拖动结束时记录最终状态；如果确实发生了变化，再压入一个统一命令。
        dragCommand.CaptureAfter();
        if (dragCommand.HasChanged())
            _history.Execute(dragCommand, this);
        _pendingDragCommand = null;
    }

    public void BeginEventDrag(int lineId, int layer, LineEventEnum type, LineEvent evt)
    {
        if (evt == null) return;
        _pendingDragCommand = new EventDragCommand(lineId, layer, type, evt, LineEventSnapshot.Capture(evt));
    }

    public void EndEventDrag(int lineId, int layer, LineEventEnum type, LineEvent evt)
    {
        if (_pendingDragCommand is not EventDragCommand dragCommand) return;
        if (evt == null) { _pendingDragCommand = null; return; }

        dragCommand.CaptureAfter();
        if (dragCommand.HasChanged())
            _history.Execute(dragCommand, this);
        _pendingDragCommand = null;
    }

    public void BeginBpmDrag(BpmEvent bpmEvent)
    {
        if (bpmEvent == null) return;
        _pendingDragCommand = new BpmDragCommand(bpmEvent, BpmEventSnapshot.Capture(bpmEvent));
    }

    public void EndBpmDrag(BpmEvent bpmEvent)
    {
        if (_pendingDragCommand is not BpmDragCommand dragCommand) return;
        if (bpmEvent == null) { _pendingDragCommand = null; return; }

        dragCommand.CaptureAfter();
        if (dragCommand.HasChanged())
            _history.Execute(dragCommand, this);
        _pendingDragCommand = null;
    }

    // ============== 公开 API（全部走命令） ==============

    public void AddBpm(float bpm, Beat startBeat)
    {
        if (!TimeUtil.IsValidBpm(bpm) || startBeat == null || startBeat.IntegerPart < 0 ||
            TimeUtil.GetBeatValue(startBeat) <= 0)
        {
            GD.PrintErr($"[{Name}] 添加 BPM 失败: BPM 或起始拍不合法");
            return;
        }

        _history.Execute(new AddBpmCommand(bpm, startBeat), this);
        GD.Print($"[{Name}] 成功添加 BPM:{bpm}, startBeat:{startBeat}");
    }

    public void SetBpmTime(int index, Beat startBeat)
    {
        if (EditingChart?.BpmList == null || index < 0 ||
            index >= EditingChart.BpmList.Count || startBeat == null ||
            startBeat.IntegerPart < 0 || TimeUtil.GetBeatValue(startBeat) <= 0)
        {
            GD.PrintErr($"[{Name}] 修改 BPM 时间失败：索引或起始拍不合法");
            return;
        }
        if (index == 0)
        {
            GD.Print($"[{Name}] 首个 BPM 是基准事件，不允许移动");
            return;
        }

        _history.Execute(new SetBpmTimeCommand(EditingChart.BpmList[index], startBeat), this);
    }

    public void ApplyBpmTimeDirect(int index, Beat startBeat)
    {
        if (EditingChart?.BpmList == null || index < 0 || index >= EditingChart.BpmList.Count || startBeat == null)
            return;

        var bpmEvent = EditingChart.BpmList[index];
        if (index == 0)
            return;

        bpmEvent.StartTime = startBeat.Duplicate().Values;
        SortBpmList();
        RefreshBpmDependencies();
    }

    public void SetBpmProperty(BpmEvent bpmEvent, string property, object value)
    {
        if (EditingChart?.BpmList == null || bpmEvent == null ||
            !EditingChart.BpmList.Contains(bpmEvent))
        {
            GD.PrintErr($"[{Name}] 修改 BPM 属性失败：事件不存在");
            return;
        }

        if (property == "Bpm" && !TimeUtil.IsValidBpm(Convert.ToSingle(value)))
        {
            GD.PrintErr($"[{Name}] 修改 BPM 数值失败: BPM 不合法");
            return;
        }
        if (property == "StartTime")
        {
            var beat = value as Beat;
            if (beat == null || beat.IntegerPart < 0 || TimeUtil.GetBeatValue(beat) <= 0)
            {
                GD.PrintErr($"[{Name}] 修改 BPM 时间失败：起始拍不合法");
                return;
            }
            if (EditingChart.BpmList.IndexOf(bpmEvent) == 0)
            {
                GD.Print($"[{Name}] 首个 BPM 是基准事件，不允许移动");
                return;
            }
        }

        _history.Execute(new SetBpmPropertyCommand(bpmEvent, property, value), this);
    }

    public void DeleteBpms(List<BpmEvent> bpmEvents)
    {
        if (EditingChart?.BpmList == null || bpmEvents == null || bpmEvents.Count == 0)
            return;

        _history.Execute(new DeleteBpmsCommand(bpmEvents), this);
    }

    public void SetNoteProperty(int lineId, int noteIndex, NotePropertyEnum property, object value)
    {
        Note note = EditingChart.JudgeLineList[lineId].Notes[noteIndex];
        _history.Execute(new SetNotePropertyCommand(lineId, note, property, value), this);
        GD.Print($"[{Name}] 修改note(line{lineId}_{noteIndex})属性 {property} : {value}");
    }

    // 直接修改属性：
    // 这个方法用于拖动中“实时预览”阶段，避免在每次吸附时新建一条历史命令。
    // 只有拖动结束时，才会统一提交一个 DragCommand 到 CommandHistory。
    public void ApplyNotePropertyDirect(int lineId, int noteIndex, NotePropertyEnum property, object value)
    {
        var line = EditingChart.JudgeLineList[lineId];
        if (line?.Notes == null || noteIndex < 0 || noteIndex >= line.Notes.Count)
            return;

        var target = line.Notes[noteIndex];
        switch (property)
        {
            case NotePropertyEnum.Above: target.Above = (int)value; break;
            case NotePropertyEnum.Alpha: target.Alpha = Convert.ToInt32(value); break;
            case NotePropertyEnum.IsFake: target.IsFake = (bool)value; break;
            case NotePropertyEnum.PosX: target.PositionX = Convert.ToSingle(value); break;
            case NotePropertyEnum.Size: target.Size = Convert.ToSingle(value); break;
            case NotePropertyEnum.Type: target.Type = Convert.ToInt32(value); break;
            case NotePropertyEnum.VisibleTime: target.VisibleTime = Convert.ToSingle(value); break;
            case NotePropertyEnum.YOffset: target.YOffset = Convert.ToSingle(value); break;
            case NotePropertyEnum.StartTime:
                target.SetStartTime(((Beat)value).Duplicate().Values, EditingChart.BpmList, line);
                break;
            case NotePropertyEnum.EndTime:
                target.SetEndTime(((Beat)value).Duplicate().Values, EditingChart.BpmList, line);
                break;
            default:
                throw new ArgumentException($"未知的属性: {property}");
        }

        if (property == NotePropertyEnum.StartTime || property == NotePropertyEnum.EndTime)
            RefreshNoteMultiHold();
    }

    public void DeleteNote(int lineId, Note note)
    {
        _history.Execute(new DeleteNotesCommand(lineId, new[] { note }), this);
        GD.Print($"[{Name}] 删除note(line{lineId})");
    }

    public void DeleteNotes(int lineId, List<Note> notes)
    {
        if (notes == null || notes.Count == 0) return;
        _history.Execute(new DeleteNotesCommand(lineId, notes), this);
        GD.Print($"[{Name}] 删除note: {notes.Count} 个");
    }

    public void AddNote(int lineId, NoteType noteType, Beat startBeat, Beat endBeat, float posX)
    {
        _history.Execute(new AddNoteCommand(lineId, noteType, startBeat, endBeat, posX), this);
    }

    public void AddLine(List<JudgeLine> judgeLines, int id = -1)
    {
        _history.Execute(new AddLineCommand(id), this);
    }

    public void DeleteLine(List<JudgeLine> judgeLines, int lineId)
    {
        _history.Execute(new DeleteLinesCommand(new[] { lineId }), this);
    }

    public void DeleteLines(List<JudgeLine> judgeLines, List<int> linesId)
    {
        if (linesId == null || linesId.Count == 0) return;
        _history.Execute(new DeleteLinesCommand(linesId), this);
    }

    public void AddEvent(int lineId, int layer, LineEventEnum lineEventEnum, Beat startBeat, Beat endBeat)
    {
        _history.Execute(new AddEventCommand(lineId, layer, lineEventEnum, startBeat, endBeat), this);
    }

    public void SetEventProperty(int lineId, int layer, LineEventEnum lineEventEnum, int index,
                                 LineEventPropertyType property, object value)
    {
        var list = EditingChart.JudgeLineList[lineId].EventLayers[layer].GetLineEvents(lineEventEnum);
        var target = list[index];
        _history.Execute(new SetEventPropertyCommand(lineId, layer, lineEventEnum, target, property, value), this);
    }

    // Event 的拖动过程中也走这里，不写历史，保证“拖动中实时更新”。
    // 只有拖动结束时再做一次命令提交，避免一拖动就塞满撤销栈。
    public void ApplyEventPropertyDirect(int lineId, int layer, LineEventEnum lineEventEnum, int index,
                                        LineEventPropertyType property, object value)
    {
        var list = EditingChart.JudgeLineList[lineId].EventLayers[layer].GetLineEvents(lineEventEnum);
        if (list == null || index < 0 || index >= list.Count)
            return;

        var target = list[index];
        switch (property)
        {
            case LineEventPropertyType.StartTime:
                target.SetStartTime(((Beat)value).Duplicate().Values, EditingChart.BpmList);
                break;
            case LineEventPropertyType.EndTime:
                target.SetEndTime(((Beat)value).Duplicate().Values, EditingChart.BpmList);
                break;
            case LineEventPropertyType.Start: target.Start = (float)value; break;
            case LineEventPropertyType.End: target.End = (float)value; break;
            case LineEventPropertyType.EasingType: target.EasingType = (int)value; break;
            case LineEventPropertyType.EasingLeft: target.EasingLeft = (float)value; break;
            case LineEventPropertyType.EasingRight: target.EasingRight = (float)value; break;
            case LineEventPropertyType.Bezier: target.Bezier = (bool)value; break;
            default: throw new ArgumentException($"未知属性: {property}");
        }

        if (property == LineEventPropertyType.StartTime || property == LineEventPropertyType.EndTime)
        {
            var line = EditingChart.JudgeLineList[lineId];
            var typeList = line.EventLayers[layer].GetLineEvents(lineEventEnum);
            typeList.Remove(target);
            InsertLineEventSorted(typeList, target);
        }

        if (lineEventEnum == LineEventEnum.Speed)
            RefreshSpeedDependencies(lineId);
    }

    public void DeleteEvent(int lineId, LineEventEnum lineEventEnum, int index)
    {
        var list = EditingChart.JudgeLineList[lineId].EventLayers[0].GetLineEvents(lineEventEnum);
        _history.Execute(new DeleteEventsCommand(lineId, 0, lineEventEnum, new[] { list[index] }), this);
    }

    public void DeleteEvents(int lineId, LineEventEnum lineEventEnum, List<int> indexes)
    {
        var layerList = EditingChart.JudgeLineList[lineId].EventLayers[0].GetLineEvents(lineEventEnum);
        var events = new List<LineEvent>(indexes.Count);
        foreach (var idx in indexes) events.Add(layerList[idx]);

        _history.Execute(new DeleteEventsCommand(lineId, 0, lineEventEnum, events), this);
    }

    // ============== internal：命令使用的辅助方法 ==============

    internal void EnsureBpmList()
    {
        if (EditingChart.BpmList == null)
            EditingChart.BpmList = new List<BpmEvent>();
    }

    internal void SortBpmList()
    {
        EditingChart.BpmList.Sort((left, right) =>
        {
            float lb = left.StartTime[0] + left.StartTime[1] / (float)left.StartTime[2];
            float rb = right.StartTime[0] + right.StartTime[1] / (float)right.StartTime[2];
            return lb.CompareTo(rb);
        });
    }

    internal void RefreshBpmDependencies()
    {
        ChartDataHelper.RefreshAllEventSec(EditingChart);
        ChartDataHelper.RefreshAllNoteSec(EditingChart);
        ChartDataHelper.RefreshAllEventPrefix(EditingChart);
        ChartDataHelper.RefreshAllNoteAllDisplacement(EditingChart);
    }

    internal void RefreshNoteMultiHold()
    {
        ChartDataHelper.RefreshAllNoteMultiHold(EditingChart);
    }

    internal void RefreshSpeedDependencies(int lineId)
    {
        EditingChart.JudgeLineList[lineId].EventLayers[0].RefreshSpeedEventsPrefix();
        EditingChart.JudgeLineList[lineId].RefreshAllNoteDisplacement();
    }

    internal void InsertLineEventSorted(List<LineEvent> lineEvents, LineEvent lineEvent)
    {
        int index = ChartDataHelper.BinarySearchLatestEvent(lineEvents, lineEvent.startSec);
        lineEvents.Insert(index + 1, lineEvent);
    }
}