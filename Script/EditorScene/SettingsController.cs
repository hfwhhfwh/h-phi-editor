using Godot;
using System;

/// <summary>
/// 设置控制器。
///
/// 职责：
/// 1. 编辑器设置（EditorSettings，按谱面保存的网格布局）：载入、应用到面板、保存；
/// 2. 全局设置（GameSettings）：网格外观变化即时应用到面板，资源包变化即时重载；
/// 3. 资源包在播放器与渲染器上的应用。
///
/// 说明：它订阅了 EditorSettings（Autoload）与 GameSettings（单例）的事件，
/// 并在 _ExitTree 中统一退订 —— 这两处订阅在原来的 EditorScene 里是漏掉的。
/// </summary>
public partial class SettingsController : Node
{
    private EditorSettings _editorSettings;
    private EditorContext _context;

    private NoteEditPanel _noteEditPanel;
    private EventEditPanel _eventEditPanel;
    private BpmEditPanel _bpmEditPanel;

    private EditorPlaybackController _playbackController;

    private ResourcePack _resourcePack;
    private bool _subscribed;

    public void Initialize(
        EditorSettings editorSettings,
        EditorContext context,
        NoteEditPanel noteEditPanel,
        EventEditPanel eventEditPanel,
        BpmEditPanel bpmEditPanel,
        EditorPlaybackController playbackController)
    {
        _editorSettings = editorSettings;
        _context = context;
        _noteEditPanel = noteEditPanel;
        _eventEditPanel = eventEditPanel;
        _bpmEditPanel = bpmEditPanel;
        _playbackController = playbackController;

        if (_editorSettings != null)
        {
            _editorSettings.SettingChanged += OnEditorSettingChanged;
        }

        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.SettingChanged += OnGameSettingChanged;
        }

        _subscribed = true;
    }

    public override void _ExitTree()
    {
        if (_subscribed)
        {
            if (_editorSettings != null)
            {
                _editorSettings.SettingChanged -= OnEditorSettingChanged;
            }

            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.SettingChanged -= OnGameSettingChanged;
            }

            _subscribed = false;
        }

        base._ExitTree();
    }

    // ==================== 编辑器设置（跟随谱面） ====================

    /// <summary>
    /// 载入指定谱面保存的编辑器布局设置。
    /// 需要在谱面加载之前调用（Load 会触发 SettingChanged，从而把设置应用到面板）。
    /// </summary>
    public void LoadForChart(string chartId)
    {
        if (_editorSettings == null || string.IsNullOrEmpty(chartId)) return;

        _editorSettings.Load(chartId);
    }

    /// <summary>
    /// 把当前编辑器设置应用到所有使用同一网格的编辑面板
    /// </summary>
    public void ApplyEditorSettings()
    {
        if (_editorSettings == null)
        {
            GD.PrintErr($"[{Name}] EditorSettings is null");
            return;
        }

        int verLineCount = _editorSettings.Current.VerLineCount;
        int subBeatCount = _editorSettings.Current.SubBeatCount;

        _noteEditPanel.VerLineCount = verLineCount;
        _noteEditPanel.SubBeatCount = subBeatCount;

        _eventEditPanel.SubBeatCount = subBeatCount;

        _bpmEditPanel.SubBeatCount = subBeatCount;

        ApplyGridAppearanceSettings();
    }

    /// <summary>
    /// 保存当前谱面的编辑器设置
    /// </summary>
    public void SaveEditorSettings()
    {
        if (_editorSettings == null || string.IsNullOrEmpty(_context?.EditingChartId)) return;

        _editorSettings.Save();
    }

    private void OnEditorSettingChanged(string key, Variant value)
    {
        if (_editorSettings == null)
        {
            GD.PrintErr($"[{Name}] EditorSettings is null");
            return;
        }

        switch (key)
        {
            case nameof(EditorSettingsData.VerLineCount):
                _noteEditPanel.VerLineCount = _editorSettings.Current.VerLineCount;
                break;

            case nameof(EditorSettingsData.SubBeatCount):
                int subBeatCount = _editorSettings.Current.SubBeatCount;
                _noteEditPanel.SubBeatCount = subBeatCount;
                _eventEditPanel.SubBeatCount = subBeatCount;
                _bpmEditPanel.SubBeatCount = subBeatCount;
                break;

            default:
                GD.PrintErr($"[{Name}] 未知的EditorSettings设置项:{key}");
                ApplyEditorSettings();
                break;
        }
    }

    // ==================== 全局设置 ====================

    private void OnGameSettingChanged(string key, Variant value)
    {
        if (key == nameof(SettingsData.ResourcePackId) || key == nameof(SettingsData.UseDefaultResource))
        {
            LoadResourcePack();
            return;
        }

        if (key == nameof(SettingsData.HorColor)
            || key == nameof(SettingsData.HorWidth)
            || key == nameof(SettingsData.HorSubColor)
            || key == nameof(SettingsData.HorSubWidth)
            || key == nameof(SettingsData.VerColor)
            || key == nameof(SettingsData.VerWidth)
            || key == nameof(SettingsData.GroundLineColor)
            || key == nameof(SettingsData.GroundLineWidth))
        {
            ApplyGridAppearanceSettings();
        }
    }

    private void ApplyGridAppearanceSettings()
    {
        if (GameSettings.Instance?.Current == null) return;

        SettingsData settings = GameSettings.Instance.Current;

        ApplyGridAppearance(_noteEditPanel, settings);
        ApplyGridAppearance(_eventEditPanel, settings);
        ApplyGridAppearance(_bpmEditPanel, settings);
    }

    private static void ApplyGridAppearance(BaseEditPanel panel, SettingsData settings)
    {
        if (panel == null) return;

        panel.HorColor = settings.HorColor;
        panel.HorWidth = settings.HorWidth;
        panel.HorSubColor = settings.HorSubColor;
        panel.HorSubWidth = settings.HorSubWidth;
        panel.VerColor = settings.VerColor;
        panel.VerWidth = settings.VerWidth;
        panel.GroundLineColor = settings.GroundLineColor;
        panel.GroundLineWidth = settings.GroundLineWidth;
    }

    // ==================== 资源包 ====================

    /// <summary>
    /// 按当前全局设置加载资源包（或切回默认资源），并应用到播放器与渲染器
    /// </summary>
    public void LoadResourcePack()
    {
        if (GameSettings.Instance == null) return;

        bool useDefault = GameSettings.Instance.Get<bool>(nameof(SettingsData.UseDefaultResource));
        if (useDefault)
        {
            _playbackController?.UseDefaultResource();
        }
        else
        {
            string id = GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId));
            _resourcePack = ResourcePackLoader.LoadFromLocal(id);
            if (_resourcePack == null)
            {
                GD.PrintErr($"[{Name}] 加载资源包失败, id:{id}");
            }
            _playbackController?.SetResourcePack(_resourcePack);
        }

        GD.Print($"[{Name}] 成功重新加载资源包!");
    }
}
