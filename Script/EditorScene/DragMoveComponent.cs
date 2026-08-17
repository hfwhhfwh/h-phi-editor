using Godot;
using QuickType;
using System;

/// <summary>
/// 通用对象拖动组件，管理拖动状态、阈值判断、吸附计算与事件分发。
/// 设计意图：与 DragPlaceComponent / BoxSelectController 同级，供任意编辑面板复用。
/// </summary>
public class DragMoveComponent
{
    public enum DragMode
    {
        None,
        Head,   // 拖动头部（如 Hold 头、Event 起始时间）
        Tail,   // 拖动尾部（如 Hold 尾、Event 结束时间）
        Body    // 拖动整体（移动 X 或整体时间偏移）
    }

    public bool IsDragging { get; private set; }
    public DragMode Mode { get; private set; }

    /// <summary>
    /// 当前拖动的对象标识。由外部面板解释其含义：
    /// NoteEditPanel 可传 int(noteIndex)；EventEditPanel 可传 (LineEventEnum, int) 等。
    /// </summary>
    public object TargetId { get; private set; }

    /// <summary>上次触发 Moved 时的吸附 X（谱面坐标）</summary>
    public float LastChartX { get; private set; }

    /// <summary>上次触发 Moved 时的吸附 Beat</summary>
    public Beat LastBeat { get; private set; }

    // -------- 事件 --------
    /// <summary>参数: (targetId, mode, newChartX, newBeat)</summary>
    public event Action<object, DragMode, float, Beat> Moved;
    public event Action<object, DragMode> Started;
    public event Action<object, DragMode> Ended;

    // -------- 生命周期 --------
    public void Start(object targetId, DragMode mode, float initialChartX, Beat initialBeat)
    {
        TargetId = targetId;
        Mode = mode;
        IsDragging = true;
        LastChartX = initialChartX;
        LastBeat = initialBeat;
        Started?.Invoke(targetId, mode);
    }

    /// <summary>
    /// 在 PointerDrag 中调用。只有真正超过 InputController 阈值后，才应进入此分支。
    /// </summary>
    public void Update(Vector2 pointerPos, CoordinateComponent coord, bool allowX, bool allowY)
    {
        if (!IsDragging) return;

        bool changed = false;
        float newX = LastChartX;
        Beat newBeat = LastBeat;

        if (allowX)
        {
            float chartX = coord.GetChartPosX(pointerPos.X);
            float snappedX = coord.SnapChartXToGrid(chartX);
            if (!Mathf.IsEqualApprox(snappedX, LastChartX))
            {
                newX = snappedX;
                changed = true;
            }
        }

        if (allowY)
        {
            float beatValue = coord.GetBeatValue(pointerPos.Y);
            Beat snappedBeat = coord.SnapBeatValueToGrid(beatValue);
            if (snappedBeat != LastBeat)
            {
                newBeat = snappedBeat;
                changed = true;
            }
        }

        if (changed)
        {
            LastChartX = newX;
            LastBeat = newBeat;
            Moved?.Invoke(TargetId, Mode, newX, newBeat);
        }
    }

    public void End()
    {
        if (!IsDragging) return;
        Ended?.Invoke(TargetId, Mode);
        IsDragging = false;
        Mode = DragMode.None;
        TargetId = null;
    }
}