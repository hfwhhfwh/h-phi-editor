using Godot;
using System;

public partial class EffectsManager : Node
{
    // SpriteFrames 资源
    [Export] private SpriteFrames hitFrames;

    public override void _Ready()
    {
        base._Ready();
        hitFrames = ResourceLoader.Load<SpriteFrames>("res://Sprite/HitEffect/hit_effect_sprite_frames.tres");
    }


    /// <summary>
    /// 在指定位置创建一个打击特效
    /// </summary>
    public void CreateHitEffect(Vector2 position)
    {
        // 创建 AnimatedSprite2D 节点
        var animatedSprite = new AnimatedSprite2D
        {
            SpriteFrames = hitFrames,
            Position = position,
            Modulate = new Color
            {
                R8 = 237,
                G8 = 236,
                B8 = 176,
                A8 = 255
            }
        };

        // 播放默认动画
        animatedSprite.Play();
        
        // 连接动画结束信号，播放完后自动销毁
        animatedSprite.AnimationFinished += () => animatedSprite.QueueFree();
        
        // 将特效添加到当前场景（或指定的特效容器节点）
        AddChild(animatedSprite);
    }
}
