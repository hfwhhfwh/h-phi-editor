using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class HitEffectPool : Node
{
    private Node _parent;                  // 用于添加/移除特效节点
    private SpriteFrames _frames;          // 动画资源
    private Stack<AnimatedSprite2D> _pool = new();
    private int _initSize;
    private Color _modulate;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="parent">特效节点将被添加为此节点的子节点（通常是调用方节点）</param>
    /// <param name="frames">SpriteFrames 资源</param>
    /// <param name="modulate">特效颜色（默认金色）</param>
    /// <param name="initSize">初始池大小</param>
    public HitEffectPool(Node parent, SpriteFrames frames, int initSize = 50)
    {
        _parent = parent;
        _frames = frames;
        _modulate = new Color
        {
            R8 = 237,
            G8 = 236,
            B8 = 176,
            A8 = 255
        };
        _initSize = initSize;

        for (int i = 0; i < _initSize; i++)
        {
            CreateNewEffect();
        }
    }

    private AnimatedSprite2D CreateNewEffect()
    {
        var fx = new AnimatedSprite2D
        {
            SpriteFrames = _frames,
            Modulate = _modulate,
            ZIndex = 3,
            Visible = false
        };
        // 动画播放完毕时自动回收
        fx.AnimationFinished += () => ReturnEffect(fx);
        return fx;
    }

    /// <summary>
    /// 从池中获取一个特效节点（已从场景树移除，需要调用方重新添加）
    /// </summary>
    public AnimatedSprite2D Get()
    {
        if (_pool.Count > 0)
            return _pool.Pop();

        // 池空则动态扩容
        return CreateNewEffect();
    }

    private void ReturnEffect(AnimatedSprite2D fx)
    {
        // 如果还在场景树中，移除
        if (fx.GetParent() != null)
            fx.GetParent().RemoveChild(fx);

        // 重置状态
        fx.Visible = false;
        fx.Position = Vector2.Zero;
        fx.Stop();
        fx.Frame = 0;

        // 放回池中
        _pool.Push(fx);
    }

    /// <summary>
    /// 便捷方法：在指定位置生成一个特效并自动播放
    /// </summary>
    /// <param name="position">全局或局部坐标（相对于 _parent）</param>
    public void Spawn(Vector2 position)
    {
        var fx = Get();
        fx.Position = position;
        fx.Visible = true;
        _parent.AddChild(fx);
        fx.Play();
    }
}
