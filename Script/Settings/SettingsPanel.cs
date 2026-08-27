using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

public partial class SettingsPanel : Control
{
    // ---------- 控件引用 ----------
    [Export] private OptionButton _vSyncOptionBtn;
    [Export] private SpinBox _maxFpsEdit;

    [Export] private OptionButton _packOptionBtn;
    [Export] private CheckButton _useDefaultResourceBtn;
    [Export] private Button _importPackBtn;
    [Export] private Button _exportPackBtn;
    [Export] private Button _deletePackBtn;

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
        _deletePackBtn.Pressed += OnDeletePackClicked;

        VisibilityChanged += () =>
        {
            if(!Visible) return;

            // InitOptions();
            RefreshUI();
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

        _useDefaultResourceBtn.Toggled += (bool value) =>
        {
            GameSettings.Instance.Set(nameof(SettingsData.UseDefaultResource), value);
        };

        _maxFpsEdit.ValueChanged += (double value) =>
        {
            int intValue = Mathf.RoundToInt(value);
            GameSettings.Instance.Set(nameof(SettingsData.MaxFps), intValue);
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
                GameSettings.Instance.ApplyToEngine();
                break;

            case nameof(SettingsData.ResourcePackId):
                RefreshPackSettings();
                break;
            
            case nameof(SettingsData.MaxFps):
                _maxFpsEdit.Value = GameSettings.Instance.Get<int>(nameof(SettingsData.MaxFps));
                GameSettings.Instance.ApplyToEngine();
                break;
            
            case nameof(SettingsData.UseDefaultResource):
                _useDefaultResourceBtn.SetPressedNoSignal(GameSettings.Instance.Get<bool>(nameof(SettingsData.UseDefaultResource)));
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

    private void OnDeletePackClicked()
    {
        // 获取当前选中资源包id
        string id = GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId));

        // ---------------- 选择字典序上一个资源包 ----------------
        List<ValueTuple<string, string>> packs = ResourcePackLoader.GetPackList();

        // 1. 提取所有 ID 并排序
        List<string> sortedIds = packs.Select(t => t.Item1).OrderBy(id => id, StringComparer.Ordinal).ToList();

        // 2. 查找目标 ID 的索引（唯一性假设）
        int index = sortedIds.IndexOf(id);
        if (index == -1)
            throw new ArgumentException($"ID '{id}' not found in the list.");

        // 3. 返回前一个 ID，如果是第一个就返回下一个
        string lastId;
        if(index == 0) lastId = sortedIds[index + 1];
        else lastId = sortedIds[index - 1];

        ResourcePackLoader.DeletePack(id);

        // 选择上一个资源包
        GameSettings.Instance.Set<string>(nameof(SettingsData.ResourcePackId), lastId);

        RefreshUI();
    }

    // ---------- 从 Settings 刷新整个面板 ----------
    private void RefreshUI()
    {
        SettingsData settings = GameSettings.Instance.Current;

        // VSync
        _vSyncOptionBtn.Clear();
        foreach (DisplayServer.VSyncMode mode in Enum.GetValues<DisplayServer.VSyncMode>())
        {
            _vSyncOptionBtn.AddItem(FormatVSyncName(mode), (int)mode);
        }
        SelectById(_vSyncOptionBtn, (int)settings.VSync);

        // 资源包相关
        RefreshPackSettings();

        // 最大帧率
        _maxFpsEdit.Value = settings.MaxFps;

        // _resourcePackEdit.Text = settings.ResourcePackId ?? "";
        // _fullscreenCheck.ButtonPressed = cur.Fullscreen;
        // _resolutionOption.Select(cur.ResolutionIndex);

        GD.Print($"[{Name}] 成功重建UI");
    }

    /// <summary>
    /// 刷新资源包相关设置的UI
    /// </summary>
    private void RefreshPackSettings()
    {
        // 资源包选项
        _packOptionBtn.Clear();
        List<ValueTuple<string, string>> packList = ResourcePackLoader.GetPackList();
        if (packList != null)
        {
            for (int i = 0; i < packList.Count; i++)
            {
                (string id, string name) = packList[i];
                _packOptionBtn.AddItem(name);

                _packOptionBtn.SetItemMetadata(i, id);

                // 当前选中
                if (id == GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId)))
                {
                    _packOptionBtn.Select(i);
                }
            }
        }

        // 资源包预览
        string packId = GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId));
        if (!string.IsNullOrEmpty(packId))
        {
            ResourcePack pack = ResourcePackLoader.LoadFromLocal(packId);
            if (pack != null)
            {
                _packOverview.Overview(pack);
            }
        }

        // 是否使用默认资源包
        _useDefaultResourceBtn.SetPressedNoSignal(
            GameSettings.Instance.Get<bool>(nameof(SettingsData.UseDefaultResource)));

        // GD.Print($"[{Name}] 成功刷新资源包设置界面");

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