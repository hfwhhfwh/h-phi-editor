using QuickType;

public struct BpmEventSnapshot
{
    public float Bpm;
    public int[] StartTime;

    public static BpmEventSnapshot Capture(BpmEvent e) => new()
    {
        Bpm = e.Bpm,
        StartTime = e.StartTime == null ? null : (int[])e.StartTime.Clone(),
    };

    public void ApplyTo(BpmEvent e)
    {
        e.Bpm = Bpm;
        e.StartTime = StartTime == null ? null : (int[])StartTime.Clone();
    }
}

public struct NoteSnapshot
{
    public int Above, Alpha, Type;
    public bool IsFake;
    public float PositionX, Size, Speed, VisibleTime, YOffset;
    public int[] StartTime, EndTime;
    public float startSec, endSec, allDisplacement, endAllDisplacement;
    public bool isMultiHold;

    public static NoteSnapshot Capture(Note n) => new()
    {
        Above = n.Above,
        Alpha = n.Alpha,
        Type = n.Type,
        IsFake = n.IsFake,
        PositionX = n.PositionX,
        Size = n.Size,
        Speed = n.Speed,
        VisibleTime = n.VisibleTime,
        YOffset = n.YOffset,
        StartTime = n.StartTime == null ? null : (int[])n.StartTime.Clone(),
        EndTime = n.EndTime == null ? null : (int[])n.EndTime.Clone(),
        startSec = n.startSec,
        endSec = n.endSec,
        allDisplacement = n.allDisplacement,
        endAllDisplacement = n.endAllDisplacement,
        isMultiHold = n.isMultiHold,
    };

    public void ApplyTo(Note n)
    {
        n.Above = Above;
        n.Alpha = Alpha;
        n.Type = Type;
        n.IsFake = IsFake;
        n.PositionX = PositionX;
        n.Size = Size;
        n.Speed = Speed;
        n.VisibleTime = VisibleTime;
        n.YOffset = YOffset;
        n.StartTime = StartTime == null ? null : (int[])StartTime.Clone();
        n.EndTime = EndTime == null ? null : (int[])EndTime.Clone();
        n.startSec = startSec;
        n.endSec = endSec;
        n.allDisplacement = allDisplacement;
        n.endAllDisplacement = endAllDisplacement;
        n.isMultiHold = isMultiHold;
    }
}

public struct LineEventSnapshot
{
    public bool Bezier;
    public float[] BezierPoints;
    public float EasingLeft, EasingRight, Start, End;
    public int EasingType, Linkgroup;
    public int[] StartTime, EndTime;
    public float startSec, endSec, prefixX;

    public static LineEventSnapshot Capture(LineEvent e) => new()
    {
        Bezier = e.Bezier,
        BezierPoints = e.BezierPoints == null ? null : (float[])e.BezierPoints.Clone(),
        EasingLeft = e.EasingLeft,
        EasingRight = e.EasingRight,
        Start = e.Start,
        End = e.End,
        EasingType = e.EasingType,
        Linkgroup = e.Linkgroup,
        StartTime = e.StartTime == null ? null : (int[])e.StartTime.Clone(),
        EndTime = e.EndTime == null ? null : (int[])e.EndTime.Clone(),
        startSec = e.startSec,
        endSec = e.endSec,
        prefixX = e.prefixX,
    };

    public void ApplyTo(LineEvent e)
    {
        e.Bezier = Bezier;
        e.BezierPoints = BezierPoints == null ? null : (float[])BezierPoints.Clone();
        e.EasingLeft = EasingLeft;
        e.EasingRight = EasingRight;
        e.Start = Start;
        e.End = End;
        e.EasingType = EasingType;
        e.Linkgroup = Linkgroup;
        e.StartTime = StartTime == null ? null : (int[])StartTime.Clone();
        e.EndTime = EndTime == null ? null : (int[])EndTime.Clone();
        e.startSec = startSec;
        e.endSec = endSec;
        e.prefixX = prefixX;
    }
}