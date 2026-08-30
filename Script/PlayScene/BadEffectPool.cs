using System;
using System.Collections.Generic;
using Godot;

public partial class BadEffectPool : Node
{
    private Node _parent;
    private Texture2D _texture;
    private Stack<Sprite2D> _pool = new();
    private int _initSize;
    private Color _defaultModulate;

    public BadEffectPool(Node parent, Texture2D tex, int initSize = 50)
    {
        _parent = parent;
        _texture = tex;
        _defaultModulate = new Color("#6C4244"); // 深红
        _initSize = initSize;

        for (int i = 0; i < _initSize; i++)
        {
            _pool.Push(CreateNewEffect());
        }
    }

    private Sprite2D CreateNewEffect()
    {
        return new Sprite2D
        {
            Texture = _texture,
            Modulate = _defaultModulate,
            ZIndex = 3,
            Visible = false
        };
    }

    public Sprite2D Get()
    {
        return _pool.Count > 0 ? _pool.Pop() : CreateNewEffect();
    }

    private void ReturnEffect(Sprite2D fx)
    {
        // 停止并清理关联的 Tween（如果存在）
        if (fx.HasMeta("tween"))
        {
            Tween tween = fx.GetMeta("tween").As<Tween>();
            if (tween != null && tween.IsValid())
            {
                tween.Kill(); // 停止动画
            }
            fx.RemoveMeta("tween");
        }

        // 从父节点移除
        if (fx.GetParent() != null)
            fx.GetParent().RemoveChild(fx);

        // 重置状态
        fx.Visible = false;
        fx.Position = Vector2.Zero;
        fx.Modulate = _defaultModulate; // 颜色恢复默认（alpha=1）

        _pool.Push(fx);
    }

    public void Spawn(Vector2 position, Vector2 scale)
    {
        Spawn(position, scale, _defaultModulate);
    }

    public void Spawn(Vector2 position, Vector2 scale, Color modulate)
    {
        Sprite2D fx = Get();
        fx.Position = position;
        fx.Scale = scale;
        modulate.A = 1.0f; // 确保初始完全不透明
        fx.Modulate = modulate;
        fx.Visible = true;
        _parent.AddChild(fx);

        // 创建淡出动画
        Tween tween = fx.CreateTween();
        tween.TweenProperty(fx, "modulate:a", 0.0f, 0.5f)
             .SetTrans(Tween.TransitionType.Linear);
        
        // 存储 Tween 引用，以便回收时停止
        fx.SetMeta("tween", tween);
        // 动画完成时自动回收
        tween.Finished += () => ReturnEffect(fx);
    }
}