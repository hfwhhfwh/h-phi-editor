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
    
    public static class Convert
    {
        public static ValueTuple<EasingFunc, EasingIO> NumberToEasing(int num)
        {
            switch (num)
            {
                case 0: return (EasingFunc.Linear, EasingIO.In); // Fixed
                case 1: return (EasingFunc.Linear, EasingIO.In);   // Linear
                case 2: return (EasingFunc.Sine, EasingIO.Out);    // easeOutSine
                case 3: return (EasingFunc.Sine, EasingIO.In);     // easeInSine
                case 4: return (EasingFunc.Quad, EasingIO.Out);    // easeOutQuad
                case 5: return (EasingFunc.Quad, EasingIO.In);     // easeInQuad
                case 6: return (EasingFunc.Sine, EasingIO.IO);     // easeInOutSine
                case 7: return (EasingFunc.Quad, EasingIO.IO);     // easeInOutQuad
                case 8: return (EasingFunc.Cubic, EasingIO.Out);   // easeOutCubic
                case 9: return (EasingFunc.Cubic, EasingIO.In);    // easeInCubic
                case 10: return (EasingFunc.Quart, EasingIO.Out);  // easeOutQuart
                case 11: return (EasingFunc.Quart, EasingIO.In);   // easeInQuart
                case 12: return (EasingFunc.Cubic, EasingIO.IO);   // easeInOutCubic (注意：原文写的是Cubic不是Quart)
                case 13: return (EasingFunc.Quart, EasingIO.IO);   // easeInOutQuart
                case 14: return (EasingFunc.Quint, EasingIO.Out);  // easeOutQuint
                case 15: return (EasingFunc.Quint, EasingIO.In);   // easeInQuint
                case 16: return (EasingFunc.Expo, EasingIO.Out);   // easeOutExpo
                case 17: return (EasingFunc.Expo, EasingIO.In);    // easeInExpo
                case 18: return (EasingFunc.Circ, EasingIO.In);    // easeInCirc (注意：Circ的In/Out编号与其他相反)
                case 19: return (EasingFunc.Circ, EasingIO.Out);   // easeOutCirc
                case 20: return (EasingFunc.Back, EasingIO.Out);   // easeOutBack
                case 21: return (EasingFunc.Back, EasingIO.In);    // easeInBack
                case 22: return (EasingFunc.Circ, EasingIO.IO);    // easeInOutCirc
                case 23: return (EasingFunc.Back, EasingIO.IO);    // easeInOutBack
                case 24: return (EasingFunc.Elastic, EasingIO.Out);// easeOutElastic
                case 25: return (EasingFunc.Elastic, EasingIO.In); // easeInElastic
                case 26: return (EasingFunc.Bounce, EasingIO.Out); // easeOutBounce
                case 27: return (EasingFunc.Bounce, EasingIO.In);  // easeInBounce
                case 28: return (EasingFunc.Bounce, EasingIO.IO);  // easeInOutBounce
                case 29: return (EasingFunc.Elastic, EasingIO.IO); // easeInOutElastic
                default: return (EasingFunc.Linear, EasingIO.In);
            }
        }

        /// <summary>
        /// 将缓动函数类型和缓急类型转换为对应的 RPE 编号（0~29）
        /// </summary>
        /// <param name="func">缓动函数类型</param>
        /// <param name="io">缓急类型</param>
        /// <returns>对应的编号，若不存在则返回 -1</returns>
        public static int EasingToNumber(EasingFunc func, EasingIO io)
        {
            // 特殊处理：Linear.In 对应编号 1（编号 0 为 Fixed，并非真正的缓动）
            if (func == EasingFunc.Linear && io == EasingIO.In)
                return 1;

            switch (func)
            {
                case EasingFunc.Sine:
                    if (io == EasingIO.Out) return 2;
                    if (io == EasingIO.In)  return 3;
                    if (io == EasingIO.IO)  return 6;
                    break;
                case EasingFunc.Quad:
                    if (io == EasingIO.Out) return 4;
                    if (io == EasingIO.In)  return 5;
                    if (io == EasingIO.IO)  return 7;
                    break;
                case EasingFunc.Cubic:
                    if (io == EasingIO.Out) return 8;
                    if (io == EasingIO.In)  return 9;
                    if (io == EasingIO.IO)  return 12;
                    break;
                case EasingFunc.Quart:
                    if (io == EasingIO.Out) return 10;
                    if (io == EasingIO.In)  return 11;
                    if (io == EasingIO.IO)  return 13;
                    break;
                case EasingFunc.Quint:
                    if (io == EasingIO.Out) return 14;
                    if (io == EasingIO.In)  return 15;
                    // Quint 没有 IO 变体，直接 break
                    break;
                case EasingFunc.Expo:
                    if (io == EasingIO.Out) return 16;
                    if (io == EasingIO.In)  return 17;
                    // Expo 没有 IO 变体
                    break;
                case EasingFunc.Circ:
                    // 注意：Circ 的 In/Out 编号与其他函数相反
                    if (io == EasingIO.In)  return 18;
                    if (io == EasingIO.Out) return 19;
                    if (io == EasingIO.IO)  return 22;
                    break;
                case EasingFunc.Back:
                    if (io == EasingIO.Out) return 20;
                    if (io == EasingIO.In)  return 21;
                    if (io == EasingIO.IO)  return 23;
                    break;
                case EasingFunc.Elastic:
                    if (io == EasingIO.Out) return 24;
                    if (io == EasingIO.In)  return 25;
                    if (io == EasingIO.IO)  return 29;
                    break;
                case EasingFunc.Bounce:
                    if (io == EasingIO.Out) return 26;
                    if (io == EasingIO.In)  return 27;
                    if (io == EasingIO.IO)  return 28;
                    break;
            }
            // 未匹配到任何有效编号
            return -1;
        }
    }
}
