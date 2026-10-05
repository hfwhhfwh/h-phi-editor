using System;

/// <summary>
/// 与谱面数据有关的事件总线。
///
/// 由 <see cref="ChartEditService"/> 持有一个实例：编辑命令在改动谱面结构后通过
/// <c>service.Events.Notify*</c> 广播，订阅方（谱面播放器、判定线面板）由场景在初始化时
/// 注入同一个实例。
///
/// 说明：这是普通 C# 类而不是静态类 —— 不再有跨场景残留的全局订阅，
/// 生命周期跟随持有者，也便于单独测试。
/// </summary>
public sealed class ChartEventBus
{
    /// <summary>
    /// 某一条判定线中添加/删除 note 时触发
    /// </summary>
    public event Action<int> NoteCountChanged;

    public void NotifyNoteCountChanged(int lineId)
    {
        NoteCountChanged?.Invoke(lineId);
    }

    /// <summary>
    /// 判定线数量改变时触发
    /// </summary>
    public event Action LineCountChanged;

    public void NotifyLineCountChanged()
    {
        LineCountChanged?.Invoke();
    }

    /// <summary>
    /// 判定线的父级改变时触发（目前还没有触发点，保留给后续功能）
    /// </summary>
    public event Action<int, int> LineFatherChanged;

    public void NotifyLineFatherChanged(int lineId, int father)
    {
        LineFatherChanged?.Invoke(lineId, father);
    }
}
