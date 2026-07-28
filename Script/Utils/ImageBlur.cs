using Godot;
using System;

public partial class ImageBlur : Node
{
    /// <summary>
    /// 对 Image 应用高斯模糊（分离卷积实现）
    /// </summary>
    /// <param name="source">原始图像（不会被修改）</param>
    /// <param name="radius">模糊半径（像素），建议 2-5</param>
    /// <param name="sigma">标准差，通常设为 radius/2</param>
    /// <returns>模糊后的新 Image</returns>
    public static Image GaussianBlur(Image source, int radius, float sigma)
    {
        // 复制一份并确保格式为 RGBA8 方便处理
        Image img = new Image();
        img.CopyFrom(source);
        img.Convert(Image.Format.Rgba8);

        int width = img.GetWidth();
        int height = img.GetHeight();

        // 1. 水平模糊：生成临时图像
        Image horizontalPass = new Image();
        horizontalPass.CopyFrom(img);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color sum = Colors.Transparent;
                float totalWeight = 0f;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= width) continue;
                    float weight = Mathf.Exp(-(dx * dx) / (2 * sigma * sigma));
                    sum += img.GetPixel(nx, y) * weight;
                    totalWeight += weight;
                }
                horizontalPass.SetPixel(x, y, sum / totalWeight);
            }
        }

        // 2. 垂直模糊：基于水平结果
        Image result = new Image();
        result.CopyFrom(horizontalPass);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color sum = Colors.Transparent;
                float totalWeight = 0f;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= height) continue;
                    float weight = Mathf.Exp(-(dy * dy) / (2 * sigma * sigma));
                    sum += horizontalPass.GetPixel(x, ny) * weight;
                    totalWeight += weight;
                }
                result.SetPixel(x, y, sum / totalWeight);
            }
        }

        return result;
    }

    /// <summary>
    /// 保存 Image 为 PNG 文件（在项目资源目录）
    /// </summary>
    public static void SaveImage(Image image, string path)
    {
        // 注意：path 应该是 "res://" 开头的项目路径
        image.SavePng(path);
    }

    /// <summary>
    /// 从文件加载 Image
    /// </summary>
    public static Image LoadImage(string path)
    {
        var img = new Image();
        img.Load(path);
        return img;
    }
}
