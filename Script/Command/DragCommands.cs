using System;
using QuickType;

// 这些命令用于“拖动事务”中最终的单次提交：
// - 之前每次 Move 都会生成一条命令，导致多次撤销；
// - 现在拖动开始和结束之间只更新共享 Chart，最终只保留一条 DragCommand。
// 这样既保留实时编辑效果，也能让 Undo 只需要按一次。
public class NoteDragCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly Note _target;
    private readonly NoteSnapshot _before;
    private NoteSnapshot _after;

    public string Name => "拖动音符";

    public NoteDragCommand(int lineId, Note target, NoteSnapshot before)
    {
        _lineId = lineId;
        _target = target;
        _before = before;
    }

    public void CaptureAfter()
    {
        _after = NoteSnapshot.Capture(_target);
    }

    public bool HasChanged()
    {
        if (_target == null) return false;
        var current = NoteSnapshot.Capture(_target);
        return !AreEqual(_before, current);
    }

    public void Execute(ChartEditService service)
    {
        if (_target == null) return;
        _after.ApplyTo(_target);
        service.RefreshNoteMultiHold();
    }

    public void Undo(ChartEditService service)
    {
        if (_target == null) return;
        _before.ApplyTo(_target);
        service.RefreshNoteMultiHold();
    }

    private static bool AreEqual(NoteSnapshot left, NoteSnapshot right)
    {
        if (left.Above != right.Above || left.Alpha != right.Alpha || left.Type != right.Type || left.IsFake != right.IsFake ||
            Math.Abs(left.PositionX - right.PositionX) > 0.0001f || Math.Abs(left.Size - right.Size) > 0.0001f ||
            Math.Abs(left.Speed - right.Speed) > 0.0001f || Math.Abs(left.VisibleTime - right.VisibleTime) > 0.0001f ||
            Math.Abs(left.YOffset - right.YOffset) > 0.0001f || Math.Abs(left.startSec - right.startSec) > 0.0001f ||
            Math.Abs(left.endSec - right.endSec) > 0.0001f || Math.Abs(left.allDisplacement - right.allDisplacement) > 0.0001f ||
            Math.Abs(left.endAllDisplacement - right.endAllDisplacement) > 0.0001f || left.isMultiHold != right.isMultiHold)
        {
            return false;
        }

        return ArraysEqual(left.StartTime, right.StartTime) && ArraysEqual(left.EndTime, right.EndTime);
    }

    private static bool ArraysEqual(int[] a, int[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}

public class EventDragCommand : IEditCommand
{
    private readonly int _lineId;
    private readonly int _layer;
    private readonly LineEventEnum _type;
    private readonly LineEvent _target;
    private readonly LineEventSnapshot _before;
    private LineEventSnapshot _after;

    public string Name => "拖动事件";

    public EventDragCommand(int lineId, int layer, LineEventEnum type, LineEvent target, LineEventSnapshot before)
    {
        _lineId = lineId;
        _layer = layer;
        _type = type;
        _target = target;
        _before = before;
    }

    public void CaptureAfter()
    {
        _after = LineEventSnapshot.Capture(_target);
    }

    public bool HasChanged()
    {
        if (_target == null) return false;
        var current = LineEventSnapshot.Capture(_target);
        return !AreEqual(_before, current);
    }

    public void Execute(ChartEditService service)
    {
        if (_target == null) return;
        _after.ApplyTo(_target);

        if (_type == LineEventEnum.Speed)
            service.RefreshSpeedDependencies(_lineId);
    }

    public void Undo(ChartEditService service)
    {
        if (_target == null) return;
        _before.ApplyTo(_target);

        if (_type == LineEventEnum.Speed)
            service.RefreshSpeedDependencies(_lineId);
    }

    private static bool AreEqual(LineEventSnapshot left, LineEventSnapshot right)
    {
        if (left.Bezier != right.Bezier || left.EasingType != right.EasingType || left.Linkgroup != right.Linkgroup ||
            left.EasingLeft != right.EasingLeft || left.EasingRight != right.EasingRight ||
            left.Start != right.Start || left.End != right.End || Math.Abs(left.startSec - right.startSec) > 0.0001f ||
            Math.Abs(left.endSec - right.endSec) > 0.0001f || Math.Abs(left.prefixX - right.prefixX) > 0.0001f)
        {
            return false;
        }

        return ArraysEqual(left.StartTime, right.StartTime) && ArraysEqual(left.EndTime, right.EndTime) &&
               ArraysEqual(left.BezierPoints, right.BezierPoints);
    }

    private static bool ArraysEqual(float[] a, float[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 0.0001f) return false;
        }
        return true;
    }

    private static bool ArraysEqual(int[] a, int[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}

public class BpmDragCommand : IEditCommand
{
    private readonly BpmEvent _target;
    private readonly BpmEventSnapshot _before;
    private BpmEventSnapshot _after;

    public string Name => "拖动 BPM";

    public BpmDragCommand(BpmEvent target, BpmEventSnapshot before)
    {
        _target = target;
        _before = before;
    }

    public void CaptureAfter()
    {
        _after = BpmEventSnapshot.Capture(_target);
    }

    public bool HasChanged()
    {
        if (_target == null) return false;
        var current = BpmEventSnapshot.Capture(_target);
        return !AreEqual(_before, current);
    }

    public void Execute(ChartEditService service)
    {
        if (_target == null) return;
        _after.ApplyTo(_target);
        service.SortBpmList();
        service.RefreshBpmDependencies();
    }

    public void Undo(ChartEditService service)
    {
        if (_target == null) return;
        _before.ApplyTo(_target);
        service.SortBpmList();
        service.RefreshBpmDependencies();
    }

    private static bool AreEqual(BpmEventSnapshot left, BpmEventSnapshot right)
    {
        if (Math.Abs(left.Bpm - right.Bpm) > 0.0001f) return false;
        return ArraysEqual(left.StartTime, right.StartTime);
    }

    private static bool ArraysEqual(int[] a, int[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}
