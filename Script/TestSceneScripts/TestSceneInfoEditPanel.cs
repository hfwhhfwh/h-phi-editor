using Godot;
using System;

public partial class TestSceneInfoEditPanel : Node
{
    [Export] private InfoEditPanel infoEditPanel;

    // ——— 模拟一份"待编辑"的数据 ———
    private string _name     = "测试谱面";
    private float  _duration = 180.5f;
    private int    _offset   = 0;
    private bool   _isFake   = false;
    private int    _alpha    = 255;

    public override void _Ready()
    {
        // 1) 连接确认信号
        infoEditPanel.OnConfirmed += OnConfirmed;

        // 2) 打开面板（清空字段并设置标题）
        infoEditPanel.BeginEdit("Meta");

        // 3) 逐个字段添加 —— readValue 用 lambda 保证读取的是"最新"值
        infoEditPanel.AddField("Name",     () => _name,     v => { _name     = (string)v; GD.Print($"[Name]     -> {_name}"); });
        infoEditPanel.AddField("Duration", () => _duration, v => { _duration = (float)v;  GD.Print($"[Duration] -> {_duration}"); });
        infoEditPanel.AddField("Offset",   () => _offset,   v => { _offset   = (int)v;    GD.Print($"[Offset]   -> {_offset}"); });
        infoEditPanel.AddField("IsFake",   () => _isFake,   v => { _isFake   = (bool)v;   GD.Print($"[IsFake]   -> {_isFake}"); });
        infoEditPanel.AddField("Alpha",    () => _alpha,    v => { _alpha    = (int)v;    GD.Print($"[Alpha]    -> {_alpha}"); });

        // 如需传范围/步长，可以这样（字段名以你项目里的 FieldOptions 为准）：
        // infoEditPanel.AddField("Duration", () => _duration,
        //     v => _duration = (float)v,
        //     new FieldOptions { Min = 0f, Max = 600f, Step = 0.1f });

        // 也可以用 AddFields 批量添加：
        // infoEditPanel.AddFields(
        //     p => p.AddField("Name",     () => _name,     v => _name     = (string)v),
        //     p => p.AddField("Duration", () => _duration, v => _duration = (float)v));

    }

    /// <summary>
    /// 演示：外部数据被改变后，如何在不触发 onChanged 回调的前提下刷新 UI。
    /// </summary>
    private void SimulateExternalChange()
    {
        // 模拟底层数据被其他系统改了
        _name     = "外部改名";
        _duration = 200f;

        // 只更新标题，不清空字段
        infoEditPanel.UpdateTitle("Meta (已更新)");

        // 静默刷新：把绑定数据回填到 UI，不会触发任何 onChanged 回调
        infoEditPanel.ApplyToUI();
        GD.Print("[ApplyToUI] 已静默刷新，界面显示最新数据");
    }

    /// <summary>
    /// 演示：清空字段后重新打开面板。
    /// 可通过快捷键调用，或在别处按钮里调用。
    /// </summary>
    public void ReopenWithNewTitle()
    {
        infoEditPanel.ClearFields();
        GD.Print("[ClearFields] 已清空所有字段");

        infoEditPanel.BeginEdit("Meta2");
        infoEditPanel.AddField("Name", () => _name, v => _name = (string)v);
        GD.Print("[BeginEdit] 重新打开面板");
    }

    // 便于测试：按 Enter 触发一次"清空并重开"流程
    private int _simulationStep = 0;
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept"))
        {
            switch (_simulationStep)
            {
                case 0:
                    // 延迟一帧后模拟"外部数据变化"（撤销重做/联动等场景）
                    CallDeferred(nameof(SimulateExternalChange));
                    break;
                case 1:
                    ReopenWithNewTitle();
                    break;
                default:

                    break;
            }

            _simulationStep++;
            
            
            
        }
    }

    private void OnConfirmed()
    {
        GD.Print($"[确认] 当前数据: name={_name}, duration={_duration}, offset={_offset}, isFake={_isFake}, alpha={_alpha}");
    }
}