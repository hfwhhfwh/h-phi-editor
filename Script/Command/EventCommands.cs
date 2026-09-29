// File: Editor/Commands/EventCommands.cs
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using QuickType;

public class AddEventCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly int _layer;
    private readonly LineEventEnum _type;
    private readonly Beat _startBeat;
    private readonly Beat _endBeat;

    private LineEvent _created;

    public string Name => "添加事件";

    public AddEventCommand(int lineId, int layer, LineEventEnum type, Beat startBeat, Beat endBeat)
    {
        _lineId = lineId;
        _layer = layer;
        _type = type;
        _startBeat = startBeat.Duplicate();
        _endBeat = endBeat.Duplicate();
    }

    public void Execute(ChartEditService service)
    {
        var chart = service.EditingChart;
        var line = chart.JudgeLineList[_lineId];
        var list = line.EventLayers[_layer].GetLineEvents(_type);

        if (_created == null)
        {
            _created = new LineEvent
            {
                Bezier = false,
                BezierPoints = new[] { 0f, 0f, 0f, 0f },
                EasingLeft = 0f,
                EasingRight = 1f,
                EasingType = 1,
                Start = 0f,
                End = 0f,
                StartTime = _startBeat.Duplicate().Values,
                EndTime = _endBeat.Duplicate().Values,
            };
            _created.SetStartTime(_startBeat.Duplicate().Values, chart.BpmList);
            _created.SetEndTime(_endBeat.Duplicate().Values, chart.BpmList);

            if (list.Count == 0)
            {
                _created.Start = 0f;
                _created.End = 0f;
            }
            else
            {
                int lastIdx = ChartDataHelper.BinarySearchLatestEvent(list, _created.startSec);
                if (lastIdx == -1)
                {
                    _created.Start = 0f;
                    _created.End = 0f;
                }
                else
                {
                    _created.Start = list[lastIdx].End;
                    _created.End = list[lastIdx].End;
                }
            }
        }

        service.InsertLineEventSorted(list, _created);

        if (_type == LineEventEnum.Speed)
            service.RefreshSpeedDependencies(_lineId);
    }

    public void Undo(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        var list = line.EventLayers[_layer].GetLineEvents(_type);
        list.Remove(_created);

        if (_type == LineEventEnum.Speed)
            service.RefreshSpeedDependencies(_lineId);
    }
}

public class PasteEventsCommand : IEditCommand
{
    private readonly int _targetLineId;
    private readonly int _targetLayer;
    private readonly Beat _sourceStartBeat;
    private readonly Beat _targetBeat;
    private readonly List<LineEventClipBoardItem> _eventItems = new();
    private readonly List<(LineEventEnum Type, LineEvent Event)> _created = new();

    public string Name => "粘贴事件";

    public PasteEventsCommand(LineEventClipBoard clipBoard, int targetLineId, int targetLayer, Beat targetBeat)
    {
        if (clipBoard == null) throw new ArgumentNullException(nameof(clipBoard));
        if (clipBoard.SourceStartBeat == null) throw new ArgumentException("剪贴板缺少源起始拍", nameof(clipBoard));
        if (targetBeat == null) throw new ArgumentNullException(nameof(targetBeat));
        if (!IsCompatibleLayer(clipBoard.SourceLayer, targetLayer))
            throw new ArgumentException("目标事件层与源事件层不兼容", nameof(targetLayer));

        _targetLineId = targetLineId;
        _targetLayer = targetLayer;
        _sourceStartBeat = clipBoard.SourceStartBeat.Duplicate();
        _targetBeat = targetBeat.Duplicate();
        _eventItems.AddRange(clipBoard.Events);
    }

    public void Execute(ChartEditService service)
    {
        var chart = service.EditingChart;
        if (chart?.JudgeLineList == null || _targetLineId < 0 || _targetLineId >= chart.JudgeLineList.Count)
            throw new ArgumentOutOfRangeException(nameof(_targetLineId));

        var line = chart.JudgeLineList[_targetLineId];
        if (line.EventLayers == null || _targetLayer < 0 || _targetLayer >= line.EventLayers.Count)
            throw new ArgumentOutOfRangeException(nameof(_targetLayer));

        Beat deltaBeat = _targetBeat - _sourceStartBeat;
        _created.Clear();
        bool containsSpeedEvents = false;

        foreach (var item in _eventItems)
        {
            if (!Enum.IsDefined(typeof(LineEventEnum), item.Type))
                throw new ArgumentOutOfRangeException(nameof(item.Type));

            LineEvent lineEvent = item.Snapshot.Create();
            Beat startBeat = new Beat(lineEvent.StartTime) + deltaBeat;
            Beat endBeat = new Beat(lineEvent.EndTime) + deltaBeat;
            lineEvent.SetStartTime(startBeat.Values, chart.BpmList);
            lineEvent.SetEndTime(endBeat.Values, chart.BpmList);

            List<LineEvent> events = line.EventLayers[_targetLayer].GetLineEvents(item.Type);
            if (events.Count == 0)
                events.Add(lineEvent);
            else
                service.InsertLineEventSorted(events, lineEvent);

            _created.Add((item.Type, lineEvent));
            containsSpeedEvents |= item.Type == LineEventEnum.Speed;
        }

        if (containsSpeedEvents)
            RefreshSpeedDependencies(service, line);
    }

