using Godot;
using QuickType;
using System;
using System.Collections.Generic;

/// <summary>
/// 谱面播放器抽象基类
/// </summary>
public abstract partial class BaseChartPlayer : Node
{
    public double Time{ get; set; } = 0;            // 当前游戏时间（秒），由音乐播放控制
    public double ChartTime{ get; set; } = 0;       // 当前谱面时间，应用了偏移
    public double ExternalTime { get; set; }        // 由外部设置的游戏时间（秒）
    public bool IsPlaying{ get; private set; }              // 是否正在播放，由上级设置
    public Chart Chart{ get; set; }                 // 加载的谱面数据，由上级设置


    protected int chartOffset;                        // 谱面偏移（以毫秒计量）

    /// <summary>已订阅的谱面事件总线，null 表示未订阅</summary>
    private ChartEventBus _eventBus;

    /// <summary>
    /// 注入谱面结构变化事件总线（传 null 表示取消订阅）。
    /// 订阅与退订统一放在基类，避免每个子类各写一遍、漏掉就会内存泄漏。
    /// </summary>
    public void SetEventBus(ChartEventBus eventBus)
    {
        if (_eventBus == eventBus) return;

        UnsubscribeEventBus();

        _eventBus = eventBus;
        if (_eventBus == null) return;

        _eventBus.LineCountChanged += OnLineCountChanged;
        _eventBus.LineFatherChanged += OnLineFatherChanged;
        _eventBus.NoteCountChanged += OnNoteCountChanged;
    }

    private void UnsubscribeEventBus()
    {
        if (_eventBus == null) return;

        _eventBus.LineCountChanged -= OnLineCountChanged;
        _eventBus.LineFatherChanged -= OnLineFatherChanged;
        _eventBus.NoteCountChanged -= OnNoteCountChanged;
        _eventBus = null;
    }

    public override void _ExitTree()
    {
        UnsubscribeEventBus();

        base._ExitTree();
    }

    /// <summary>判定线数量变化（子类按需重写）</summary>
    protected virtual void OnLineCountChanged() { }

    /// <summary>判定线父级变化（子类按需重写）</summary>
    protected virtual void OnLineFatherChanged(int lineId, int father) { }

    /// <summary>某条判定线的音符数量变化（子类按需重写）</summary>
    protected virtual void OnNoteCountChanged(int lineId) { }


    // 返回数组 + 有效长度
    public abstract (JudgeLineRenderData[] Data, int Count) GetLineRenderDatas();
    public abstract (NoteRenderData[] Data, int Count) GetNoteRenderDatas();

    /// <summary>
    /// 当有note打击时触发，参数是打击位置（坐标系：parent坐标）
    /// </summary>
    // public Action<Vector2> onNoteHited;

    // 开关
    public bool Disabled { get; set; } = false;
    public bool AutoHitEnabled { get; set; } = false;

    // 资源
    protected ResourcePack _resourcePack;
    public AudioStream TapSound { get; set; }
    public AudioStream DragSound { get; set; }
    public AudioStream FlickSound { get; set; }
    public SpriteFrames HitFrames { get; set; }
    public virtual ResourcePack Pack
    {
        set
        {
            _resourcePack = value;
            TapSound = value.sxDic["click"];
            DragSound = value.sxDic["drag"];
            FlickSound = value.sxDic["flick"];
            HitFrames = value.hitEffectSF;
        }
    }

    public abstract void UseDefaultResource();

    /// <summary>
    /// 初始化
    /// </summary>
    public abstract void Initialize(Control parent, Chart chart, Image bgImage, AudioStream audio, HitEffectPool hitEffectPool);

    /// <summary>
    /// 计算判定线和note的位置
    /// </summary>
    public abstract void UpdateLogic(double deltaTime);

    /// <summary>
    /// 从指定时间开始播放
    /// </summary>
    /// <param name="time">开始播放的时间</param>
    public virtual void Play(float time)
    {
        IsPlaying = true;
    }


    /// <summary>
    /// 暂停播放
    /// </summary>
    public virtual void Pause()
    {
        IsPlaying = false;
    }

    public virtual void CreateHitEffect(Vector2 position)
    {
    }

    public virtual void CreateHitEffect(Vector2 position, Color modulate)
    {
    }

}
