using Godot;
using QuickType;
using System;

public partial class TestSceneEventInfoPanel : Node
{
    [Export] private EventInfoPanel eventInfoPanel;

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

        eventInfoPanel.ShowInfo(lineEvent, 0, LineEventEnum.MoveX, 999);

        eventInfoPanel.EventPropertyChanged += (
            int lineId, LineEventEnum lineEventEnum, 
            int index, LineEventPropertyType propertyType, object newValue) =>
        {
            GD.Print($"[{this.Name}] 用户修改了Event属性{propertyType}:{newValue} (line{lineId}_{lineEventEnum}_{index})");
        };

        //CallDeferred(MethodName.ShowInfo);
    }

    private void ShowInfo()
    {
        
    }


}
