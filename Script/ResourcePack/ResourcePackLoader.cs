using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

public static class ResourcePackLoader
{
    public static readonly string Name = nameof(ResourcePackLoader);
    // 必需文件清单
    private static readonly string[] RequiredTextures = new[]
    {
        "click", "click_mh", "drag", "drag_mh",
        "flick", "flick_mh", "hold", "hold_mh", "hit_fx"
    };

    private const string LocalPackPath = "res://ResourcePacks/";

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

        // 其他可选属性
        string[] optional = ["hitFxDuration", "hitFxScale", "hideParticles", "hitFxRotate", "hitFxTinted"];
        Type[] types = [typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(bool)];
        for (int i = 0; i < optional.Length; i++)
        {
            if (infoDic.TryGetValue(optional[i], out string text))
            {
                try
                {
                    object value = Convert.ChangeType(text, types[i]);
                    switch (optional[i])
                    {
                        case "hitFxDuration":
                            pack.Manifest.HitFxDuration = (float)value;
                            break;
                        case "hitFxScale":
                            pack.Manifest.HitFxScale = (float)value;
                            break;
                        case "hideParticles":
                            pack.Manifest.HideParticles = (bool)value;
                            break;
                        case "hitFxRotate":
                            pack.Manifest.HitFxRotate = (bool)value;
                            break;
                        case "hitFxTinted":
                            pack.Manifest.HitFxTinted = (bool)value;
                            break;
                    }
                }
                catch(Exception e)
                {
                    GD.PrintErr($"无法解析资源包属性: {optional[i]}, 错误:{e.Message}");
                }
            }
        }

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
            {
                pack.sxDic[name] = FileUtil.LoadAudioFromFile(path);

                // 缓存原始 OGG 字节，供导出使用
                using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file != null)
                    pack.audioRawData[name] = file.GetBuffer((long)file.GetLength());
            }
                
        }
        
        // 5. 构建贴图
        pack.Build();

        // 5. 验证 TODO
        
        // 6. 清理临时文件

        GD.Print($"[{Name}] 成功加载资源包:{pack.Manifest.Name}");
        
        return pack;
    }

    // /// <summary>
    // /// 将 ResourcePack 导出为 ZIP 文件
    // /// </summary>
    // public static void ExportToZip(ResourcePack pack, string zipPath)
    // {
    //     if (pack?.Manifest == null)
    //         throw new ArgumentException("资源包或清单为空");

    //     string tempDir = Path.Combine(OS.GetUserDataDir(), "temp_export", GD.Randi().ToString());
    //     Directory.CreateDirectory(tempDir);

    //     try
    //     {
    //         // 1. 生成 info.yml
    //         WriteManifestToYml(pack.Manifest, Path.Combine(tempDir, "info.yml"));

    //         // 2. 导出纹理为 PNG
    //         foreach (var kvp in pack.textureDic)
    //         {
    //             string fileName = $"{kvp.Key}.png";
    //             string outputPath = Path.Combine(tempDir, fileName);
                
    //             Image img = kvp.Value?.GetImage();
    //             if (img == null)
    //             {
    //                 GD.PrintErr($"[{Name}] 无法获取纹理图像: {kvp.Key}");
    //                 continue;
    //             }
                
    //             Error err = img.SavePng(outputPath);
    //             if (err != Error.Ok)
    //                 GD.PrintErr($"[{Name}] 保存纹理失败: {kvp.Key}, 错误: {err}");
    //         }

    //         // 3. 导出音频（使用缓存的原始字节）
    //         foreach (var kvp in pack.audioRawData)
    //         {
    //             string outputPath = Path.Combine(tempDir, $"{kvp.Key}.ogg");
    //             File.WriteAllBytes(outputPath, kvp.Value);
    //         }

    //         // 4. 打包为 ZIP
    //         if (Godot.FileAccess.FileExists(zipPath))
    //             Godot.DirAccess.RemoveAbsolute(zipPath);

    //         ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);

    //         GD.Print($"[{Name}] 成功导出资源包到: {zipPath}");
    //     }
    //     catch (Exception e)
    //     {
    //         GD.PrintErr($"[{Name}] 导出资源包失败: {e.Message}");
    //         throw;
    //     }
    //     finally
    //     {
    //         // 5. 清理临时目录
    //         // try
    //         // {
    //         //     if (Directory.Exists(tempDir))
    //         //         Directory.Delete(tempDir, true);
    //         // }
    //         // catch (Exception e)
    //         // {
    //         //     GD.PrintErr($"[{Name}] 清理临时目录失败: {e.Message}");
    //         // }
    //     }
    // }

    public static void CopyToLocal(string zipPath)
    {
        string id = Util.GenerateRandomId(16);
        string localDir = Path.Combine(LocalPackPath, id, zipPath.GetFile());

        ZipFile.ExtractToDirectory(zipPath, localDir);

    }

    public static void ExportFromLocal(string id, string exportPath)
    {
        string localDir = Path.Combine(LocalPackPath, id);

        ZipFile.CreateFromDirectory(localDir, exportPath);

        // string localFile = FileUtil.GetFirstFileAlphabetically(localDir, true);
        // FileUtil.CopyFile(localFile, exportPath);
    }

    public static ResourcePack LoadFromLocal(string id)
    {
        string localDir = Path.Combine(LocalPackPath, id);
        ResourcePack pack = new ResourcePack();
        
        // 1. 解析 info.yml → PackManifest
        Dictionary<string, string> infoDic = FileUtil.ReadInfoFile(Path.Combine(localDir, "info.yml"));
        pack.Manifest = new PackManifest
        {
            Name = infoDic.GetValueOrDefault("name"),
            Author = infoDic.GetValueOrDefault("author"),
            Description = infoDic.GetValueOrDefault("description"),
            HitFxGrid = infoDic.GetValueOrDefault("hitFx").StringToVector2I(),
            HoldAtlas = infoDic.GetValueOrDefault("holdAtlas").StringToVector2I(),
            HoldAtlasMH = infoDic.GetValueOrDefault("holdAtlasMH").StringToVector2I(),
        };

        // 其他可选属性
        string[] optional = ["hitFxDuration", "hitFxScale", "hideParticles", "hitFxRotate", "hitFxTinted"];
        Type[] types = [typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(bool)];
        for (int i = 0; i < optional.Length; i++)
        {
            if (infoDic.TryGetValue(optional[i], out string text))
            {
                try
                {
                    object value = Convert.ChangeType(text, types[i]);
                    switch (optional[i])
                    {
                        case "hitFxDuration":
                            pack.Manifest.HitFxDuration = (float)value;
                            break;
                        case "hitFxScale":
                            pack.Manifest.HitFxScale = (float)value;
                            break;
                        case "hideParticles":
                            pack.Manifest.HideParticles = (bool)value;
                            break;
                        case "hitFxRotate":
                            pack.Manifest.HitFxRotate = (bool)value;
                            break;
                        case "hitFxTinted":
                            pack.Manifest.HitFxTinted = (bool)value;
                            break;
                    }
                }
                catch(Exception e)
                {
                    GD.PrintErr($"无法解析资源包属性: {optional[i]}, 错误:{e.Message}");
                }
            }
        }

        // 2. 加载图片
        foreach (string name in RequiredTextures)
        {
            string path = Path.Combine(localDir, $"{name}.png");
            if (Godot.FileAccess.FileExists(path))
            {
                pack.textureDic[name] = FileUtil.LoadTextureFromFile(path, out string _);
                // TODO 这里不好，可以考虑在FileUtil内部直接修改错误格式
            }
                
        }

        // 3. 加载音频（OGG，TODO 强制检查 44100Hz）
        foreach (var name in new[] { "click", "drag", "flick", "ending" })
        {
            string path = Path.Combine(localDir, $"{name}.ogg");
            if (Godot.FileAccess.FileExists(path))
            {
                pack.sxDic[name] = FileUtil.LoadAudioFromFile(path);

                // 缓存原始 OGG 字节，供导出使用
                using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file != null)
                    pack.audioRawData[name] = file.GetBuffer((long)file.GetLength());
            }
                
        }
        
        // 4. 构建贴图
        pack.Build();

        // 5. 验证 TODO
        
        // 6. 清理临时文件

        GD.Print($"[{Name}] 成功导入资源包:{pack.Manifest.Name}");
        
        return pack;
    }

    /// <summary>
    /// 将 PackManifest 写入为 info.yml
    /// </summary>
    private static void WriteManifestToYml(PackManifest manifest, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"name: {EscapeYaml(manifest.Name)}");
        sb.AppendLine($"author: {EscapeYaml(manifest.Author)}");
        sb.AppendLine($"description: {EscapeYaml(manifest.Description)}");
        sb.AppendLine($"hitFx: [{manifest.HitFxGrid.X},{manifest.HitFxGrid.Y}]");
        sb.AppendLine($"holdAtlas: [{manifest.HoldAtlas.X},{manifest.HoldAtlas.Y}]");
        sb.AppendLine($"holdAtlasMH: [{manifest.HoldAtlasMH.X},{manifest.HoldAtlasMH.Y}]");
        sb.AppendLine($"hitFxDuration: {manifest.HitFxDuration}");
        sb.AppendLine($"hitFxScale: {manifest.HitFxScale}");
        sb.AppendLine($"hideParticles: {manifest.HideParticles.ToString().ToLower()}");
        sb.AppendLine($"hitFxRotate: {manifest.HitFxRotate.ToString().ToLower()}");
        sb.AppendLine($"hitFxTinted: {manifest.HitFxTinted.ToString().ToLower()}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 简单的 YAML 字符串转义（处理特殊字符和换行）
    /// </summary>
    private static string EscapeYaml(string text)
    {
        if (string.IsNullOrEmpty(text)) return "\"\"";
        
        // 如果包含特殊字符，用引号包裹
        if (text.Contains(':') || text.Contains('#') || text.Contains('\n') || 
            text.Contains('\"') || text.StartsWith(" ") || text.StartsWith("-"))
        {
            return $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")}\"";
        }
        return text;
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
