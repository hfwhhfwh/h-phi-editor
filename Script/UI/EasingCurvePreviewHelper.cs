using Godot;
using System.Collections.Generic;

public static class EasingCurvePreviewHelper
{
    // public void DrawEasingCurve(int easingType, Transform2D transform, Color color, float width, int sample)
    // {
    //     // if(!EasingHelper.IsNumberValid(easingType)) return;

    //     DrawPolyline(GetCurvePoints(easingType, transform, sample), color, width, true);
    // }

    public static Vector2[] GetCurvePoints(int easingType, Transform2D transform, int sample)
    {
        Vector2[] points = new Vector2[sample + 1];
        for (int i = 0; i <= sample; i++)
        {
            float t = i / (float)sample;
            Vector2 point = new(t, EasingHelper.Interpolate(t, easingType));
            points[i] = transform * point;
        }

        return points;
    }

    public static Vector2[] GetFramePoints(Transform2D transform)
    {
        return new[]
        {
            transform * new Vector2(0f, 0f),
            transform * new Vector2(1f, 0f),
            transform * new Vector2(1f, 1f),
            transform * new Vector2(0f, 1f),
            transform * new Vector2(0f, 0f)
        };
    }
}
