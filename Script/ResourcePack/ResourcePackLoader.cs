using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public static class ResourcePackLoader
{
    public static readonly string Name = nameof(ResourcePackLoader);
    // 必需文件清单
    private static readonly string[] RequiredTextures = new[]
    {
        "click", "click_mh", "drag", "drag_mh",
        "flick", "flick_mh", "hold", "hold_mh", "hit_fx"
    };

    public static ResourcePack LoadFromZip(string zipPath)
    {
        ResourcePack pack = new ResourcePack();
        string tempDir = Path.Combine(OS.GetUserDataDir(), "temp_pack", GD.Randi().ToString());

        // 1. 解压 ZIP
        FileUtil.UnzipFileTo(zipPath, tempDir);

        // 2. 解析 info.yml → PackManifest
        Dictionary<string, string> infoDic = FileUtil.ReadInfoFile(Path.Combine(tempDir, "info.yml"));
        pack.Manifest = new PackManifest
        {
            Name = infoDic.GetValueOrDefault("name"),
            Author = infoDic.GetValueOrDefault("author"),
            Description = infoDic.GetValueOrDefault("description"),
            HitFxGrid = infoDic.GetValueOrDefault("hitFx").StringToVector2I(),
            HoldAtlas = infoDic.GetValueOrDefault("holdAtlas").StringToVector2I(),
            HoldAtlasMH = infoDic.GetValueOrDefault("holdAtlasMH").StringToVector2I(),
        };

        // 3. 加载图片
        foreach (string name in RequiredTextures)
        {
            string path = Path.Combine(tempDir, $"{name}.png");
            if (Godot.FileAccess.FileExists(path))
            {
                pack.textureDic[name] = FileUtil.LoadTextureFromFile(path, out string _);
                // TODO 这里不好，可以考虑在FileUtil内部直接修改错误格式
            }
                
        }

        // 4. 加载音频（OGG，TODO 强制检查 44100Hz）
        foreach (var name in new[] { "click", "drag", "flick", "ending" })
        {
            string path = Path.Combine(tempDir, $"{name}.ogg");
            if (Godot.FileAccess.FileExists(path))
                pack.sxDic[name] = FileUtil.LoadAudioFromFile(path);
        }
        
        // 5. 构建贴图
        pack.Build();

        // 5. 验证 TODO
        
        // 6. 清理临时文件

        GD.Print($"[{Name}] 成功加载资源包:{pack.Manifest.Name}");
        
        return pack;
    }

    private static Vector2I StringToVector2I(this string text)
    {
        // 去掉方括号和空白
        string cleaned = text.Trim().TrimStart('[').TrimEnd(']');

        // 按逗号分割，支持中英文逗号
        string[] parts = cleaned.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length != 2)
            throw new FormatException($"无法解析为 Vector2I: {text}");
        
        return new Vector2I(
            int.Parse(parts[0].Trim()),
            int.Parse(parts[1].Trim())
        );
    }
}
