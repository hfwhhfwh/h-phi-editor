using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

public partial class SettingsPanel : Control
{
    // ---------- 控件引用 ----------
    // 在编辑器里把对应节点设为 Unique Name，或在 Inspector 中拖拽赋值
    [Export] private OptionButton _vSyncOptionBtn;
    [Export] private OptionButton _packOptionBtn;
    // [Export] private LineEdit _resourcePackEdit;
    // [Export] private CheckButton _fullscreenCheck;
    // [Export] private OptionButton _resolutionOption;
    [Export] private Button _importPackBtn;
    [Export] private Button _exportPackBtn;
    [Export] private Button _applyBtn;
    [Export] private Button _confirmBtn;

    [Export] private ResourcePackOverview _packOverview;
    // [Export] private Button _resetBtn;

    // 分辨率预设
    // private readonly (string Name, Vector2I Size)[] _resolutions = new[]
    // {
    //     ("1280 x 720",  new Vector2I(1280, 720)),
    //     ("1920 x 1080", new Vector2I(1920, 1080)),
    //     ("2560 x 1440", new Vector2I(2560, 1440)),
    //     ("3840 x 2160", new Vector2I(3840, 2160)),
    // };

    public override void _Ready()
    {

        if (GameSettings.Instance == null)
        {
            GD.PushError("[SettingsPanel] GameSettings.Instance 为 null");
            return;
        }

        // InitOptions();      // 填充下拉框
        // RefreshUI();        // 从 Settings 读取初始值

        // UI → Settings
        BindEvents();       
        // Settings → UI
        GameSettings.Instance.SettingChanged += OnSettingChanged;
        GameSettings.Instance.SettingsApplied += OnSettingsApplied;      

        _importPackBtn.Pressed += OnImportClicked;

        // 打开自动重新加载
        this.VisibilityChanged += () =>
        {
            if(!Visible) return;

            InitOptions();      // 填充下拉框
            RefreshUI();        // 从 Settings 读取初始值
        };
    }

    public override void _ExitTree()
    {
        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.SettingChanged -= OnSettingChanged;
            GameSettings.Instance.SettingsApplied -= OnSettingsApplied;
        }
    }

    // ---------- 初始化下拉框 ----------
    private void InitOptions()
    {
        // VSync
        _vSyncOptionBtn.Clear();
        foreach (DisplayServer.VSyncMode mode in Enum.GetValues<DisplayServer.VSyncMode>())
        {
            _vSyncOptionBtn.AddItem(FormatVSyncName(mode), (int)mode);
        }

        // 资源包
        RefreshPackSettings();

        // // 分辨率
        // foreach (var res in _resolutions)
        // {
        //     _resolutionOption.AddItem(res.Name);
        // }
    }

    // ---------- 绑定：UI 修改 → Settings ----------
    private void BindEvents()
    {
        _vSyncOptionBtn.ItemSelected += (long idx) =>
        {
            int id = _vSyncOptionBtn.GetItemId((int)idx);
            GameSettings.Instance.Set(nameof(SettingsData.VSync), (DisplayServer.VSyncMode)id);
        };

        _packOptionBtn.ItemSelected += (long index) =>
        {
            string id = _packOptionBtn.GetItemMetadata((int)index).As<string>();
            GameSettings.Instance.Set(nameof(SettingsData.ResourcePackId), id);
        };

        _applyBtn.Pressed += () => GameSettings.Instance.Save();
        _confirmBtn.Pressed += () =>
        {
            GameSettings.Instance.Save();
            Visible = false;
        };
        // _resetBtn.Pressed += () => GameSettings.Instance.ResetToDefault();
    }

    // // ---------- 绑定：Settings 变化 → UI ----------
    // private void BindSignals()
    // {
    //     GameSettings.Instance.SettingChanged += OnSettingChanged;
    //     GameSettings.Instance.SettingsApplied += OnSettingsApplied;
    // }

    private void OnSettingChanged(string key, Variant value)
    {
        switch (key)
        {
            case nameof(SettingsData.VSync):
                SelectById(_vSyncOptionBtn, value.AsInt32());
                break;

            case nameof(SettingsData.ResourcePackId):
                RefreshPackSettings();
                break;
        }
    }

    private void OnSettingsApplied() => RefreshUI();

    private void OnImportClicked()
    {
        FileDialogManager.Instance.ShowOpenDialog(
            (string path) =>
            {
                if(string.IsNullOrEmpty(path)) return;
                // 导入资源包
                string id = ResourcePackLoader.CopyToLocal(path);

                // 更新设置
                GameSettings.Instance.Set(nameof(SettingsData.ResourcePackId), id);

                // RefreshUI();
            }
        );
    }

    // ---------- 从 Settings 刷新整个面板 ----------
    private void RefreshUI()
    {
        var settings = GameSettings.Instance.Current;

        SelectById(_vSyncOptionBtn, (int)settings.VSync);

        // 资源包
        RefreshPackSettings();

        // _resourcePackEdit.Text = settings.ResourcePackId ?? "";
        // _fullscreenCheck.ButtonPressed = cur.Fullscreen;
        // _resolutionOption.Select(cur.ResolutionIndex);
    }

    /// <summary>
    /// 刷新资源包相关设置的UI
    /// </summary>
    private void RefreshPackSettings()
    {
        _packOptionBtn.Clear();
        List<ValueTuple<string, string>> packList = ResourcePackLoader.GetPackList();
        if(packList != null)
        {
            for(int i = 0; i < packList.Count; i++)
            {
                (string id, string name) = packList[i];
                _packOptionBtn.AddItem(name);

                _packOptionBtn.SetItemMetadata(i, id);

                // 当前选中
                if(id == GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId)))
                {
                    _packOptionBtn.Select(i);
                }
            }
        }

        string packId = GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId));
        if(string.IsNullOrEmpty(packId)) return;

        ResourcePack pack = ResourcePackLoader.LoadFromLocal(packId);
        if(pack != null)
        {
            _packOverview.Overview(pack);
        }
        

        GD.Print($"[{Name}] 成功刷新资源包设置界面");
    }

    // ---------- 辅助方法 ----------
    private static void SelectById(OptionButton btn, int id)
    {
        for (int i = 0; i < btn.ItemCount; i++)
        {
            if (btn.GetItemId(i) == id)
            {
                btn.Select(i);
                return;
            }
        }
    }

    private static string FormatVSyncName(DisplayServer.VSyncMode mode) => mode switch
    {
        DisplayServer.VSyncMode.Disabled => "关闭 (Disabled)",
        DisplayServer.VSyncMode.Enabled  => "开启 (Enabled)",
        DisplayServer.VSyncMode.Adaptive => "自适应 (Adaptive)",
        DisplayServer.VSyncMode.Mailbox  => "信箱 (Mailbox)",
        _ => mode.ToString()
    };
}