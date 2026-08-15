using System;
using System.Collections.Generic;
using Godot;

public static class ResourceManager
{
    /// <summary>当前激活的资源包，由外部（如 EditorScene）加载并赋值</summary>
    public static ResourcePack CurrentPack { get; set; }
    private static readonly Dictionary<string, Func<Resource>> Loaders = new();
    private static readonly Dictionary<string, Resource> Cache = new();

    /// <summary>
    /// 注册资源加载器（通常在模块初始化时调用）
    /// </summary>
    public static void Register<T>(string key, Func<T> loader) where T : Resource
    {
        Loaders[key] = () => loader();
    }

    /// <summary>
    /// 注册基于路径的资源
    /// </summary>
    public static void RegisterPath<T>(string key, string path) where T : Resource
    {
        Loaders[key] = () => GD.Load<T>(path);
    }

    public static T Get<T>(string key) where T : Resource
    {
        if (Cache.TryGetValue(key, out Resource cached))
            return cached as T;

        if (!Loaders.TryGetValue(key, out Func<Resource> loader))
        {
            GD.PushError($"ResourceManager: 未找到 '{key}' 的加载器");
            return null;
        }

        Resource resource = loader();
        if (resource != null) Cache[key] = resource;
        return resource as T;
    }

    public static void Clear() => Cache.Clear();
}