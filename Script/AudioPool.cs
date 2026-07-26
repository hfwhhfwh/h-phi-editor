using Godot;
using System;
using System.Collections.Generic;

public partial class AudioPool : Node
{
    private Stack<AudioStreamPlayer> _pool = new();
    private Node _parent;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="parent">生成的AudioStreamPlayer的父节点</param>
    public AudioPool(Node parent)
    {
        _parent = parent;

        // 预创建几个
        for (int i = 0; i < 10; i++) CreateNew();
    }

    // public void Initialize(Node parent)
    // {
        
    // }

    private void CreateNew()
    {
        var player = new AudioStreamPlayer();
        player.Finished += () => Recycle(player);
        _parent.AddChild(player);
        _pool.Push(player);
    }

    public AudioStreamPlayer Get()
    {
        if (_pool.Count == 0) CreateNew();
        return _pool.Pop();
    }

    public void Recycle(AudioStreamPlayer player)
    {
        player.Stop();
        player.Stream = null;
        _pool.Push(player);
    }
}
