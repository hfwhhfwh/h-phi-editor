using Godot;
using System;


/// <summary>
/// 基本操做模式
/// </summary>
public enum EditModeEnum
{
    Normal,
    Place, // 放置note或事件模式
    Delete, // 快速删除note或事件模式
}

public static class EditModeManager
{
    public static EditModeEnum EditMode { get; private set; } = EditModeEnum.Normal;

    public static event Action<EditModeEnum> OnEditModeChanged;

    public static void SetEditMode(EditModeEnum editMode)
    {
        EditMode = editMode;

        OnEditModeChanged?.Invoke(editMode);

        GD.Print($"[EditModeManager] 切换到编辑模式:{editMode}");
    }
}
