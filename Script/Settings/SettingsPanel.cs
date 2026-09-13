using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class SettingsPanel : Control
{
    // 用于取消正在进行的扫描任务（可选）
    private CancellationTokenSource _cts;


    // ---------- 控件引用 ----------
    [Export] private OptionButton _vSyncOptionBtn;
    [Export] private Button _resetVSyncBtn;
    [Export] private SpinBox _maxFpsEdit;
    [Export] private Button _resetMaxFpsBtn;

    [Export] private OptionButton _packOptionBtn;
    [Export] private Button _resetPackBtn;
    [Export] private CheckButton _useDefaultResourceBtn;
    [Export] private Button _resetUseDefaultResourceBtn;
    [Export] private Button _importPackBtn;
    [Export] private Button _exportPackBtn;
    [Export] private Button _deletePackBtn;

    [Export] private Button _applyBtn;
    [Export] private Button _confirmBtn;
    [Export] private Button _clearCasheBtn;
    [Export] private Label _casheSizeLabel;

    [Export] private ResourcePackOverview _packOverview;

    [Export] private CheckButton _mhHighlightBtn;
    [Export] private Button _resetMhHighlightBtn;

    // ---------------- 编辑器设置 ----------------
    [Export] private ColorPickerButton _verLineColorPicker;
    [Export] private ColorPickerButton _horSubLineColorPicker;
    [Export] private ColorPickerButton _horLineColorPicker;
    [Export] private ColorPickerButton _groundLineColorPicker;

    [Export] private SpinBox _verLineWidthEdit;
    [Export] private SpinBox _horSubLineWidthEdit;
    [Export] private SpinBox _horLineWidthEdit;
    [Export] private SpinBox _groundLineWidthEdit;

    [Export] private Button _resetVerLineColorBtn;
    [Export] private Button _resetVerLineWidthBtn;
    [Export] private Button _resetHorSubLineColorBtn;
    [Export] private Button _resetHorSubLineWidthBtn;
    [Export] private Button _resetHorLineColorBtn;
    [Export] private Button _resetHorLineWidthBtn;
    [Export] private Button _resetGroundLineColorBtn;
    [Export] private Button _resetGroundLineWidthBtn;

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
        _ = RefreshUI();

        _importPackBtn.Pressed += OnImportClicked;
        _deletePackBtn.Pressed += OnDeletePackClicked;
        _clearCasheBtn.Pressed += () =>
        {
            FileUtil.ClearDir("user://temp_import");
            FileUtil.ClearDir("user://temp_pack");

            _ = RefreshUI();

            GD.Print($"成功清除缓存");
            PopupHelper.Instance.ShowAlert("提示", "成功清除缓存");
        };

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
        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.SettingChanged -= OnSettingChanged;
            GameSettings.Instance.SettingsApplied -= OnSettingsApplied;
        }

        if (_verLineColorPicker != null) _verLineColorPicker.ColorChanged -= OnVerLineColorChanged;
        if (_horSubLineColorPicker != null) _horSubLineColorPicker.ColorChanged -= OnHorSubLineColorChanged;
        if (_horLineColorPicker != null) _horLineColorPicker.ColorChanged -= OnHorLineColorChanged;
        if (_groundLineColorPicker != null) _groundLineColorPicker.ColorChanged -= OnGroundLineColorChanged;
        if (_verLineWidthEdit != null) _verLineWidthEdit.ValueChanged -= OnVerLineWidthChanged;
        if (_horSubLineWidthEdit != null) _horSubLineWidthEdit.ValueChanged -= OnHorSubLineWidthChanged;
        if (_horLineWidthEdit != null) _horLineWidthEdit.ValueChanged -= OnHorLineWidthChanged;
        if (_groundLineWidthEdit != null) _groundLineWidthEdit.ValueChanged -= OnGroundLineWidthChanged;
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

        _mhHighlightBtn.Toggled += (bool value) =>
        {
            GameSettings.Instance.Set(nameof(SettingsData.UseMultiholdHighlight), value);
        };

        _resetVSyncBtn.Pressed += () => ResetProperty(nameof(SettingsData.VSync));
        _resetMaxFpsBtn.Pressed += () => ResetProperty(nameof(SettingsData.MaxFps));
        _resetPackBtn.Pressed += () => ResetProperty(nameof(SettingsData.ResourcePackId));
        _resetUseDefaultResourceBtn.Pressed += () => ResetProperty(nameof(SettingsData.UseDefaultResource));
        _resetMhHighlightBtn.Pressed += () => ResetProperty(nameof(SettingsData.UseMultiholdHighlight));
        _resetVerLineColorBtn.Pressed += () => ResetProperty(nameof(SettingsData.VerColor));
        _resetVerLineWidthBtn.Pressed += () => ResetProperty(nameof(SettingsData.VerWidth));
        _resetHorSubLineColorBtn.Pressed += () => ResetProperty(nameof(SettingsData.HorSubColor));
        _resetHorSubLineWidthBtn.Pressed += () => ResetProperty(nameof(SettingsData.HorSubWidth));
        _resetHorLineColorBtn.Pressed += () => ResetProperty(nameof(SettingsData.HorColor));
        _resetHorLineWidthBtn.Pressed += () => ResetProperty(nameof(SettingsData.HorWidth));
        _resetGroundLineColorBtn.Pressed += () => ResetProperty(nameof(SettingsData.GroundLineColor));
        _resetGroundLineWidthBtn.Pressed += () => ResetProperty(nameof(SettingsData.GroundLineWidth));

        _verLineColorPicker.ColorChanged += OnVerLineColorChanged;
        _horSubLineColorPicker.ColorChanged += OnHorSubLineColorChanged;
        _horLineColorPicker.ColorChanged += OnHorLineColorChanged;
        _groundLineColorPicker.ColorChanged += OnGroundLineColorChanged;
        _verLineWidthEdit.ValueChanged += OnVerLineWidthChanged;
        _horSubLineWidthEdit.ValueChanged += OnHorSubLineWidthChanged;
        _horLineWidthEdit.ValueChanged += OnHorLineWidthChanged;
        _groundLineWidthEdit.ValueChanged += OnGroundLineWidthChanged;

        _applyBtn.Pressed += () => GameSettings.Instance.Save();
        _confirmBtn.Pressed += () =>
        {
            GameSettings.Instance.Save();
            Visible = false;
        };

        
    }

    private static void ResetProperty(string key) => GameSettings.Instance.ResetProperty(key);

    private void OnVerLineColorChanged(Color value) =>
        GameSettings.Instance.Set(nameof(SettingsData.VerColor), value);

    private void OnHorSubLineColorChanged(Color value) =>
        GameSettings.Instance.Set(nameof(SettingsData.HorSubColor), value);

    private void OnHorLineColorChanged(Color value) =>
        GameSettings.Instance.Set(nameof(SettingsData.HorColor), value);

    private void OnGroundLineColorChanged(Color value) =>
        GameSettings.Instance.Set(nameof(SettingsData.GroundLineColor), value);

    private void OnVerLineWidthChanged(double value) =>
        GameSettings.Instance.Set(nameof(SettingsData.VerWidth), Mathf.Max(0.1f, (float)value));

    private void OnHorSubLineWidthChanged(double value) =>
        GameSettings.Instance.Set(nameof(SettingsData.HorSubWidth), Mathf.Max(0.1f, (float)value));

    private void OnHorLineWidthChanged(double value) =>
        GameSettings.Instance.Set(nameof(SettingsData.HorWidth), Mathf.Max(0.1f, (float)value));

    private void OnGroundLineWidthChanged(double value) =>
        GameSettings.Instance.Set(nameof(SettingsData.GroundLineWidth), Mathf.Max(0.1f, (float)value));

    // // ---------- 绑定：Settings 变化 → UI ----------
    // private void BindSignals()
    // {
    //     GameSettings.Instance.SettingChanged += OnSettingChanged;
    //     GameSettings.Instance.SettingsApplied += OnSettingsApplied;
    // }

    private void OnSettingChanged(string key, Variant value)
    {
        UpdateResetButton(key);

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
            
            case nameof(SettingsData.UseMultiholdHighlight):
                _mhHighlightBtn.ButtonPressed = GameSettings.Instance.Get<bool>(nameof(SettingsData.UseMultiholdHighlight));
                break;

            case nameof(SettingsData.VerColor):
                _verLineColorPicker.Color = value.AsColor();
                break;

            case nameof(SettingsData.HorSubColor):
                _horSubLineColorPicker.Color = value.AsColor();
                break;

            case nameof(SettingsData.HorColor):
                _horLineColorPicker.Color = value.AsColor();
                break;

            case nameof(SettingsData.GroundLineColor):
                _groundLineColorPicker.Color = value.AsColor();
                break;

            case nameof(SettingsData.VerWidth):
                _verLineWidthEdit.SetValueNoSignal(value.AsDouble());
                break;

            case nameof(SettingsData.HorSubWidth):
                _horSubLineWidthEdit.SetValueNoSignal(value.AsDouble());
                break;

            case nameof(SettingsData.HorWidth):
                _horLineWidthEdit.SetValueNoSignal(value.AsDouble());
                break;

            case nameof(SettingsData.GroundLineWidth):
                _groundLineWidthEdit.SetValueNoSignal(value.AsDouble());
                break;
        }
    }

    private void UpdateResetButton(string key)
    {
        Button button = key switch
        {
            nameof(SettingsData.VSync) => _resetVSyncBtn,
            nameof(SettingsData.MaxFps) => _resetMaxFpsBtn,
            nameof(SettingsData.ResourcePackId) => _resetPackBtn,
            nameof(SettingsData.UseDefaultResource) => _resetUseDefaultResourceBtn,
            nameof(SettingsData.UseMultiholdHighlight) => _resetMhHighlightBtn,
            nameof(SettingsData.VerColor) => _resetVerLineColorBtn,
            nameof(SettingsData.VerWidth) => _resetVerLineWidthBtn,
            nameof(SettingsData.HorSubColor) => _resetHorSubLineColorBtn,
            nameof(SettingsData.HorSubWidth) => _resetHorSubLineWidthBtn,
            nameof(SettingsData.HorColor) => _resetHorLineColorBtn,
            nameof(SettingsData.HorWidth) => _resetHorLineWidthBtn,
            nameof(SettingsData.GroundLineColor) => _resetGroundLineColorBtn,
            nameof(SettingsData.GroundLineWidth) => _resetGroundLineWidthBtn,
            _ => null
        };

        if (button != null)
        {
            button.Visible = !GameSettings.Instance.IsPropertyDefault(key);
        }
    }

    private void UpdateAllResetButtons()
    {
        UpdateResetButton(nameof(SettingsData.VSync));
        UpdateResetButton(nameof(SettingsData.MaxFps));
        UpdateResetButton(nameof(SettingsData.ResourcePackId));
        UpdateResetButton(nameof(SettingsData.UseDefaultResource));
        UpdateResetButton(nameof(SettingsData.UseMultiholdHighlight));
        UpdateResetButton(nameof(SettingsData.VerColor));
        UpdateResetButton(nameof(SettingsData.VerWidth));
        UpdateResetButton(nameof(SettingsData.HorSubColor));
        UpdateResetButton(nameof(SettingsData.HorSubWidth));
        UpdateResetButton(nameof(SettingsData.HorColor));
        UpdateResetButton(nameof(SettingsData.HorWidth));
        UpdateResetButton(nameof(SettingsData.GroundLineColor));
        UpdateResetButton(nameof(SettingsData.GroundLineWidth));
    }

    private void OnSettingsApplied() => _ = RefreshUI();

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

        _ = RefreshUI();
    }

    // ---------- 从 Settings 刷新整个面板 ----------
    private async Task RefreshUI()
    {
        try
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

            // 播放设置
            _mhHighlightBtn.ButtonPressed = settings.UseMultiholdHighlight;

            // 网格设置
            _verLineColorPicker.Color = settings.VerColor;
            _horSubLineColorPicker.Color = settings.HorSubColor;
            _horLineColorPicker.Color = settings.HorColor;
            _groundLineColorPicker.Color = settings.GroundLineColor;

            _verLineWidthEdit.SetValueNoSignal(settings.VerWidth);
            _horSubLineWidthEdit.SetValueNoSignal(settings.HorSubWidth);
            _horLineWidthEdit.SetValueNoSignal(settings.HorWidth);
            _groundLineWidthEdit.SetValueNoSignal(settings.GroundLineWidth);

            UpdateAllResetButtons();

            // 存储空间
            // 取消之前的扫描任务（如果有）
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            // 启动异步扫描
            _ = ScanCacheSizeAsync(_cts.Token);

            // _resourcePackEdit.Text = settings.ResourcePackId ?? "";
            // _fullscreenCheck.ButtonPressed = cur.Fullscreen;
            // _resolutionOption.Select(cur.ResolutionIndex);

            GD.Print($"[{Name}] 成功重建UI");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SettingsPanel] RefreshUI 发生异常: {ex.Message}");
        }
        
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
            GD.Print("缓存扫描已取消");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"扫描缓存失败: {ex.Message}");
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