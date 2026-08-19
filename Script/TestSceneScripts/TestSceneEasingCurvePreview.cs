using Godot;
using System;

public partial class TestSceneEasingCurvePreview : Node
{
    [Export] private EasingCurvePreview easingCurvePreview;

    public override void _Ready()
    {
        easingCurvePreview.EasingType = 4;
        
        easingCurvePreview.QueueRedraw();
    }

}
