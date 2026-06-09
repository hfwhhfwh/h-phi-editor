using Godot;
using System;

//缓动函数
public enum EasingFunc
{
    Linear,Sine,Quad,Cubic,Quart,Quint,Expo,Circ,Back,Elastic,Bounce
}
public enum EasingIO
{
    In,Out,IO
}

public static class EasingHelper
{
    /// <summary>
    /// 缓动插值函数
    /// </summary>
    /// <param name="t">[0,1]</param>
    /// <param name="func">缓动函数的函数类型</param>
    /// <param name="io">缓动函数的缓急类型</param>
    /// <returns>缓动后的结果</returns>
    public static float Interpolate(float t, EasingFunc func, EasingIO io)
    {
        if(io == EasingIO.IO)
        {
            //由In和Out拼接而成
            if(t>=0 && t < 0.5f)
            {
                return 0.5f * Interpolate(2f * t, func, EasingIO.In);
            }
            else if(t>=0.5 && t <= 1)
            {
                return 0.5f + 0.5f * Interpolate(2f*t-1, func, EasingIO.Out);
            }
        }
        else if(io == EasingIO.In)
        {
            switch (func)
            {
                case EasingFunc.Linear : return t;
                case EasingFunc.Sine : return (float)(1 - Mathf.Cos(Math.PI * t / 2f));
                case EasingFunc.Quad : return t*t;
                case EasingFunc.Cubic : return t*t*t;
                case EasingFunc.Quart : return (float)Math.Pow(t,4);
                case EasingFunc.Quint : return (float)Math.Pow(t,5);
                case EasingFunc.Expo:
                    if (t == 0) return 0;
                    return (float)Math.Pow(2, 10 * t - 10);
                case EasingFunc.Circ: return (float)(1 - Math.Sqrt(1 - t * t));
                case EasingFunc.Back: return (float)((2.70158f * t - 1.70158f) * t * t);
                case EasingFunc.Elastic:
                    if (t == 0) return 0;
                    if (t == 1) return 1;
                    const float c4 = 2f * (float)Math.PI / 3f;
                    return (float)(-Math.Pow(2, 10 * t - 10) * Math.Sin((t * 10 - 10.75) * c4));
                case EasingFunc.Bounce:
                    return 1 - Interpolate(1 - t, func, EasingIO.Out); // InBounce = 1 - OutBounce(1-t)
                default: return t;
            }
        }
        else if(io == EasingIO.Out)
        {
            switch (func)
            {
                case EasingFunc.Linear: return t;
                case EasingFunc.Sine: return (float)Math.Sin(Math.PI * t / 2f);
                case EasingFunc.Quad: return 1 - (1 - t) * (1 - t);
                case EasingFunc.Cubic: return 1 - (1 - t) * (1 - t) * (1 - t);
                case EasingFunc.Quart: return 1 - (float)Math.Pow(1 - t, 4);
                case EasingFunc.Quint: return 1 - (float)Math.Pow(1 - t, 5);
                case EasingFunc.Expo:
                    if (Math.Abs(t - 1) < 1e-6) return 1f;
                    return (float)(1 - Math.Pow(2, -10 * t));
                case EasingFunc.Circ: return (float)Math.Sqrt(1 - (t - 1) * (t - 1));
                case EasingFunc.Back:
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1;
                    return 1 + c3 * u * u * u + c1 * u * u;
                case EasingFunc.Elastic:
                    if (t == 0) return 0;
                    if (t == 1) return 1;
                    const float c4_elastic = (2f * (float)Math.PI) / 3f;
                    return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4_elastic) + 1);
                case EasingFunc.Bounce:
                    // 标准 easeOutBounce 分段函数
                    float n1 = 7.5625f;
                    float d1 = 2.75f;
                    if (t < 1f / d1)
                    {
                        return n1 * t * t;
                    }
                    else if (t < 2f / d1)
                    {
                        t -= 1.5f / d1;
                        return n1 * t * t + 0.75f;
                    }
                    else if (t < 2.5f / d1)
                    {
                        t -= 2.25f / d1;
                        return n1 * t * t + 0.9375f;
                    }
                    else
                    {
                        t -= 2.625f / d1;
                        return n1 * t * t + 0.984375f;
                    }
                default: return t;
            }
        }
        return t; // 理论上不会执行到这里
    }

    /// <summary>
    /// 缓动插值函数
    /// </summary>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <returns>缓动后的结果</returns>
    public static float Interpolate(float t, int easingType)
    {
        switch (easingType)
        {
            case 0: return 0; // Fixed
            case 1: return Interpolate(t, EasingFunc.Linear, EasingIO.In);   // Linear
            case 2: return Interpolate(t, EasingFunc.Sine, EasingIO.Out);    // easeOutSine
            case 3: return Interpolate(t, EasingFunc.Sine, EasingIO.In);     // easeInSine
            case 4: return Interpolate(t, EasingFunc.Quad, EasingIO.Out);    // easeOutQuad
            case 5: return Interpolate(t, EasingFunc.Quad, EasingIO.In);     // easeInQuad
            case 6: return Interpolate(t, EasingFunc.Sine, EasingIO.IO);     // easeInOutSine
            case 7: return Interpolate(t, EasingFunc.Quad, EasingIO.IO);     // easeInOutQuad
            case 8: return Interpolate(t, EasingFunc.Cubic, EasingIO.Out);   // easeOutCubic
            case 9: return Interpolate(t, EasingFunc.Cubic, EasingIO.In);    // easeInCubic
            case 10: return Interpolate(t, EasingFunc.Quart, EasingIO.Out);  // easeOutQuart
            case 11: return Interpolate(t, EasingFunc.Quart, EasingIO.In);   // easeInQuart
            case 12: return Interpolate(t, EasingFunc.Cubic, EasingIO.IO);   // easeInOutCubic (注意：原文写的是Cubic不是Quart)
            case 13: return Interpolate(t, EasingFunc.Quart, EasingIO.IO);   // easeInOutQuart
            case 14: return Interpolate(t, EasingFunc.Quint, EasingIO.Out);  // easeOutQuint
            case 15: return Interpolate(t, EasingFunc.Quint, EasingIO.In);   // easeInQuint
            case 16: return Interpolate(t, EasingFunc.Expo, EasingIO.Out);   // easeOutExpo
            case 17: return Interpolate(t, EasingFunc.Expo, EasingIO.In);    // easeInExpo
            case 18: return Interpolate(t, EasingFunc.Circ, EasingIO.In);    // easeInCirc (注意：Circ的In/Out编号与其他相反)
            case 19: return Interpolate(t, EasingFunc.Circ, EasingIO.Out);   // easeOutCirc
            case 20: return Interpolate(t, EasingFunc.Back, EasingIO.Out);   // easeOutBack
            case 21: return Interpolate(t, EasingFunc.Back, EasingIO.In);    // easeInBack
            case 22: return Interpolate(t, EasingFunc.Circ, EasingIO.IO);    // easeInOutCirc
            case 23: return Interpolate(t, EasingFunc.Back, EasingIO.IO);    // easeInOutBack
            case 24: return Interpolate(t, EasingFunc.Elastic, EasingIO.Out);// easeOutElastic
            case 25: return Interpolate(t, EasingFunc.Elastic, EasingIO.In); // easeInElastic
            case 26: return Interpolate(t, EasingFunc.Bounce, EasingIO.Out); // easeOutBounce
            case 27: return Interpolate(t, EasingFunc.Bounce, EasingIO.In);  // easeInBounce
            case 28: return Interpolate(t, EasingFunc.Bounce, EasingIO.IO);  // easeInOutBounce
            case 29: return Interpolate(t, EasingFunc.Elastic, EasingIO.IO); // easeInOutElastic
            default: return Interpolate(t, EasingFunc.Linear, EasingIO.In);
        }
    }

    /// <summary>
    /// 带有实际值的插值
    /// </summary>
    /// <param name="x1"></param>
    /// <param name="x2"></param>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <returns></returns>
    public static float InterpolateValue(float x1, float x2, float t, int easingType)
    {
        return x1 + (x2-x1) * Interpolate(t,easingType);
    }

    /// <summary>
    /// 经过裁剪的缓动插值
    /// </summary>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <param name="left">左切割，[0,1]</param>
    /// <param name="right">右切割，[0,1]</param>
    /// <returns></returns>
    public static float CutInterpolate(float t, int easingType, float left, float right)
    {
        float leftX = InterpolateValue(0,1,left,easingType);
        float rightX = InterpolateValue(0,1,right,easingType);
        float T = left + (right - left) * t;
        float TX = InterpolateValue(0,1,T,easingType);

        float result = (TX - leftX) / (rightX - leftX);
        return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="x1">初始值</param>
    /// <param name="x2">末尾值</param>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <param name="left">左切割，[0,1]</param>
    /// <param name="right">右切割，[0,1]</param>
    /// <returns></returns>
    public static float CutInterpolateValue(float x1, float x2, float t, int easingType, float left, float right)
    {
        return x1 + (x2-x1) * CutInterpolate(t, easingType, left, right);
    }
    
}
