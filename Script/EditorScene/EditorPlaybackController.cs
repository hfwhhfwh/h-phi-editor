using Godot;
using QuickType;
using System;

/// <summary>
/// 播放 / 预览控制器。
///
/// 职责：
/// 1. 持有谱面播放器与渲染器，负责每帧的时间轴同步、逻辑更新与渲染；
/// 2. 管理播放/暂停/停止与「编辑器模式 / 播放器模式」之间的切换（含面板显隐与右侧页签）；
/// 3. 管理资源包在播放器与渲染器上的应用。
///
/// 注意：必须在谱面播放器 Initialize 完成之后才能调用 <see cref="EnterEditingMode"/>，
/// 否则会提前调用到播放器内部尚未创建的音频播放节点。
/// </summary>
public partial class EditorPlaybackController : Node
{
    private EditorContext _context;
    private BaseChartPlayer _chartPlayer;
    private BaseChartRenderer _chartRenderer;
    private Control _chartPlayParent;
    private Control _editPanel;
    private BaseEditPanel[] _editPanels;
    private RightPanel _rightPanel;

    private bool _subscribed;

    /// <summary>是否正在播放谱面</summary>
    public bool IsPlaying { get; private set; }

    #if TOOLS
    private double _timeSyncTimeUs;
    private double _logicTimeUs;
    private double _renderTimeUs;
    #endif

    public void Initialize(
        EditorContext context,
        BaseChartPlayer chartPlayer,
        BaseChartRenderer chartRenderer,
        Control chartPlayParent,
        Control editPanel,
        BaseEditPanel[] editPanels,
        RightPanel rightPanel)
    {
        _context = context;
        _chartPlayer = chartPlayer;
        _chartRenderer = chartRenderer;
        _chartPlayParent = chartPlayParent;
        _editPanel = editPanel;
        _editPanels = editPanels;
        _rightPanel = rightPanel;

        PlayModeManager.PlayModeChanged += OnPlayModeChanged;
        _subscribed = true;

        #if TOOLS
        Performance.AddCustomMonitor("EditorPlayback/TimeSyncUs", Callable.From(() => _timeSyncTimeUs));
        Performance.AddCustomMonitor("EditorPlayback/LogicUs", Callable.From(() => _logicTimeUs));
        Performance.AddCustomMonitor("EditorPlayback/RenderUs", Callable.From(() => _renderTimeUs));
        #endif
    }

    /// <summary>
    /// PlayModeManager 是静态类，必须在这里断开，否则下次进入编辑器会残留回调。
    /// </summary>
    public override void _ExitTree()
    {
        if (_subscribed)
        {
            PlayModeManager.PlayModeChanged -= OnPlayModeChanged;
            _subscribed = false;
        }

        #if TOOLS
        Performance.RemoveCustomMonitor("EditorPlayback/TimeSyncUs");
        Performance.RemoveCustomMonitor("EditorPlayback/LogicUs");
        Performance.RemoveCustomMonitor("EditorPlayback/RenderUs");
        #endif

        base._ExitTree();
    }

    /// <summary>
    /// 在谱面播放器初始化完成、编辑面板可用之后调用，进入初始的编辑模式。
    /// </summary>
    public void EnterEditingMode()
    {
        PlayModeManager.SetPlayMode(PlayModeEnum.Editing);
    }

    /// <summary>
    /// 由 EditorScene 每帧调用：同步时间轴、更新播放器逻辑并渲染。
    /// </summary>
    public void ProcessPlayback(double delta)
    {
        if (_context == null || _chartPlayer == null || _chartRenderer == null) return;

        #if TOOLS
        ulong t1 = Time.GetTicksUsec();
        #endif

        if (IsPlaying)
        {
            //正在播放时，时间轴由音乐决定
            _context.ChartTime = _chartPlayer.ChartTime;
        }
        else
        {
            //否则，时间轴由编辑器面板决定
            _chartPlayer.ExternalTime = _context.ChartTime;
        }

        #if TOOLS
        ulong t2 = Time.GetTicksUsec();
        #endif

        _chartPlayer.UpdateLogic(delta);

        #if TOOLS
        ulong t3 = Time.GetTicksUsec();
        #endif

        if (!_chartPlayer.Disabled && !_chartRenderer.Disabled)
        {
            (JudgeLineRenderData[] lineData, int lineCount) = _chartPlayer.GetLineRenderDatas();
            (NoteRenderData[] noteData, int noteCount) = _chartPlayer.GetNoteRenderDatas();

            _chartRenderer.Render(lineData, lineCount, noteData, noteCount);
        }

        #if TOOLS
        ulong t4 = Time.GetTicksUsec();
        _timeSyncTimeUs = t2 - t1;
        _logicTimeUs = t3 - t2;
        _renderTimeUs = t4 - t3;
        #endif
    }

