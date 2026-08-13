using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using HPhiEditorGame.Editor;

/// <summary>
/// 动态设置面板。自动反射 SettingsData，按类型生成对应编辑器，
/// 双向绑定 GameSettings，支持实时修改、保存、重置。
/// </summary>
public partial class SettingsPanel : Control
{
    private GameSettings _settings;
    [Export] private VBoxContainer _propertyVBox;
	[Export] private Button _applyButton;
    private readonly Dictionary<string, Control> _editors = new();
    private readonly Dictionary<string, PropertyInfo> _properties = new();

	[Export] private Theme _theme;

    // 分辨率预设（可按需扩展）
    private readonly (string Name, Vector2I Size)[] _resolutions = new[]
    {
        ("1280 x 720",   new Vector2I(1280, 720)),
        ("1920 x 1080",  new Vector2I(1920, 1080)),
        ("2560 x 1440",  new Vector2I(2560, 1440)),
        ("3840 x 2160",  new Vector2I(3840, 2160)),
    };

    public override void _Ready()
    {
        _settings = GameSettings.Instance;
        if (_settings == null)
        {
            GD.PushError("[SettingsPanel] GameSettings.Instance 为 null, 请确保场景中存在 GameSettings 节点");
            return;
        }

        BuildUI();
        BindValues();

		_applyButton.Pressed += OnApplyPressed;

        _settings.SettingChanged += OnSettingChanged;
        _settings.SettingsApplied += OnSettingsApplied;
    }

    public override void _ExitTree()
    {
        if (_settings != null)
        {
            _settings.SettingChanged -= OnSettingChanged;
            _settings.SettingsApplied -= OnSettingsApplied;
        }
    }

    private void BuildUI()
    {
        // // 根边距
        // MarginContainer margin = new MarginContainer();
        // AddChild(margin);
        // margin.AddThemeConstantOverride("margin_left", 28);
        // margin.AddThemeConstantOverride("margin_right", 28);
        // margin.AddThemeConstantOverride("margin_top", 24);
        // margin.AddThemeConstantOverride("margin_bottom", 24);

        // // 滚动区域（防止设置项过多溢出）
        // ScrollContainer scroll = new ScrollContainer
        // {
        //     SizeFlagsHorizontal = SizeFlags.ExpandFill,
        //     SizeFlagsVertical = SizeFlags.ExpandFill
        // };
        // margin.AddChild(scroll);

        // _content = new VBoxContainer
        // {
        //     SizeFlagsHorizontal = SizeFlags.ExpandFill,
        //     SizeFlagsVertical = SizeFlags.ExpandFill
        // };
        // _content.AddThemeConstantOverride("separation", 14);
        // scroll.AddChild(_content);

        // // 标题
        // Label title = new Label
        // {
        //     Text = "游戏设置",
        //     ThemeTypeVariation = "HeaderLarge",
        //     HorizontalAlignment = HorizontalAlignment.Center
        // };
        // _content.AddChild(title);
        // _content.AddChild(new HSeparator());

        // 按属性动态生成行
        foreach (PropertyInfo prop in typeof(SettingsData).GetProperties())
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            HBoxContainer row = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Begin
            };

            // 左侧标签
            Label label = new Label
            {
                Text = FormatLabelName(prop.Name),
                CustomMinimumSize = new Vector2(180, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            row.AddChild(label);

            // 右侧编辑器
            Control editor = CreateEditor(prop);
            if (editor != null)
            {
                editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(editor);
                _editors[prop.Name] = editor;
                _properties[prop.Name] = prop;
                _propertyVBox.AddChild(row);
            }
        }

        // 底部按钮
        // HBoxContainer btnRow = new HBoxContainer
        // {
        //     SizeFlagsHorizontal = SizeFlags.ExpandFill,
        //     Alignment = BoxContainer.AlignmentMode.End
        // };
        // btnRow.AddThemeConstantOverride("separation", 10);

        // Button resetBtn = new Button { Text = "恢复默认" };
        // resetBtn.Pressed += OnResetPressed;

        // Button applyBtn = new Button { Text = "保存并应用" };
        // applyBtn.Pressed += OnApplyPressed;

        // btnRow.AddChild(resetBtn);
        // btnRow.AddChild(applyBtn);
        // _propertyVBox.AddChild(new HSeparator());
        // _propertyVBox.AddChild(btnRow);
    }

