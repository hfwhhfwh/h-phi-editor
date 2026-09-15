// File: Editor/Commands/EventCommands.cs
using System;
using System.Collections.Generic;
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

public class DeleteEventsCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly int _layer;
    private readonly LineEventEnum _type;
    private readonly List<LineEvent> _removed = new();
    private readonly List<LineEventSnapshot> _snapshots = new();

    public string Name => "删除事件";

    public DeleteEventsCommand(int lineId, int layer, LineEventEnum type, IEnumerable<LineEvent> events)
    {
        _lineId = lineId;
        _layer = layer;
        _type = type;
        foreach (var e in events)
        {
            if (e == null) continue;
            _removed.Add(e);
            _snapshots.Add(LineEventSnapshot.Capture(e));
        }
    }

    public void Execute(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        var list = line.EventLayers[_layer].GetLineEvents(_type);

        foreach (var e in _removed) list.Remove(e);

        if (_type == LineEventEnum.Speed)
            service.RefreshSpeedDependencies(_lineId);
    }

    public void Undo(ChartEditService service)
    {
        var line = service.EditingChart.JudgeLineList[_lineId];
        var list = line.EventLayers[_layer].GetLineEvents(_type);

        for (int i = 0; i < _removed.Count; i++)
        {
            _snapshots[i].ApplyTo(_removed[i]);
            service.InsertLineEventSorted(list, _removed[i]);
        }

        if (_type == LineEventEnum.Speed)
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