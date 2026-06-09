using Godot;
using System;
using System.Collections.Generic;

public static class AudioPool
{
    private static Stack<AudioStreamPlayer> _pool = new();
    private static Node _parent;

    public static void Initialize(Node parent)
    {
        _parent = parent;
        // 预创建几个
        for (int i = 0; i < 10; i++) CreateNew();
    }

    private static void CreateNew()
    {
        var player = new AudioStreamPlayer();
        player.Finished += () => Recycle(player);
        _parent.AddChild(player);
        _pool.Push(player);
    }

    public static AudioStreamPlayer Get()
    {
        if (_pool.Count == 0) CreateNew();
        return _pool.Pop();
    }

    public static void Recycle(AudioStreamPlayer player)
    {
        player.Stop();
        player.Stream = null;
        _pool.Push(player);
    }
}
