using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

public class ResourcePack
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
    
    
}
