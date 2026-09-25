// File: Editor/Commands/BpmCommands.cs
using System.Collections.Generic;
using QuickType;

public class AddBpmCommand : IEditCommand
{
    private readonly float _bpm;
    private readonly Beat _startBeat;
    private BpmEvent _created;

    public string Name => "添加 BPM";

    public AddBpmCommand(float bpm, Beat startBeat)
    {
        _bpm = bpm;
        _startBeat = startBeat.Duplicate();
    }

    public void Execute(ChartEditService service)
    {
        var chart = service.EditingChart;
        service.EnsureBpmList();

        if (_created == null)
        {
            _created = new BpmEvent
            {
                Bpm = _bpm,
                StartTime = _startBeat.Duplicate().Values
            };
        }

        if (!chart.BpmList.Contains(_created))
            chart.BpmList.Add(_created);

        service.SortBpmList();
        service.RefreshBpmDependencies();
    }

    public void Undo(ChartEditService service)
    {
        service.EditingChart.BpmList.Remove(_created);
        service.RefreshBpmDependencies();
    }
}

public class SetBpmPropertyCommand : IEditCommand
{
    private readonly BpmEvent _target;
    private readonly string _property;
    private readonly object _newValue;
    private BpmEventSnapshot _snapshot;
    private bool _captured;

    public string Name => "修改 BPM 属性";

    public SetBpmPropertyCommand(BpmEvent target, string property, object newValue)
    {
        _target = target;
        _property = property;
        _newValue = newValue;
    }

    public void Execute(ChartEditService service)
    {
        if (!_captured)
        {
            _snapshot = BpmEventSnapshot.Capture(_target);
            _captured = true;
        }

        Apply(service, _newValue);
        service.SortBpmList();
        service.RefreshBpmDependencies();
    }

    public void Undo(ChartEditService service)
    {
        _snapshot.ApplyTo(_target);
        service.SortBpmList();
        service.RefreshBpmDependencies();
    }

    private void Apply(ChartEditService service, object value)
    {
        switch (_property)
        {
            case "Bpm":
                _target.Bpm = System.Convert.ToSingle(value);
                break;
            case "StartTime":
                _target.StartTime = ((Beat)value).Duplicate().Values;
                break;
        }
    }
}

public class SetBpmTimeCommand : IEditCommand
{
    private readonly BpmEvent _target;
    private readonly Beat _newStart;
    private BpmEventSnapshot _snapshot;
    private bool _captured;

    public string Name => "修改 BPM 时间";

    public SetBpmTimeCommand(BpmEvent target, Beat newStart)
    {
        _target = target;
        _newStart = newStart.Duplicate();
    }

    public void Execute(ChartEditService service)
    {
        if (!_captured)
        {
            _snapshot = BpmEventSnapshot.Capture(_target);
            _captured = true;
        }
        _target.StartTime = _newStart.Duplicate().Values;
        service.SortBpmList();
        service.RefreshBpmDependencies();
    }

    public void Undo(ChartEditService service)
    {
        _snapshot.ApplyTo(_target);
        service.SortBpmList();
        service.RefreshBpmDependencies();
    }
}

public class DeleteBpmsCommand : IEditCommand
{
    private readonly List<BpmEvent> _removed = new();
    private readonly List<BpmEventSnapshot> _snapshots = new();

    public string Name => "删除 BPM";

    public DeleteBpmsCommand(IEnumerable<BpmEvent> toRemove)
    {
        foreach (var e in toRemove) _removed.Add(e);
    }

    public void Execute(ChartEditService service)
    {
        var chart = service.EditingChart;
        if (chart.BpmList == null || _removed.Count == 0) return;

        var baseBpm = chart.BpmList[0];
        _snapshots.Clear();

        for (int i = _removed.Count - 1; i >= 0; i--)
        {
            var e = _removed[i];
            if (e == null || e == baseBpm) { _removed.RemoveAt(i); continue; }
            if (chart.BpmList.Remove(e))
                _snapshots.Insert(0, BpmEventSnapshot.Capture(e));
        }

        service.SortBpmList();
        service.RefreshBpmDependencies();
    }

    public void Undo(ChartEditService service)
    {
        var chart = service.EditingChart;
        service.EnsureBpmList();

        for (int i = 0; i < _removed.Count; i++)
        {
            _snapshots[i].ApplyTo(_removed[i]);
            if (!chart.BpmList.Contains(_removed[i]))
                chart.BpmList.Add(_removed[i]);
        }

        service.SortBpmList();
        service.RefreshBpmDependencies();
    }
}