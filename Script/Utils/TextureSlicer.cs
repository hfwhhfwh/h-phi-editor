using Godot;
using System;

public static class TextureSlicer
{
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

        // 按行优先：从左到右，从上到下
        for (int y = 0; y < vframes; y++)
        {
            for (int x = 0; x < hframes; x++)
            {
                var atlasTex = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2I(
                        x * frameW, 
                        y * frameH, 
                        frameW, 
                        frameH
                    )
                };
                
                // duration: 单帧显示时长（秒）
                spriteFrames.AddFrame(animName, atlasTex);
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
            GD.PushError("无法读取纹理像素数据（可能是 ViewportTexture 或压缩格式不支持）");
            return null;
        }

        // 2. 裁剪区域
        Image croppedImage = sourceImage.GetRegion(region);
        
        // 3. 生成新的纹理
        return ImageTexture.CreateFromImage(croppedImage);
    }
}