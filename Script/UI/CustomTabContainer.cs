using Godot;
using System;
using System.Collections.Generic;

public partial class CustomTabContainer : Node
{
    
    /// <summary>
    /// 在编辑器 Inspector 中配置：Key=选项卡按钮，Value=对应的内容面板
    /// </summary>
    [Export]
    public Godot.Collections.Dictionary<Button, Control> TabMap { get; set; } = new();
    [Export] private Button defaultButton;

    private ButtonGroup _buttonGroup;

    public override void _Ready()
    {
        if (TabMap == null || TabMap.Count == 0)
        {
            GD.PrintErr($"[{Name}] TabMap 为空，请在编辑器中配置映射。");
            return;
        }

        _buttonGroup = new ButtonGroup();

        foreach (KeyValuePair<Button, Control> pair in TabMap)
        {
            Button button = pair.Key;
            Control content = pair.Value;

            if (button == null || content == null) continue;

            // 关键：开启 Toggle 并加入互斥组
            button.ToggleMode = true;
            button.ButtonGroup = _buttonGroup;

            // 初始全部隐藏
            content.Hide();

            // 绑定切换事件
            button.Toggled += pressed => OnTabToggled(button, pressed);
        }

        // 显示默认界面
        SwitchTo(defaultButton);
    }

    private void OnTabToggled(Button button, bool pressed)
    {
        // if(button == null)
        // {
        //     GD.PrintErr($"[{Name}] OnTabToggled() button为空!");
        //     return;
        // }
        // if(!TabMap.TryGetValue(button, out Control selectedPage))
        // {
        //     GD.PrintErr($"[{Name}] OnTabToggled() button不再字典中");
        //     return;
        // }

        if(!pressed) return; // 只处理按下事件

        SwitchTo(button);
    }

    /// <summary>代码切换：传入按钮引用</summary>
    public void SwitchTo(Button button)
    {
        if(button == null)
        {
            GD.PrintErr($"[{Name}] button为空!");
            return;
        }
        if(!TabMap.TryGetValue(button, out Control selectedPage))
        {
            GD.PrintErr($"[{Name}] button不再字典中");
            return;
        }
        
        button.ButtonPressed = true;

        // 隐藏所有界面
        foreach(Control page in TabMap.Values)
        {
            page.Visible = false;
        }

        // 显示选择的界面
        selectedPage.Visible = true;
    }

    /// <summary>获取当前被选中的按钮</summary>
    public Button GetActiveTab()
    {
        foreach (var pair in TabMap)
            if (pair.Key.ButtonPressed) return pair.Key;
        return null;
    }
}
