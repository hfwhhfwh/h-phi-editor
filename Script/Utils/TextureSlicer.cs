using Godot;
using System;
using System.Reflection.Metadata;

public static class TextureSlicer
{
    public const string Name = nameof(TextureSlicer);
    /// <summary>
    /// 将 Texture2D 按网格切割为 SpriteFrames
    /// </summary>
    public static SpriteFrames CreateSpriteFrames(
        Texture2D texture, 
        int hframes, 
        int vframes, 
        string animName = "default")
    {
        if (texture == null) throw new ArgumentNullException(nameof(texture));
        if (hframes <= 0 || vframes <= 0) throw new ArgumentException("帧数必须大于0");

        int frameW = texture.GetWidth() / hframes;
        int frameH = texture.GetHeight() / vframes;

        var spriteFrames = new SpriteFrames();
        if(!spriteFrames.HasAnimation(animName)) spriteFrames.AddAnimation(animName);

        // 一次性获取原图像素数据
        Image sourceImage = texture.GetImage();
        if (sourceImage == null)
        {
            GD.PushError($"[{Name}] 无法读取纹理像素数据");
            return spriteFrames;
        }

        for (int y = 0; y < vframes; y++)
        {
            for (int x = 0; x < hframes; x++)
            {
                Rect2I region = new Rect2I(
                    x * frameW, 
                    y * frameH, 
                    frameW, 
                    frameH
                );

                // 裁出独立像素块 → 生成独立纹理
                Image frameImage = sourceImage.GetRegion(region);
                ImageTexture frameTex = ImageTexture.CreateFromImage(frameImage);

                spriteFrames.AddFrame(animName, frameTex);
            }
        }

        return spriteFrames;
    }

    /// <summary>
    /// 从 Texture2D 中裁剪指定矩形区域，返回新的独立 Texture2D
    /// </summary>
    public static Texture2D CropTexture(Texture2D source, Rect2I region)
    {
        // 1. 获取像素数据
        Image sourceImage = source.GetImage();
        if (sourceImage == null)
        {
            GD.PushError($"[{Name}] 无法读取纹理像素数据（可能是 ViewportTexture 或压缩格式不支持）");
            return null;
        }

        // 2. 裁剪区域
        Image croppedImage = sourceImage.GetRegion(region);
        
        // 3. 生成新的纹理
        return ImageTexture.CreateFromImage(croppedImage);
    }
}