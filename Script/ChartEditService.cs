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