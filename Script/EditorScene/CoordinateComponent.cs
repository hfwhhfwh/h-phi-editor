using Godot;
using QuickType;
using System;

public class CoordinateComponent
{
    public float horMargin;
    public float verMargin;
    public int subBeatCount;
    public int verLineCount; 
    public float GroundY;

    public float horOffsetSmoothed;
    public float horSeparationSmoothed;

    public Vector2 parentSize;

    /// <summary>
    /// 获取某个物体在面板上的坐标
    /// </summary>
    /// <param name="posX">X坐标，[-675, 675]</param>
    /// <param name="beatTime">时间（单位为拍数）</param>
    /// <returns>物体在面板上的坐标</returns>
    public Vector2 GetPanelPosition(float posX, float beatTime)
    {
        // 计算面板 X 坐标（谱面坐标 -675~675 映射到面板水平范围）
        float panelX = GetPanelPosX(posX);
        // 起始 Y 坐标（向上为负）
        float panelY = GetPanelPosY(beatTime);

        return new Vector2(panelX, panelY);
    }

    /// <summary>
    /// 获取某个物体在面板上的Y坐标
    /// </summary>
    /// <param name="beatTime">时间（单位为拍数）</param>
    /// <returns>物体在面板上的Y坐标</returns>
    public float GetPanelPosY(float beatTime)
    {
        // 起始 Y 坐标（向上为负）
        float panelY = GroundY + horOffsetSmoothed - beatTime * horSeparationSmoothed;

        return panelY;
    }

    /// <summary>
    /// 将谱面X坐标转换为Control坐标系下的X坐标
    /// </summary>
    /// <param name="chartPosX">铺面坐标系下的X坐标</param>
    /// <returns>物体在Control坐标系下的X坐标</returns>
    public float GetPanelPosX(float chartPosX)
    {
        // 计算面板 X 坐标（谱面坐标 -675~675 映射到面板水平范围）
        float ratio = (chartPosX - (-675f)) / 1350f;
        float panelX = verMargin + ratio * (parentSize.X - 2 * verMargin);

        return panelX;
    }

    /// <summary>
    /// 将Control坐标系下的X坐标转换为谱面X坐标
    /// </summary>
    /// <param name="localX">Control坐标系下的X坐标</param>
    /// <returns>谱面坐标系下的X坐标</returns>
    public float GetChartPosX(float localX)
    {
        float ratio = (localX - verMargin) / (parentSize.X - 2 * verMargin);
        float chartPosX = -675f + ratio * 1350f;
        return chartPosX;
    }

    /// <summary>
    /// 将Control坐标系下的Y坐标转换为BeatValue
    /// </summary>
    /// <param name="localY">Control坐标系下的Y坐标</param>
    /// <returns>BeatValue</returns>
    public float GetBeatValue(float localY)
    {
        // panelY = Size.Y / 2f + horOffsetSmoothed - beatTime * horSeparationSmoothed;
        float beatValue = (GroundY + horOffsetSmoothed - localY) / horSeparationSmoothed;

        return beatValue;
    }
    
    /// <summary>
    /// 长度换算 BeatValue -> PanelY
    /// </summary>
    /// <param name="beatValue"></param>
    /// <returns></returns>
    public float BeatValueToPanelY(float beatValue)
    {
        return - beatValue * horSeparationSmoothed;
    }

    /// <summary>
    /// 长度换算 ChartX -> PanelX
    /// </summary>
    /// <param name="chartX"></param>
    /// <returns></returns>
    public float ChartXToPanelX(float chartX)
    {
        float ratio = chartX / 1350f;
        float panelX = ratio * (parentSize.X - 2 * verMargin);

        return panelX;
    }

    /// <summary>
    /// 长度换算 PanelY -> BeatValue
    /// </summary>
    /// <param name="panelY"></param>
    /// <returns></returns>
    public float PanelYToBeatValue(float panelY)
    {
        return - panelY / horSeparationSmoothed;
    }

    /// <summary>
    /// 长度换算 PanelX -> ChartX
    /// </summary>
    /// <param name="panelX"></param>
    /// <returns></returns>
    public float PanelXToChartX(float panelX)
    {
        float ratio = panelX / (parentSize.X - 2 * verMargin);
        float chartX = ratio * 1350f;

        return chartX;
    }

    public float SnapChartXToGrid(float chartX)
    {
        float ratioX = (chartX - (-675f)) / 1350f;
        float snappedratioX = Mathf.Round(ratioX * (verLineCount - 1)) / (verLineCount - 1);
        float snappedX = -675f + snappedratioX * 1350f;

        return snappedX;
    }

    /// <summary>
    /// 将谱面X坐标吸附到最近的竖线上，返回竖线的索引（最左边为0）
    /// </summary>
    /// <param name="chartX">[-675, 675]</param>
    /// <returns>最近竖线的索引</returns>
    public int SnapChartXToVerLine(float chartX)
    {
        float gap = 1350f / (verLineCount - 1);
        return Mathf.RoundToInt((chartX - (-675f) ) / gap);
    }

    public Beat SnapBeatValueToGrid(float beatValue)
    {
        // float snappedBeatValue = Mathf.Round(beatValue * subBeatCount) / subBeatCount;

        int a;
        if(Mathf.Ceil(beatValue) - beatValue < 1f / subBeatCount / 2)
        {
            a = Mathf.CeilToInt(beatValue);
        }
        else
        {
            a = Mathf.FloorToInt(beatValue);
        }
        
        int b = Mathf.RoundToInt(beatValue * subBeatCount) % subBeatCount;
        int c = subBeatCount;

        return new Beat(a, b, c);
    }

    public float SnapDeltaChartXToDeltaGrid(float deltaChartX)
    {
        float ratioX = deltaChartX / 1350f;
        float snappedratioX = Mathf.Round(ratioX * (verLineCount - 1)) / (verLineCount - 1);
        float snappedX = snappedratioX * 1350f;

        return snappedX;
    }

    public Beat SnapDeltaBeatValueToGrid(float deltaBeatValue)
    {

        int a;
        if(Mathf.Ceil(deltaBeatValue) - deltaBeatValue < 1f / subBeatCount / 2)
        {
            a = Mathf.CeilToInt(deltaBeatValue);
        }
        else
        {
            a = Mathf.FloorToInt(deltaBeatValue);
        }
        
        int b = Mathf.RoundToInt(deltaBeatValue * subBeatCount) % subBeatCount;
        int c = subBeatCount;

        return new Beat(a, b, c);
    }
}
