using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public partial class SettingsPanel : Control
{
    // 用于取消正在进行的扫描任务（可选）
    private CancellationTokenSource _cts;
    private readonly Dictionary<string, Button> _resetButtons = new();
    private const string SettingKeyMeta = "setting_key";

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
    [Export] private Button _clearCasheBtn;
    [Export] private Label _userDataDirLabel;
    [Export] private Label _casheSizeLabel;

    [Export] private ResourcePackOverview _packOverview;

    // ---------------- 播放设置 ----------------
    [Export] private CheckButton _mhHighlightBtn;
    [Export] private SpinBox _noteScaleEditorEdit;
    [Export] private SpinBox _noteScalePlayEdit;

    // ---------------- 编辑器设置 ----------------
    [Export] private ColorPickerButton _verLineColorPicker;
    [Export] private ColorPickerButton _horSubLineColorPicker;
    [Export] private ColorPickerButton _horLineColorPicker;
    [Export] private ColorPickerButton _groundLineColorPicker;

    [Export] private SpinBox _verLineWidthEdit;
    [Export] private SpinBox _horSubLineWidthEdit;
    [Export] private SpinBox _horLineWidthEdit;
    [Export] private SpinBox _groundLineWidthEdit;

    // ---------------- 运行日志 ----------------
    [Export] private Button _shareLogBtn;
    [Export] private Button _exportLogBtn;


    // =============== 绑定注册框架 ===============
    // string → 如何把 Variant 刷到 UI 上
    private readonly Dictionary<string, Action> _uiPreparers = new();
    private readonly Dictionary<string, Action<Variant>> _uiUpdaters = new();
    private readonly List<Action> _unbindActions = new();

    // 在 Set 之后需要额外处理的动作
    private static readonly HashSet<string> _engineAppliedKeys = new()
    {
        nameof(SettingsData.VSync),
        nameof(SettingsData.MaxFps),
    };

    /// <summary>把一个控件注册为某个设置项的 UI 表示</summary>
    private void RegisterSetting(string key, Action<Variant> applyToUI, Action unbind, Action prepare = null)
    {
        if (prepare != null) _uiPreparers[key] = prepare;
        _uiUpdaters[key] = applyToUI;
        _unbindActions.Add(unbind);

        prepare?.Invoke(); // 先填选项
        applyToUI(GameSettings.Instance.GetVariant(key)); // 再刷值
    }

    private void BindSpinBoxInt(SpinBox box, string key)
    {
        Action<Variant> apply = v => box.SetValueNoSignal(v.AsInt32());

        SpinBox.ValueChangedEventHandler handler = v =>
            GameSettings.Instance.Set(key, Mathf.RoundToInt((float)v));
        box.ValueChanged += handler;

        RegisterSetting(key, apply, () => box.ValueChanged -= handler);
    }

    private void BindSpinBoxFloat(SpinBox box, string key, float min = 0.1f)
    {
        Action<Variant> apply = v => box.SetValueNoSignal(v.AsDouble());

        SpinBox.ValueChangedEventHandler handler = v =>
            GameSettings.Instance.Set(key, Mathf.Max(min, (float)v));
        box.ValueChanged += handler;

        RegisterSetting(key, apply, () => box.ValueChanged -= handler);
    }

    private void BindCheckButton(CheckButton btn, string key)
    {
        Action<Variant> apply = v => btn.SetPressedNoSignal(v.AsBool());

        BaseButton.ToggledEventHandler handler = v =>
            GameSettings.Instance.Set(key, v);
        btn.Toggled += handler;

        RegisterSetting(key, apply, () => btn.Toggled -= handler);
    }

    private void BindColorPicker(ColorPickerButton picker, string key)
    {
        Action<Variant> apply = v => picker.Color = v.AsColor();

        ColorPickerButton.ColorChangedEventHandler handler = c =>
            GameSettings.Instance.Set(key, c);
        picker.ColorChanged += handler;

        RegisterSetting(key, apply, () => picker.ColorChanged -= handler);
    }

    private void BindOptionButtonById(OptionButton btn, string key, Action prepare,
                                  Func<int, int> idToValue = null)
    {
        idToValue ??= x => x;
        Action<Variant> apply = v =>
        {
            int target = idToValue(v.AsInt32());
            for (int i = 0; i < btn.ItemCount; i++)
                if (btn.GetItemId(i) == target) { btn.Select(i); return; }
        };

        OptionButton.ItemSelectedEventHandler handler = idx =>
            GameSettings.Instance.Set(key, btn.GetItemId((int)idx));
        btn.ItemSelected += handler;

        RegisterSetting(key, apply, () => btn.ItemSelected -= handler, prepare);
    }

    private void BindPackOptionButton(OptionButton btn, string key, Action prepare)
    {
        Action<Variant> apply = v =>
        {
            string id = v.AsString();
            for (int i = 0; i < btn.ItemCount; i++)
                if (btn.GetItemMetadata(i).As<string>() == id) { btn.Select(i); return; }
        };

        OptionButton.ItemSelectedEventHandler handler = idx =>
            GameSettings.Instance.Set(key, btn.GetItemMetadata((int)idx).As<string>());
        btn.ItemSelected += handler;

        RegisterSetting(key, apply, () => btn.ItemSelected -= handler, prepare);
    }


    public override void _Ready()
    {

        GD.Print(GameSettings.Instance != null);
        GD.Print(GameSettings.Instance.GetVariant(nameof(SettingsData.VerColor)));
        GD.Print(GameSettings.Instance.GetVariant(nameof(SettingsData.VerWidth)));

        if (GameSettings.Instance == null)
        {
            FileLogger.LogErr("[SettingsPanel] GameSettings.Instance 为 null");
            return;
        }

        SetupBindings();
        CreateResetButtons();
        
        // Settings → UI
        GameSettings.Instance.SettingChanged += OnSettingChanged;
        GameSettings.Instance.SettingsApplied += OnSettingsApplied;
        
        _ = RefreshUI();

        _applyBtn.Pressed += () => GameSettings.Instance.Save();
        _confirmBtn.Pressed += () =>
        {
            GameSettings.Instance.Save();
            Visible = false;
        };

        _importPackBtn.Pressed += OnImportClicked;
        _deletePackBtn.Pressed += OnDeletePackClicked;
        _clearCasheBtn.Pressed += () =>
        {
            FileUtil.ClearDir("user://temp_import");
            FileUtil.ClearDir("user://temp_pack");

            _ = RefreshUI();

            FileLogger.Log($"成功清除缓存");
            PopupHelper.Instance.ShowAlert("提示", "成功清除缓存");
        };
        _userDataDirLabel.Text = "用户数据目录: " + ProjectSettings.GlobalizePath("user://");

        VisibilityChanged += () =>
        {
            if (Visible)
            {
                _ = RefreshUI();
            }
            else
            {
                
            }

        };
    }

    public override void _ExitTree()
    {
        // if (GameSettings.Instance != null)
        // {
        //     GameSettings.Instance.SettingChanged -= OnSettingChanged;
        //     GameSettings.Instance.SettingsApplied -= OnSettingsApplied;
        // }

        // if (_verLineColorPicker != null) _verLineColorPicker.ColorChanged -= OnVerLineColorChanged;
        // if (_horSubLineColorPicker != null) _horSubLineColorPicker.ColorChanged -= OnHorSubLineColorChanged;
        // if (_horLineColorPicker != null) _horLineColorPicker.ColorChanged -= OnHorLineColorChanged;
        // if (_groundLineColorPicker != null) _groundLineColorPicker.ColorChanged -= OnGroundLineColorChanged;
        // if (_verLineWidthEdit != null) _verLineWidthEdit.ValueChanged -= OnVerLineWidthChanged;
        // if (_horSubLineWidthEdit != null) _horSubLineWidthEdit.ValueChanged -= OnHorSubLineWidthChanged;
        // if (_horLineWidthEdit != null) _horLineWidthEdit.ValueChanged -= OnHorLineWidthChanged;
        // if (_groundLineWidthEdit != null) _groundLineWidthEdit.ValueChanged -= OnGroundLineWidthChanged;

        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.SettingChanged -= OnSettingChanged;
            GameSettings.Instance.SettingsApplied -= OnSettingsApplied;
        }
        foreach (var unbind in _unbindActions) unbind();
        _unbindActions.Clear();
        _uiUpdaters.Clear();
    }

    private void SetupBindings()
    {
        // ---- 显示 ----
        BindOptionButtonById(_vSyncOptionBtn, nameof(SettingsData.VSync), FillVSyncOptions);
        BindSpinBoxInt    (_maxFpsEdit,      nameof(SettingsData.MaxFps));

        // ---- 资源包 ----
        BindPackOptionButton(_packOptionBtn, nameof(SettingsData.ResourcePackId), FillPackOptions);
        BindCheckButton     (_useDefaultResourceBtn, nameof(SettingsData.UseDefaultResource));

        // ---- 播放 ----
        BindCheckButton(_mhHighlightBtn, nameof(SettingsData.UseMultiholdHighlight));

        // ---- 编辑器 ----
        BindColorPicker  (_verLineColorPicker,   nameof(SettingsData.VerColor));
        BindColorPicker  (_horSubLineColorPicker,nameof(SettingsData.HorSubColor));
        BindColorPicker  (_horLineColorPicker,   nameof(SettingsData.HorColor));
        BindColorPicker  (_groundLineColorPicker,nameof(SettingsData.GroundLineColor));

        BindSpinBoxFloat(_verLineWidthEdit,    nameof(SettingsData.VerWidth));
        BindSpinBoxFloat(_horSubLineWidthEdit, nameof(SettingsData.HorSubWidth));
        BindSpinBoxFloat(_horLineWidthEdit,    nameof(SettingsData.HorWidth));
        BindSpinBoxFloat(_groundLineWidthEdit, nameof(SettingsData.GroundLineWidth));
    }

    private void CreateResetButtons()
    {
        HashSet<string> resettableKeys = typeof(SettingsData)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => Attribute.IsDefined(property, typeof(ResettableSettingAttribute)))
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<Control> settingControls = new();
        CollectSettingControls(this, settingControls);

        Texture2D resetIcon = GD.Load<Texture2D>("res://Textures/刷新.png");
        foreach (Control control in settingControls)
        {
            string key = control.GetMeta(SettingKeyMeta).AsString();
            if (!resettableKeys.Contains(key))
            {
                continue;
            }

            if (_resetButtons.ContainsKey(key))
            {
                FileLogger.LogWarning($"[{Name}] 设置 {key} 对应了多个重置控件");
                continue;
            }

            if (control.GetParent() is not Godot.Container row)
            {
                FileLogger.LogWarning($"[{Name}] 设置 {key} 的输入控件不在 Container 中");
                continue;
            }

            Button button = new()
            {
                Name = $"Reset{key}",
                CustomMinimumSize = new Vector2(60, 0),
                Icon = resetIcon,
                IconAlignment = HorizontalAlignment.Center,
                ExpandIcon = true,
                TooltipText = "恢复默认值"
            };
            button.Pressed += () => GameSettings.Instance.ResetProperty(key);

            row.AddChild(button);
            row.MoveChild(button, control.GetIndex() + 1);
            _resetButtons.Add(key, button);
        }

        foreach (string key in resettableKeys)
        {
            if (!_resetButtons.ContainsKey(key))
            {
                FileLogger.LogWarning($"[{Name}] 可重置设置 {key} 没有对应的 UI 控件");
            }
        }
    }

    private static void CollectSettingControls(Node parent, List<Control> controls)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is Control control && control.HasMeta(SettingKeyMeta))
            {
                controls.Add(control);
            }

            CollectSettingControls(child, controls);
        }
    }

    // ---------- 绑定：UI 修改 → Settings ----------
    // private void BindEvents()
    // {
    //     _vSyncOptionBtn.ItemSelected += (long idx) =>
    //     {
    //         int id = _vSyncOptionBtn.GetItemId((int)idx);
    //         GameSettings.Instance.Set(nameof(SettingsData.VSync), (DisplayServer.VSyncMode)id);
    //     };

    //     _packOptionBtn.ItemSelected += (long index) =>
    //     {
    //         string id = _packOptionBtn.GetItemMetadata((int)index).As<string>();
    //         GameSettings.Instance.Set(nameof(SettingsData.ResourcePackId), id);
    //     };

    //     _useDefaultResourceBtn.Toggled += (bool value) =>
    //     {
    //         GameSettings.Instance.Set(nameof(SettingsData.UseDefaultResource), value);
    //     };

    //     _maxFpsEdit.ValueChanged += (double value) =>
    //     {
    //         int intValue = Mathf.RoundToInt(value);
    //         GameSettings.Instance.Set(nameof(SettingsData.MaxFps), intValue);
    //     };

    //     _mhHighlightBtn.Toggled += (bool value) =>
    //     {
    //         GameSettings.Instance.Set(nameof(SettingsData.UseMultiholdHighlight), value);
    //     };

    //     _verLineColorPicker.ColorChanged += OnVerLineColorChanged;
    //     _horSubLineColorPicker.ColorChanged += OnHorSubLineColorChanged;
    //     _horLineColorPicker.ColorChanged += OnHorLineColorChanged;
    //     _groundLineColorPicker.ColorChanged += OnGroundLineColorChanged;
    //     _verLineWidthEdit.ValueChanged += OnVerLineWidthChanged;
    //     _horSubLineWidthEdit.ValueChanged += OnHorSubLineWidthChanged;
    //     _horLineWidthEdit.ValueChanged += OnHorLineWidthChanged;
    //     _groundLineWidthEdit.ValueChanged += OnGroundLineWidthChanged;

    //     if (_shareLogBtn != null)
    //     {
    //         _shareLogBtn.Pressed += OnShareLogClicked;
    //     }
    //     else
    //     {
    //         FileLogger.LogWarning("[SettingsPanel] _shareLogBtn 未绑定，无法分享日志");
    //     }

    //     if( _exportLogBtn != null)
    //     {
    //         _exportLogBtn.Pressed += OnExportLogClicked;
    //     }
    //     else
    //     {
    //         FileLogger.LogWarning("[SettingsPanel] _exportLogBtn 未绑定，无法导出日志");
    //     }

    //     _applyBtn.Pressed += () => GameSettings.Instance.Save();
    //     _confirmBtn.Pressed += () =>
    //     {
    //         GameSettings.Instance.Save();
    //         Visible = false;
    //     };

        
    // }

    // private void OnVerLineColorChanged(Color value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.VerColor), value);

    // private void OnHorSubLineColorChanged(Color value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.HorSubColor), value);

    // private void OnHorLineColorChanged(Color value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.HorColor), value);

    // private void OnGroundLineColorChanged(Color value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.GroundLineColor), value);

    // private void OnVerLineWidthChanged(double value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.VerWidth), Mathf.Max(0.1f, (float)value));

    // private void OnHorSubLineWidthChanged(double value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.HorSubWidth), Mathf.Max(0.1f, (float)value));

    // private void OnHorLineWidthChanged(double value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.HorWidth), Mathf.Max(0.1f, (float)value));

    // private void OnGroundLineWidthChanged(double value) =>
    //     GameSettings.Instance.Set(nameof(SettingsData.GroundLineWidth), Mathf.Max(0.1f, (float)value));

    // // ---------- 绑定：Settings 变化 → UI ----------
    // private void BindSignals()
    // {
    //     GameSettings.Instance.SettingChanged += OnSettingChanged;
    //     GameSettings.Instance.SettingsApplied += OnSettingsApplied;
    // }

    private void OnSettingChanged(string key, Variant value)
    {
        UpdateResetButton(key);

        if (_uiUpdaters.TryGetValue(key, out var apply))
            apply(value);

        // 副作用集中处理
        if (_engineAppliedKeys.Contains(key))
            GameSettings.Instance.ApplyToEngine();
        else if (key == nameof(SettingsData.ResourcePackId))
            RefreshPackSettings();

        // switch (key)
        // {
        //     case nameof(SettingsData.VSync):
        //         SelectById(_vSyncOptionBtn, value.AsInt32());
        //         GameSettings.Instance.ApplyToEngine();
        //         break;

        //     case nameof(SettingsData.ResourcePackId):
        //         RefreshPackSettings();
        //         break;
            
        //     case nameof(SettingsData.MaxFps):
        //         _maxFpsEdit.Value = GameSettings.Instance.Get<int>(nameof(SettingsData.MaxFps));
        //         GameSettings.Instance.ApplyToEngine();
        //         break;
            
        //     case nameof(SettingsData.UseDefaultResource):
        //         _useDefaultResourceBtn.SetPressedNoSignal(GameSettings.Instance.Get<bool>(nameof(SettingsData.UseDefaultResource)));
        //         break;
            
        //     case nameof(SettingsData.UseMultiholdHighlight):
        //         _mhHighlightBtn.ButtonPressed = GameSettings.Instance.Get<bool>(nameof(SettingsData.UseMultiholdHighlight));
        //         break;

        //     case nameof(SettingsData.VerColor):
        //         _verLineColorPicker.Color = value.AsColor();
        //         break;

        //     case nameof(SettingsData.HorSubColor):
        //         _horSubLineColorPicker.Color = value.AsColor();
        //         break;

        //     case nameof(SettingsData.HorColor):
        //         _horLineColorPicker.Color = value.AsColor();
        //         break;

        //     case nameof(SettingsData.GroundLineColor):
        //         _groundLineColorPicker.Color = value.AsColor();
        //         break;

        //     case nameof(SettingsData.VerWidth):
        //         _verLineWidthEdit.SetValueNoSignal(value.AsDouble());
        //         break;

        //     case nameof(SettingsData.HorSubWidth):
        //         _horSubLineWidthEdit.SetValueNoSignal(value.AsDouble());
        //         break;

        //     case nameof(SettingsData.HorWidth):
        //         _horLineWidthEdit.SetValueNoSignal(value.AsDouble());
        //         break;

        //     case nameof(SettingsData.GroundLineWidth):
        //         _groundLineWidthEdit.SetValueNoSignal(value.AsDouble());
        //         break;
        // }
    }

    private void UpdateResetButton(string key)
    {
        if (_resetButtons.TryGetValue(key, out Button button))
        {
            button.Visible = !GameSettings.Instance.IsPropertyDefault(key);
        }
    }

    private void UpdateAllResetButtons()
    {
        foreach (string key in _resetButtons.Keys)
        {
            UpdateResetButton(key);
        }
    }

    private void OnSettingsApplied() => _ = RefreshUI();

    private void OnImportClicked()
    {
        FileDialogManager.Instance.ShowOpenDialog(
            (string[] paths) =>
            {
                string path = paths[0];
                
                if(string.IsNullOrEmpty(path)) return;
                // 导入资源包
                string id = ResourcePackLoader.CopyToLocal(path);

                // 更新设置
                GameSettings.Instance.Set(nameof(SettingsData.ResourcePackId), id);

                // RefreshUI();
            }
        );
    }

    // ---------- 运行日志 ----------

    /// <summary>
    /// 导出当前运行日志：复制到 user://export/ 下并通过 ShareHelper 调用系统分享。
    /// Android 上 ShareFileProvider 暴露的是 files 根目录，因此 user:// 下的文件都可分享。
    /// </summary>
    private void OnShareLogClicked()
    {
        FileLogger.Flush(); // 确保日志已落盘

        string logPath = FileLogger.GetCurrentLogPath();
        if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
        {
            FileLogger.LogErr("[SettingsPanel] 未找到可导出的日志文件");
            PopupHelper.Instance.ShowAlert("提示", "未找到可导出的日志文件");
            return;
        }

        string exportPath;
        try
        {
            exportPath = CopyLogToExportDir(logPath);
        }
        catch (Exception ex)
        {
            FileLogger.LogErr($"[SettingsPanel] 导出日志失败: {ex.Message}");
            PopupHelper.Instance.ShowAlert("提示", $"导出日志失败：{ex.Message}");
            return;
        }

        if (ShareHelper.Instance == null || !ShareHelper.IsAvailable)
        {
            FileLogger.Log("[SettingsPanel] 当前平台不支持系统分享，日志已导出但未分享");
            PopupHelper.Instance.ShowAlert("提示", $"当前平台不支持系统分享，日志已保存至：\n{exportPath}");
            return;
        }

        ShareHelper.Instance.ShareFile(
            exportPath,
            "text/plain",
            "运行日志",
            "HPhi Editor 运行日志",
            $"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}"
        );

        PopupHelper.Instance.ShowAlert("提示", $"日志已发起分享：\n{exportPath}");
    }

    private void OnExportLogClicked()
    {
        FileLogger.Flush(); // 确保日志已落盘
        
        string logPath = FileLogger.GetCurrentLogPath();
        if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
        {
            FileLogger.LogErr("[SettingsPanel] 未找到可导出的日志文件");
            PopupHelper.Instance.ShowAlert("提示", "未找到可导出的日志文件");
            return;
        }

        string exportPath;
        try
        {
            exportPath = CopyLogToExportDir(logPath);
        }
        catch (Exception ex)
        {
            FileLogger.LogErr($"[SettingsPanel] 导出日志失败: {ex.Message}");
            PopupHelper.Instance.ShowAlert("提示", $"导出日志失败：{ex.Message}");
            return;
        }

        FileLogger.Log($"[SettingsPanel] 日志已导出至：{exportPath}");
        // PopupHelper.Instance.ShowAlert("提示", $"日志已导出至：\n{exportPath}");
        
        FileDialogManager.Instance.ShowSaveDialog(
            onFileSelected: (string path) =>
            {
                if(string.IsNullOrEmpty(path)) return;
                FileUtil.CopyFile(exportPath, path);
            },
            defaultName: exportPath.GetFile()
        );
    }

    /// <summary>
    /// 把当前日志复制到 user://export/，返回可直接用于分享的绝对路径。
    /// </summary>
    private static string CopyLogToExportDir(string logPath)
    {
        const string exportDir = "user://export";
        DirAccess.MakeDirRecursiveAbsolute(exportDir);

        string fileName = $"HPhiEditor_Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
        string targetVirtualPath = $"{exportDir}/{fileName}";
        string targetAbsPath = ProjectSettings.GlobalizePath(targetVirtualPath);

        File.Copy(logPath, targetAbsPath, true);
        return targetAbsPath;
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

        _ = RefreshUI();
    }

    // ---------- 从 Settings 刷新整个面板 ----------
    private async Task RefreshUI()
    {
        try
        {
            // 先重建所有 OptionButton 的选项
            foreach (var prepare in _uiPreparers.Values)
                prepare();

            // 再刷新所有绑定值
            foreach (var (key, apply) in _uiUpdaters)
                apply(GameSettings.Instance.GetVariant(key));

            // 剩下的：非绑定型 UI（重置按钮状态、缓存大小扫描）
            UpdateAllResetButtons();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _ = ScanCacheSizeAsync(_cts.Token);

            // SettingsData settings = GameSettings.Instance.Current;

            // // VSync
            // _vSyncOptionBtn.Clear();
            // foreach (DisplayServer.VSyncMode mode in Enum.GetValues<DisplayServer.VSyncMode>())
            // {
            //     _vSyncOptionBtn.AddItem(FormatVSyncName(mode), (int)mode);
            // }
            // SelectById(_vSyncOptionBtn, (int)settings.VSync);

            // // 资源包相关
            // RefreshPackSettings();

            // // 最大帧率
            // _maxFpsEdit.Value = settings.MaxFps;

            // // 播放设置
            // _mhHighlightBtn.ButtonPressed = settings.UseMultiholdHighlight;

            // // 网格设置
            // _verLineColorPicker.Color = settings.VerColor;
            // _horSubLineColorPicker.Color = settings.HorSubColor;
            // _horLineColorPicker.Color = settings.HorColor;
            // _groundLineColorPicker.Color = settings.GroundLineColor;

            // _verLineWidthEdit.SetValueNoSignal(settings.VerWidth);
            // _horSubLineWidthEdit.SetValueNoSignal(settings.HorSubWidth);
            // _horLineWidthEdit.SetValueNoSignal(settings.HorWidth);
            // _groundLineWidthEdit.SetValueNoSignal(settings.GroundLineWidth);

            // UpdateAllResetButtons();

            // // 存储空间
            // // 取消之前的扫描任务（如果有）
            // _cts?.Cancel();
            // _cts?.Dispose();
            // _cts = new CancellationTokenSource();

            // // 启动异步扫描
            // _ = ScanCacheSizeAsync(_cts.Token);

            // _resourcePackEdit.Text = settings.ResourcePackId ?? "";
            // _fullscreenCheck.ButtonPressed = cur.Fullscreen;
            // _resolutionOption.Select(cur.ResolutionIndex);

            FileLogger.Log($"[{Name}] 成功重建UI");
        }
        catch (Exception ex)
        {
            FileLogger.LogErr($"[SettingsPanel] RefreshUI 发生异常: {ex.Message}");
        }
        
    }

    /// <summary>
    /// 刷新资源包相关设置的UI
    /// </summary>
    private void RefreshPackSettings()
    {
        // 资源包选项
        // _packOptionBtn.Clear();
        // List<ValueTuple<string, string>> packList = ResourcePackLoader.GetPackList();
        // if (packList != null)
        // {
        //     for (int i = 0; i < packList.Count; i++)
        //     {
        //         (string id, string name) = packList[i];
        //         _packOptionBtn.AddItem(name);

        //         _packOptionBtn.SetItemMetadata(i, id);

        //         // 当前选中
        //         if (id == GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId)))
        //         {
        //             _packOptionBtn.Select(i);
        //         }
        //     }
        // }
        FillPackOptions();

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
        // _useDefaultResourceBtn.SetPressedNoSignal(
        //     GameSettings.Instance.Get<bool>(nameof(SettingsData.UseDefaultResource)));

        // FileLogger.Log($"[{Name}] 成功刷新资源包设置界面");

    }

    private void FillVSyncOptions()
    {
        int currentId = (int)GameSettings.Instance.Get<DisplayServer.VSyncMode>(nameof(SettingsData.VSync));
        _vSyncOptionBtn.Clear();
        foreach (DisplayServer.VSyncMode mode in Enum.GetValues<DisplayServer.VSyncMode>())
            _vSyncOptionBtn.AddItem(FormatVSyncName(mode), (int)mode);
        // 注意：Clear 会丢选择，交给 apply 恢复，这里不需要 Select
    }

    private void FillPackOptions()
    {
        _packOptionBtn.Clear();
        foreach (var (id, name) in ResourcePackLoader.GetPackList() ?? new())
        {
            int i = _packOptionBtn.ItemCount;
            _packOptionBtn.AddItem(name);
            _packOptionBtn.SetItemMetadata(i, id);
        }

        // 顺手刷新预览（原来在 RefreshPackSettings 里那段）
        string packId = GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId));
        if (!string.IsNullOrEmpty(packId))
        {
            ResourcePack pack = ResourcePackLoader.LoadFromLocal(packId);
            if (pack != null) _packOverview.Overview(pack);
        }
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

    private async Task ScanCacheSizeAsync(CancellationToken token)
    {
        try
        {
            string[] cacheDirs = new[] { "user://temp_import", "user://temp_pack" };
            long totalSize = await CalculateTotalSizeAsync(
                cacheDirs,
                progress => CallDeferred(nameof(UpdateCacheLabel), progress),
                token
            );
            // 扫描完成，标签已更新为最终大小
        }
        catch (OperationCanceledException)
        {
            FileLogger.Log("缓存扫描已取消");
        }
        catch (Exception ex)
        {
            FileLogger.LogErr($"扫描缓存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 异步获取目录大小，并通过回调实时更新标签
    /// </summary>
    /// <param name="dirPaths">要统计的目录列表（Godot 虚拟路径，如 "user://temp_import"）</param>
    /// <param name="onProgress">每次更新时回调，参数为当前累计字节数</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task<long> CalculateTotalSizeAsync(
        string[] dirPaths,
        Action<long> onProgress,
        CancellationToken cancellationToken = default)
    {
        long total = 0;
        int processedCount = 0;          // 已处理文件数
        const int updateInterval = 20;    // 每处理 20 个文件更新一次 UI

        foreach (string virtualPath in dirPaths)
        {
            string absolutePath = ProjectSettings.GlobalizePath(virtualPath);
            if (!Directory.Exists(absolutePath))
                continue;

            try
            {
                // 使用 EnumerateFiles 流式处理，避免一次性加载所有路径
                IEnumerable<string> files = Directory.EnumerateFiles(absolutePath, "*", SearchOption.AllDirectories);
                foreach (string filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        FileInfo info = new FileInfo(filePath);
                        total += info.Length;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (FileNotFoundException) { }

                    // 定期触发进度更新（避免频繁跨线程调用）
                    processedCount++;
                    if (processedCount % updateInterval == 0)
                    {
                        // 使用 CallDeferred 安全更新 UI
                        // CallDeferred(nameof(UpdateCacheLabel), total);
                        // 或直接 onProgress?.Invoke(total); 但需确保在主线程调用
                        onProgress?.Invoke(total);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (OperationCanceledException) { throw; }
        }

        // 最终更新
        // CallDeferred(nameof(UpdateCacheLabel), total);
        onProgress?.Invoke(total);
        return total;
    }

    // 用于 CallDeferred 调用的方法（必须是无参或单参数，且位于主线程）
    private void UpdateCacheLabel(long sizeInBytes)
    {
        _casheSizeLabel.Text = FormatFileSize(sizeInBytes);
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}