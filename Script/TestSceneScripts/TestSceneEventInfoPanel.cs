using Godot;
using QuickType;
using System;

public partial class TestSceneEventInfoPanel : Node
{
    [Export] private LineEventInfoPanel eventInfoPanel;

    public override void _Ready()
    {
        base._Ready();

        LineEvent lineEvent = new LineEvent
        {
            StartTime = [1,2,3],
            EndTime = [4,5,6],
            Start = 123,
            End = 456,
            EasingType = 25,
        };

        eventInfoPanel.Edit(lineEvent, 0, 0, LineEventEnum.MoveX, 999);

        // 面板现在直接调用 ChartEditService 执行属性修改命令（面板通过 Initialize 注入服务），
        // 本测试场景没有谱面数据，因此只做界面展示。

        eventInfoPanel.OnConfirmed += () => {
            GD.Print($"用户按下了确认键");
        };

        //CallDeferred(MethodName.ShowInfo);
    }


}
