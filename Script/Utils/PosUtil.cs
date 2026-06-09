using Godot;
using System;

public static class PosUtil
{
    /// <summary>
    /// 将谱面坐标映射到Container的坐标
    /// </summary>
    /// <param name="pos">谱面坐标</param>
    /// <param name="containerSize">Container大小</param>
    /// <returns></returns>
    public static Vector2 ChartPosToViewportPos(Vector2 pos, Vector2 containerSize)
    {
        //获取屏幕大小
        // Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        // //[-675,675] -> [0,X]
        // float newX = (viewportSize.X/1350f) * pos.X + (viewportSize.X/2f);
        // //[-450,450] -> [Y,0]
        // float newY = (-viewportSize.Y / 900f) * pos.Y + (viewportSize.Y/2f);
        
        //根据容器大小调整
        float ratioX = (pos.X + 675f) / 1350f;
        float newX = ratioX * containerSize.X;

        float ratioY = (pos.Y + 450f) / 900f;
        float newY = (1 - ratioY) * containerSize.Y;

        return new Vector2(newX,newY);
    }

    /// <summary>
    /// 将note在谱面中相对于判定线的坐标映射到Godot中相对于判定线的的坐标
    /// </summary>
    /// <param name="pos">note在谱面中相对于判定线的坐标</param>
    /// <param name="containerSize">Container大小</param>
    /// <returns>Godot中相对于判定线的的坐标</returns>
    public static Vector2 ChartPosToLocalPos(Vector2 pos, Vector2 containerSize)
    {
        float localY = -pos.Y * (containerSize.Y / 900f);
        float localX = pos.X * (containerSize.X / 1350f);

        return new Vector2(localX, localY);
    }

    /// <summary>
    /// 从相对父物体的局部坐标转换到全局坐标（谱面坐标系）
    /// </summary>
    /// <param name="fatherPos">父线的坐标（谱面坐标系）</param>
    /// <param name="childLocalPos">子线的相对坐标（谱面坐标系）</param>
    /// <param name="fatherRotationDegrees">父线的旋转，单位为度，正值表示逆时针旋转</param>
    /// <returns>子线的全局谱面坐标</returns>
    public static Vector2 GetChildGlobalPosition(Vector2 fatherPos, Vector2 childLocalPos, float fatherRotationDegrees)
    {
        // // 构建父物体的变换矩阵（旋转 + 平移）
        // Transform2D parentTransform = new Transform2D(Mathf.DegToRad(fatherRotationDegrees), fatherPos);

        // // 将局部坐标转换为全局坐标
        // return parentTransform * childLocalPos;

        float rad = Mathf.DegToRad(fatherRotationDegrees);
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        // 顺时针旋转矩阵：
        // [ cosθ  sinθ ]
        // [ -sinθ cosθ ]
        Vector2 rotated = new Vector2(
            childLocalPos.X * cos + childLocalPos.Y * sin,
            -childLocalPos.X * sin + childLocalPos.Y * cos
        );

        // 加上父物体位置
        return fatherPos + rotated;
    }
}
