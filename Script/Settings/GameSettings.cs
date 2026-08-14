using Godot;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

public partial class GameSettings : Node
{
    public static GameSettings Instance { get; private set; }

    // 当前运行时设置
    public SettingsData Current { get; private set; }

    // 变更事件：key = 属性名，value = 新值
    [Signal]
    public delegate void SettingChangedEventHandler(string key, Variant value);
    
    // 批量变更完成事件（用于一次性应用多个设置）
    [Signal]
    public delegate void SettingsAppliedEventHandler();

    private const string SettingsPath = "user://settings/settings.cfg";
    private SettingsData _defaultSettings;

    public override void _Ready()
    {
        Instance = this;
        _defaultSettings = new SettingsData();
        if (!Godot.FileAccess.FileExists(SettingsPath))
        {
            GD.Print($"[{Name}] 不存在设置文件, 将会新建一个");
            CreateNew();
        }
        Load();
        
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public T Get<[MustBeVariant] T>(string key)
    {
        return Current.Get(key).As<T>();
    }

    public void Set<[MustBeVariant] T>(string key, T value)
    {
        Variant newVariant = Variant.From(value);
        Variant currentVariant = Current.Get(key);
        
        if (currentVariant.Equals(newVariant)) return;

        Current.Set(key, newVariant);
        EmitSignal(SignalName.SettingChanged, key, newVariant);
        SaveDeferred();
    }

    // 为常用设置提供强类型属性，兼顾便利性和类型安全
    public string ResourcePackId
    {
        get => Current.ResourcePackId;
        set => Set(nameof(Current.ResourcePackId), value);
    }

    // 加载设置
    public void Load()
    {
        Current = new SettingsData();
        ConfigFile config = new ConfigFile();

        Error err = config.Load(SettingsPath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[{Name}] 使用默认设置(首次运行或读取失败: {err})");
            ApplyToEngine(); // 应用默认值到引擎
            return;
        }

        // 反射自动加载所有属性，避免手动写一堆代码
        // 只获取当前类声明的属性，排除 Resource / GodotObject 继承来的属性
        foreach (PropertyInfo prop in Current.GetType().GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!prop.CanWrite || !prop.CanRead) continue;

            string section = "Game"; // 可按类型分 Section
            if (config.HasSectionKey(section, prop.Name))
            {
                Variant value = config.GetValue(section, prop.Name);
                // 类型安全转换
                try
                {
                    // object typedValue = Convert.ChangeType(value.Obj, prop.PropertyType);
                    // prop.SetValue(Current, typedValue);
                    Current.Set(prop.Name, value);
                }
                catch (Exception e)
                {
                    GD.PushWarning($"[{Name}] 加载设置 {prop.Name} 失败: {e.Message}");
                }
            }
        }

        ApplyToEngine();
        EmitSignal(SignalName.SettingsApplied);
    }

    public void CreateNew()
    {
        Current = _defaultSettings.Clone();
        EnsureDirectoryExists();
        SaveInternal();
        GD.Print($"[{Name}] 新建设置文件成功");
    }

    /// <summary>确保设置文件所在目录存在</summary>
    private void EnsureDirectoryExists()
    {
        string dir = SettingsPath.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dir))
        {
            Error err = DirAccess.MakeDirRecursiveAbsolute(dir);
            if (err != Error.Ok)
            {
                GD.PushError($"[{Name}] 创建目录失败 {dir}: {err}");
            }
        }
    }

    // 保存设置
    public void Save()
    {
        EnsureDirectoryExists();
        SaveInternal();
        GD.Print($"[{Name}] 设置已保存");
    }

    private void SaveInternal()
    {
        ConfigFile config = new ConfigFile();
        
        foreach (PropertyInfo prop in Current.GetType().GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!prop.CanWrite || !prop.CanRead) continue;
            config.SetValue("Game", prop.Name, Current.Get(prop.Name));
        }

        Error err = config.Save(SettingsPath);
        if (err != Error.Ok)
        {
            GD.PushError($"[{Name}] 保存设置失败: {err}");
        }
    }

    private double _saveDelay = 0;
    private void SaveDeferred()
    {
        _saveDelay = 0.5;
    }

    public override void _Process(double delta)
    {
        if (_saveDelay > 0)
        {
            _saveDelay -= delta;
            if (_saveDelay <= 0)
            {
                Save();
            }
        }
    }

    // 将设置实际应用到引擎（分辨率、音量等）
    public void ApplyToEngine()
    {
        // 音频
        // AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Current.MasterVolume));
        // AudioServer.SetBusVolumeDb(1, Mathf.LinearToDb(Current.MusicVolume));
        // AudioServer.SetBusVolumeDb(2, Mathf.LinearToDb(Current.SfxVolume));

        // 显示
        // DisplayServer.WindowSetMode(
        //     Current.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed
        // );
        DisplayServer.WindowSetVsyncMode(Current.VSync);

        // 语言
        // TranslationServer.SetLocale(Current.Language);
    }

    // 重置为默认
    public void ResetToDefault()
    {
        Current = _defaultSettings.Clone();
        ApplyToEngine();
        Save();
        EmitSignal(SignalName.SettingsApplied);
    }
}