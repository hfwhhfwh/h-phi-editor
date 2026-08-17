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
            EasingType = 7,
        };

        eventInfoPanel.Edit(lineEvent, 0, 0, LineEventEnum.MoveX, 999);

        eventInfoPanel.PropertyChanged += (
            int lineId, int layer, LineEventEnum type, int idx, LineEventPropertyType prop, object val) =>
        {
            GD.Print($"[{Name}] 修改了 {prop}:{val} (line{lineId}_layer{layer}_{type}_{idx})");
        };

        eventInfoPanel.OnConfirmed += () => {
            GD.Print($"用户按下了确认键");
        };

        //CallDeferred(MethodName.ShowInfo);
    }


}
