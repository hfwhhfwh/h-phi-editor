using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

public partial class ResourcePack : Resource
{
    // public enum TextureType
    // {
    //     Click, ClickMh, Drag, DragMh, Flick, FlickMh,
    //     Hold, HoldMh
    // }

    // public enum SxType
    // {
    //     Click, Drag, Flick, Ending
    // }
    public PackManifest Manifest { get; set; }
    
    public Dictionary<string, Texture2D> textureDic = new();
    public Dictionary<string, AudioStream> sxDic = new();
    public Dictionary<string, byte[]> audioRawData = new(); // 缓存原始音频字节，可用于导出

    public SpriteFrames hitEffectSF;
    public Texture2D holdHeadTexture, holdBodyTexture, holdEndTexture;
    public Texture2D holdHeadTextureMh, holdBodyTextureMh, holdEndTextureMh;

    public void Build()
    {
        // 1. 设置打击特效
        string animName = "default";
        // 切割为SpriteFrames
        hitEffectSF = TextureSlicer.CreateSpriteFrames(textureDic["hit_fx"],
            Manifest.HitFxGrid.X,
            Manifest.HitFxGrid.Y,
            animName
        );
        // 不循环播放
        hitEffectSF.SetAnimationLoop(animName, false);
        // 设置持续时间
        int animSpeed = Mathf.RoundToInt(hitEffectSF.GetFrameCount(animName) / Manifest.HitFxDuration);
        hitEffectSF.SetAnimationSpeed(animName, animSpeed);

        // 2. 生成Hold分体贴图
        BuildHoldTexture(textureDic["hold"], Manifest.HoldAtlas, 
            out holdHeadTexture, out holdBodyTexture, out holdEndTexture);
        BuildHoldTexture(textureDic["hold_mh"], Manifest.HoldAtlasMH, 
            out holdHeadTextureMh, out holdBodyTextureMh, out holdEndTextureMh);
        
        // // 3. 压缩所有贴图
        // CompressPackTextures(this);
        
    }

    private static void BuildHoldTexture(Texture2D texture, Vector2I atlas, 
        out Texture2D headTexture, out Texture2D bodyTexture, out Texture2D endTexture)
    {
        Vector2 textureSize = texture.GetSize();
        headTexture = TextureSlicer.CropTexture(
            texture,
            new Rect2I(
                new Vector2I(0, (int)textureSize.Y - atlas.Y + 1),
                new Vector2I((int)textureSize.X, atlas.Y)
            )
        );

        bodyTexture = TextureSlicer.CropTexture(
            texture,
            new Rect2I(
                new Vector2I(0, atlas.X + 1),
                new Vector2I((int)textureSize.X, (int)textureSize.Y - atlas.X - atlas.Y)
            )
        );

        endTexture = TextureSlicer.CropTexture(
            texture,
            new Rect2I(
                new Vector2I(0, 0),
                new Vector2I((int)textureSize.X, atlas.X)
            )
        );
    }

    // /// <summary>
    // /// 将资源包内所有 Texture2D 压缩为 VRAM Compressed (S3TC/ETC2/BCn)
    // /// </summary>
    // private static void CompressPackTextures(ResourcePack pack)
    // {
    //     GD.Print("[ResourcePack] 正在压缩资源包纹理...");
    //     // 1. 基础纹理字典（hit_fx 等）
    //     foreach (string key in new List<string>(pack.textureDic.Keys))
    //     {
    //         if (pack.textureDic[key] != null)
    //             pack.textureDic[key] = CompressTexture(pack.textureDic[key]);
    //     }

    //     // 2. Hold 分体贴图
    //     pack.holdHeadTexture = CompressTexture(pack.holdHeadTexture);
    //     pack.holdBodyTexture = CompressTexture(pack.holdBodyTexture);
    //     pack.holdEndTexture = CompressTexture(pack.holdEndTexture);
    //     pack.holdHeadTextureMh = CompressTexture(pack.holdHeadTextureMh);
    //     pack.holdBodyTextureMh = CompressTexture(pack.holdBodyTextureMh);
    //     pack.holdEndTextureMh = CompressTexture(pack.holdEndTextureMh);

    //     // 3. 打击特效 SpriteFrames
    //     // 注意：如果 TextureSlicer.CreateSpriteFrames 内部使用的是 AtlasTexture，
    //     // 直接压缩会丢失 Region 信息。建议确保 CreateSpriteFrames 生成的是独立 ImageTexture，
    //     if (pack.hitEffectSF != null)
    //     {
    //         string anim = "default";
    //         int frameCount = pack.hitEffectSF.GetFrameCount(anim);
    //         for (int i = 0; i < frameCount; i++)
    //         {
    //             Texture2D frameTex = pack.hitEffectSF.GetFrameTexture(anim, i);
    //             if (frameTex != null)
    //                 pack.hitEffectSF.SetFrame(anim, i, CompressTexture(frameTex));
    //         }
    //     }

    //     GD.Print("[ResourcePack] 压缩资源包纹理完成!");
    // }

    private static Texture2D CompressTexture(Texture2D source)
    {
        if (source == null) return null;

        // 获取 CPU 端图像数据
        Image img = source.GetImage();
        if (img == null) return source;

        // 进行 VRAM 压缩。使用 Srgb 源以正确保留颜色渐变。
        // 关键：压缩为 VRAM Compressed，走 GPU 压缩纹理上传路径
        // PC 平台用 BPTC（高质量），或 S3TC（兼容性好）
        Error err = img.Compress(Image.CompressMode.Bptc, Image.CompressSource.Generic);
        if (err != Error.Ok)
        {
            // BPTC 不支持时回退到 S3TC
            err = img.Compress(Image.CompressMode.S3Tc, Image.CompressSource.Generic);
        }
        if(err != Error.Ok)
        {
            GD.PushWarning($"无法压缩图片, 可能导致白色纹理渲染不正确, 详情见issue#117181");
            return source;
        }

        return ImageTexture.CreateFromImage(img);
    }
}
