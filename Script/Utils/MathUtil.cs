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
}
