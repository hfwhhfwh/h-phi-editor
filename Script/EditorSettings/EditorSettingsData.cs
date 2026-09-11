using Godot;
using System;

public partial class EditorSettingsData : Resource
{
    // 配置格式版本。新增或修改设置字段时，应在这里递增并实现对应迁移逻辑。
    public const int CurrentVersion = 1;

    // 网格设置会随当前谱面保存，新增公开属性后会自动参与配置读写。
    [Export] public int verLineCount { get; set; } = 21;
    [Export] public int subBeatCount { get; set; } = 4;

    // 使用 Godot Resource 的复制能力，避免默认设置和当前设置共享同一实例。
    public EditorSettingsData Clone()
    {
        return (EditorSettingsData)Duplicate();
    }
}