    /// <summary>根据属性类型创建合适的编辑器控件</summary>
    private Control CreateEditor(PropertyInfo prop)
    {
        Type type = prop.PropertyType;
        string name = prop.Name;

        // ---------- 特殊属性：自定义原生控件 ----------
        // if (name == nameof(SettingsData.ResolutionIndex))
        // {
        //     OptionButton option = new OptionButton();
        //     foreach (var res in _resolutions)
        //         option.AddItem(res.Name);
        //     option.ItemSelected += idx => _settings.Set(name, (int)idx);
        //     return option;
        // }

        // if (name == nameof(SettingsData.Fullscreen))
        // {
        //     CheckButton check = new CheckButton { Text = "启用全屏" };
		// 	check.Theme = _theme;
        //     check.Toggled += (bool v) => _settings.Set(name, v);
        //     return check;
        // }

        if (name == nameof(SettingsData.VSync))
        {
            OptionButton option = new OptionButton();
            foreach (DisplayServer.VSyncMode mode in Enum.GetValues<DisplayServer.VSyncMode>())
            {
                option.AddItem(FormatVSyncName(mode), (int)mode);
            }
			option.Theme = _theme;
            option.ItemSelected += idx =>
            {
                int id = option.GetItemId((int)idx);
                _settings.Set(name, id); // 枚举以 int 形式写入
            };
            return option;
        }

        // ---------- 通用属性：复用 PropertyEditor 体系 ----------
        try
        {
            IPropertyEditor editor = PropertyEditorFactory.Create(type);
			if(editor == null)
			{
				GD.PrintErr($"[{Name}] 无法创建对应编辑字段:{name}({type})");
			}
            editor.Setup(name);
            editor.ValueChanged += (key, value) => _settings.Set(key, Variant.From(value));
            return editor.Control;
        }
        catch (NotSupportedException)
        {
            GD.PushWarning($"[SettingsPanel] 不支持的设置类型: {type.Name} ({name})");
            return new Label { Text = $"<不支持: {type.Name}>" };
        }
    }

    /// <summary>将 GameSettings.Current 的值同步到所有 UI 控件</summary>
    private void BindValues()
    {
        foreach (KeyValuePair<string, Control> kvp in _editors)
        {
            string key = kvp.Key;
            Control ctrl = kvp.Value;
            object raw = _properties[key].GetValue(_settings.Current);

            switch (ctrl)
            {
                case OptionButton opt:
                    if (key == nameof(SettingsData.VSync))
                    {
                        int id = (int)raw;
                        for (int i = 0; i < opt.ItemCount; i++)
                            if (opt.GetItemId(i) == id) { opt.Select(i); break; }
                    }
                    else
                    {
                        opt.Select(Convert.ToInt32(raw));
                    }
                    break;

                case CheckButton check:
                    check.ButtonPressed = Convert.ToBoolean(raw);
                    break;

                default:
                    if (ctrl is IPropertyEditor ipe)
                        ipe.SetValue(raw);
                    break;
            }
        }
    }

    // ---------- 事件回调 ----------

    private void OnSettingChanged(string key, Variant value)
    {
        if (!_editors.TryGetValue(key, out Control ctrl)) return;

        switch (ctrl)
        {
            case OptionButton opt:
                if (key == nameof(SettingsData.VSync))
                {
                    int id = value.AsInt32();
                    for (int i = 0; i < opt.ItemCount; i++)
                        if (opt.GetItemId(i) == id) { opt.Select(i); break; }
                }
                else
                {
                    opt.Select(value.AsInt32());
                }
                break;

            case CheckButton check:
                check.ButtonPressed = value.AsBool();
                break;

            default:
                if (ctrl is IPropertyEditor ipe)
                    ipe.SetValue(value.Obj);
                break;
        }
    }

    private void OnSettingsApplied() => BindValues();

    // private void OnResetPressed() => _settings.ResetToDefault();

    private void OnApplyPressed()
    {
        _settings.ApplyToEngine();
        _settings.Save();
    }

    // ---------- 辅助 ----------

    private static string FormatLabelName(string name) => name switch
    {
        // nameof(SettingsData.ResolutionIndex) => "分辨率",
        // nameof(SettingsData.Fullscreen)      => "全屏模式",
        nameof(SettingsData.VSync)           => "垂直同步",
        nameof(SettingsData.ResourcePackId)  => "资源包",
        _ => name
    };

    private static string FormatVSyncName(DisplayServer.VSyncMode mode) => mode switch
    {
        DisplayServer.VSyncMode.Disabled => "关闭 (Disabled)",
        DisplayServer.VSyncMode.Enabled  => "开启 (Enabled)",
        DisplayServer.VSyncMode.Adaptive => "自适应 (Adaptive)",
        DisplayServer.VSyncMode.Mailbox  => "信箱 (Mailbox)",
        _ => mode.ToString()
    };
}