    public void Undo(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_targetLineId];
        bool containsSpeedEvents = false;

        foreach (var created in _created)
        {
            line.EventLayers[_targetLayer].GetLineEvents(created.Type).Remove(created.Event);
            containsSpeedEvents |= created.Type == LineEventEnum.Speed;
        }

        if (containsSpeedEvents)
            RefreshSpeedDependencies(service, line);
    }

    private void RefreshSpeedDependencies(ChartEditService service, JudgeLine line)
    {
        if (_targetLayer == 0)
            service.RefreshSpeedDependencies(_targetLineId);
        else
            line.EventLayers[_targetLayer].RefreshSpeedEventsPrefix();
    }

    private static bool IsCompatibleLayer(int sourceLayer, int targetLayer)
    {
        if (sourceLayer == 4) return targetLayer == 4;
        return sourceLayer >= 0 && sourceLayer <= 3 && targetLayer >= 0 && targetLayer <= 3;
    }
}

public class DeleteEventsCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly int _layer;
    private readonly List<(LineEventEnum Type, LineEvent Evt)> _removed = new();
    private readonly List<(LineEventEnum Type, LineEventSnapshot Snapshot)> _snapshots = new();

    public string Name => "删除事件";

    public DeleteEventsCommand(int lineId, int layer, IEnumerable<(LineEventEnum Type, LineEvent Evt)> events)
    {
        _lineId = lineId;
        _layer = layer;
        foreach (var e in events)
        {
            if (e.Evt == null) continue;
            _removed.Add(e);
            _snapshots.Add((e.Type, LineEventSnapshot.Capture(e.Evt)));
        }
    }

    public void Execute(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        // var list = line.EventLayers[_layer].GetLineEvents(_type);

        bool hasSpeedEvent = false;
        foreach (var e in _removed)
        {
            line.EventLayers[_layer].GetLineEvents(e.Type).Remove(e.Evt);
            // list.Remove(e);

            if(e.Type == LineEventEnum.Speed) hasSpeedEvent = true;
        }

        if (hasSpeedEvent)
            service.RefreshSpeedDependencies(_lineId);
    }

    public void Undo(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        
        bool hasSpeedEvent = false;

        for (int i = 0; i < _removed.Count; i++)
        {
            (LineEventEnum type, LineEvent lineEvent) = _removed[i];
            
            _snapshots[i].Snapshot.ApplyTo(lineEvent);
            service.InsertLineEventSorted(line.EventLayers[_layer].GetLineEvents(type), lineEvent);

            if(type == LineEventEnum.Speed) hasSpeedEvent = true;
        }

        if (hasSpeedEvent)
            service.RefreshSpeedDependencies(_lineId);
    }
}

public class SetEventPropertyCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly int _layer;
    private readonly LineEventEnum _type;
    private readonly LineEvent _target;
    private readonly LineEventPropertyType _property;
    private readonly object _newValue;

    private LineEventSnapshot _snapshot;
    private bool _captured;

    public string Name => "修改事件属性";

    public SetEventPropertyCommand(int lineId, int layer, LineEventEnum type, LineEvent target,
                                   LineEventPropertyType property, object newValue)
    {
        _lineId = lineId;
        _layer = layer;
        _type = type;
        _target = target;
        _property = property;
        _newValue = newValue;
    }

    public void Execute(ChartEditService service)
    {
        if (!_captured)
        {
            _snapshot = LineEventSnapshot.Capture(_target);
            _captured = true;
        }

        Apply(service, _newValue);
        ResortIfTimeChanged(service);
        if (_type == LineEventEnum.Speed) service.RefreshSpeedDependencies(_lineId);
    }

    public void Undo(ChartEditService service)
    {
        _snapshot.ApplyTo(_target);
        ResortIfTimeChanged(service);
        if (_type == LineEventEnum.Speed) service.RefreshSpeedDependencies(_lineId);
    }

    private void Apply(ChartEditService service, object value)
    {
        var chart = service.EditingChart;
        switch (_property)
        {
            case LineEventPropertyType.StartTime:
                _target.SetStartTime(((Beat)value).Duplicate().Values, chart.BpmList);
                break;
            case LineEventPropertyType.EndTime:
                _target.SetEndTime(((Beat)value).Duplicate().Values, chart.BpmList);
                break;
            case LineEventPropertyType.Start:      _target.Start = (float)value; break;
            case LineEventPropertyType.End:        _target.End = (float)value; break;
            case LineEventPropertyType.EasingType: _target.EasingType = (int)value; break;
            case LineEventPropertyType.EasingLeft: _target.EasingLeft = (float)value; break;
            case LineEventPropertyType.EasingRight:_target.EasingRight = (float)value; break;
            case LineEventPropertyType.Bezier:     _target.Bezier = (bool)value; break;
            default: throw new ArgumentException($"未知属性: {_property}");
        }
    }

    private void ResortIfTimeChanged(ChartEditService service)
    {
        if (_property != LineEventPropertyType.StartTime &&
            _property != LineEventPropertyType.EndTime) return;

        var line = service.EditingChart.JudgeLineList[_lineId];
        var list = line.EventLayers[_layer].GetLineEvents(_type);
        list.Remove(_target);
        service.InsertLineEventSorted(list, _target);
    }
}