    // ==================== 资源包 ====================

    /// <summary>显示/隐藏谱面播放器（含渲染器）</summary>
    public void SetPlayerVisible(bool value)
    {
        SetChartPlayerVisible(value);
    }

    public void UseDefaultResource()
    {
        _chartPlayer?.UseDefaultResource();
        _chartRenderer?.UseDefaultResource();
    }

    public void SetResourcePack(ResourcePack pack)
    {
        if (_chartPlayer != null) _chartPlayer.Pack = pack;
        if (_chartRenderer != null) _chartRenderer.Pack = pack;
    }

    // ==================== 场景按钮入口 ====================
    // 这些方法由 editor_scene.tscn 的信号连接直接调用，因此保留在 EditorScene 上并转发到这里。

    public void PlayWithPlayer()
    {
        // 切换播放模式
        PlayModeManager.SetPlayMode(PlayModeEnum.PlayerPlaying);
    }

    /// <summary>在编辑面板内滚动播放（再次点击返回编辑模式）</summary>
    public void TogglePlayInEditPanel()
    {
        if (PlayModeManager.PlayMode == PlayModeEnum.EditorPlaying)
        {
            PlayModeManager.SetPlayMode(PlayModeEnum.Editing);
        }
        else
        {
            PlayModeManager.SetPlayMode(PlayModeEnum.EditorPlaying);
        }
    }

    public void Stop()
    {
        PlayModeManager.SetPlayMode(PlayModeEnum.Editing);
    }

    public void PausePlayer()
    {
        PlayModeManager.SetPlayMode(PlayModeEnum.PlayerPause);
    }

    // ==================== 模式切换 ====================

    private void OnPlayModeChanged(PlayModeEnum playMode)
    {
        switch (playMode)
        {
            case PlayModeEnum.Editing:
                SetChartPlayerVisible(false);
                SetEditPanelVisible(true);
                SetIsPlaying(false);
                _rightPanel?.SwitchToTab(RightPanel.RightPanelTabPage.Normal);
                break;

            case PlayModeEnum.PlayerPlaying:
                SetChartPlayerVisible(true);
                SetEditPanelVisible(false);
                SetIsPlaying(true);
                _rightPanel?.SwitchToTab(RightPanel.RightPanelTabPage.AutoPlay);
                break;

            case PlayModeEnum.PlayerPause:
                SetChartPlayerVisible(true);
                SetEditPanelVisible(false);
                SetIsPlaying(false);
                _rightPanel?.SwitchToTab(RightPanel.RightPanelTabPage.Pause);
                break;

            case PlayModeEnum.EditorPlaying:
                SetChartPlayerVisible(false);
                SetEditPanelVisible(true);
                SetIsPlaying(true);
                _rightPanel?.SwitchToTab(RightPanel.RightPanelTabPage.Normal);
                break;

            case PlayModeEnum.EditorAndPlayerPlaying:
                SetChartPlayerVisible(true);
                SetEditPanelVisible(true);
                SetIsPlaying(true);
                // TODO
                break;
        }
    }

    private void SetEditPanelVisible(bool value)
    {
        if (_editPanel != null) _editPanel.Visible = value;

        if (_editPanels == null) return;
        foreach (BaseEditPanel panel in _editPanels)
        {
            if (panel != null) panel.Disabled = !value;
        }
    }

    private void SetChartPlayerVisible(bool value)
    {
        if (_chartPlayParent != null) _chartPlayParent.Visible = value;
        if (_chartPlayer != null) _chartPlayer.Disabled = !value;
        if (_chartRenderer != null) _chartRenderer.Disabled = !value;
    }

    private void SetIsPlaying(bool value)
    {
        IsPlaying = value;

        if (_chartPlayer == null) return;

        if (value) _chartPlayer.Play((float)_context.ChartTime);
        else _chartPlayer.Pause();
    }
}
