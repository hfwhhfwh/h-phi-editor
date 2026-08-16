using Godot;
using System;

public static class MathUtil
{

    public static int NextPowerOfTwo(int value)
    {
        int power = 1;
        while (power < value)
            power <<= 1;
        return power;
    }

    public static int GCD(int a, int b)
    {
        int max = a > b ? a : b;
        int min = a < b ? a : b;

        int temp;
        while (min != 0)
        {
            temp = max % min;
            max = min;
            min = temp;
        }

        return max;
    }

    //获取最小公约数
    public static int LCM(int a, int b)
    {
        return a * b / GCD(a, b);
    }
}
