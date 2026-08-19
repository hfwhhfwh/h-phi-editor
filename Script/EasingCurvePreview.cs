using Godot;
using System;

public partial class EasingCurvePreview : Control
{
    public int EasingType { get; set; }
    public float LeftCut { get; set; } = 0f;
    public float RightCut { get; set; } = 1f;

    public Color LineColor { get; set; } = Colors.Orange;
    public float LineWidth { get; set; } = 5f;

    public Color FrameColor { get; set; } = Colors.Blue;
    public float FrameWidth { get; set; } = 2f;

    

    public override void _Draw()
    {
        base._Draw();

        Transform2D transform = Transform2D.Identity
            .Scaled( new Vector2(Size.X, -Size.Y))
            .Translated(new Vector2(0, Size.Y));
            // .Translated(Size / 2);

        Vector2[] curvePoints = EasingCurvePreviewHelper.GetCurvePoints(
            EasingType,
            transform,
            32
        );

        DrawPolyline(curvePoints, LineColor, LineWidth, false);

        Vector2[] framePoints = EasingCurvePreviewHelper.GetFramePoints(transform);
        DrawPolyline(framePoints, FrameColor, FrameWidth, false);
    }


}
