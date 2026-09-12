using Godot;
using System;
using System.IO;
using System.Reflection;

public partial class EditorSettings : Node
{
    // 由 autoload 创建，整个应用只保留一个编辑器设置管理器。
    public static EditorSettings Instance { get; private set; }

    // 每个谱面使用独立配置文件，配置文件位于该谱面的存档目录中。
    public const string SettingsFileName = "editor_settings.cfg";
    private const string SettingsSection = "Editor";
    private const string VersionKey = "Version";

    // Current 始终表示 CurrentChartId 对应的设置。
    public EditorSettingsData Current { get; private set; }
    public string CurrentChartId { get; private set; } = "";

    // 单项修改和整批加载/重置完成时分别通知界面或其他编辑器模块。
    [Signal]
    public delegate void SettingChangedEventHandler(string key, Variant value);

    [Signal]
    public delegate void SettingsAppliedEventHandler();

    private EditorSettingsData _defaultSettings;

    public override void _Ready()
    {
        Instance = this;
        _defaultSettings = new EditorSettingsData();
        Current = _defaultSettings.Clone();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public T Get<[MustBeVariant] T>(string key)
    {
        EnsureCurrent();
        return Current.Get(key).As<T>();
    }

    public void Set<[MustBeVariant] T>(string key, T value)
    {
        EnsureCurrent();

        Variant newValue = Variant.From(value);
        Variant currentValue = Current.Get(key);
        if (currentValue.Equals(newValue)) return;

        Current.Set(key, newValue);
        EmitSignal(SignalName.SettingChanged, key, newValue);
    }

    public void Load(string chartId)
    {
        // 由调用方显式指定谱面，避免设置管理器依赖 Global 中的隐式状态。
        ValidateChartId(chartId);

        if (CurrentChartId == chartId && Current != null) return;

        CurrentChartId = chartId;
        Current = _defaultSettings.Clone();

        // 缺少配置文件时直接使用默认值，并立即创建文件，保证后续设置有固定落点。
        string settingsPath = GetSettingsPath(chartId);
        if (!Godot.FileAccess.FileExists(settingsPath))
        {
            Save();
            EmitSignal(SignalName.SettingsApplied);
            return;
        }

        ConfigFile config = new ConfigFile();
        Error error = config.Load(settingsPath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"[{Name}] 读取编辑器设置失败，使用默认值: {error}");
            Save();
            EmitSignal(SignalName.SettingsApplied);
            return;
        }

        // 检查配置文件版本
        int version = config.HasSectionKey(SettingsSection, VersionKey)
            ? (int)config.GetValue(SettingsSection, VersionKey, 0)
            : 0;
        Migrate(config, version);

        // 加载设置项
        LoadProperties(config);

        EmitSignal(SignalName.SettingsApplied);
    }

    /// <summary>将当前谱面的编辑器设置写入其独立配置文件。</summary>
    public void Save()
    {
        EnsureCurrent();
        if (string.IsNullOrEmpty(CurrentChartId)) return;

        string settingsPath = GetSettingsPath(CurrentChartId);
        EnsureDirectoryExists(settingsPath.GetBaseDir());

        // 写入配置文件
        ConfigFile config = new ConfigFile();
        // 版本信息
        config.SetValue(SettingsSection, VersionKey, EditorSettingsData.CurrentVersion);
        // 设置项
        foreach (PropertyInfo property in GetSettingsProperties())
        {
            config.SetValue(SettingsSection, property.Name, Current.Get(property.Name));
        }

        Error error = config.Save(settingsPath);
        if (error != Error.Ok)
        {
            GD.PushError($"[{Name}] 保存编辑器设置失败: {error}");
        }
    }

    public void ResetToDefault()
    {
        EnsureCurrent();
        Current = _defaultSettings.Clone();
        Save();
        EmitSignal(SignalName.SettingsApplied);
    }

    public void Delete(string chartId)
    {
        // 谱面删除时同步清理配置；配置不应脱离所属谱面单独保留。
        ValidateChartId(chartId);
        string settingsPath = GetSettingsPath(chartId);
        if (Godot.FileAccess.FileExists(settingsPath))
        {
            DirAccess.RemoveAbsolute(settingsPath);
        }

        if (CurrentChartId == chartId)
        {
            CurrentChartId = "";
            Current = _defaultSettings.Clone();
        }
    }

    private void LoadProperties(ConfigFile config)
    {
        // 只要在 EditorSettingsData 中增加公开属性，此处无需新增序列化分支。
        foreach (PropertyInfo property in GetSettingsProperties())
        {
            if (!config.HasSectionKey(SettingsSection, property.Name)) continue;

            try
            {
                Current.Set(property.Name, config.GetValue(SettingsSection, property.Name));
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[{Name}] 加载编辑器设置 {property.Name} 失败: {exception.Message}");
            }
        }
    }

    private void Migrate(ConfigFile config, int version)
    {
        // 未来旧版本字段转换应集中放在这里。
        if (version > EditorSettingsData.CurrentVersion)
        {
            GD.PushWarning($"[{Name}] 编辑器设置版本 {version} 高于当前版本，尝试读取兼容字段");
        }
    }

    private PropertyInfo[] GetSettingsProperties()
    {
        // 仅处理数据模型自身声明的公开属性，排除 Resource/GodotObject 的继承成员。
        return typeof(EditorSettingsData).GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    }

    private string GetSettingsPath(string chartId)
    {
        // 使用 Godot 的 user:// 虚拟路径，保证不同平台使用各自的用户数据目录。
        return Path.Combine("user://ChartSaves", chartId, SettingsFileName);
    }

    private void EnsureDirectoryExists(string directory)
    {
        if (DirAccess.DirExistsAbsolute(directory)) return;

        Error error = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (error != Error.Ok)
        {
            GD.PushError($"[{Name}] 创建编辑器设置目录失败 {directory}: {error}");
        }
    }

    private void ValidateChartId(string chartId)
    {
        if (string.IsNullOrWhiteSpace(chartId) || chartId == "." || chartId == ".."
            || chartId.Contains('/') || chartId.Contains('\\'))
        {
            throw new ArgumentException("ChartId 不能为空或包含路径分隔符", nameof(chartId));
        }
    }

    private void EnsureCurrent()
    {
        if (Current == null) Current = _defaultSettings.Clone();
    }
}
