using Godot;
using System;


/// <summary>
/// 基本操做模式
/// </summary>
public enum EditModeEnum
{
    Normal,
    PlacingNote, // 放置note模式
}

public static class EditModeManager
{
    public static EditModeEnum EditMode { get; private set; } = EditModeEnum.Normal;

    public static event Action<EditModeEnum> OnEditModeChanged;

    public static void SetEditMode(EditModeEnum editMode)
    {
        EditMode = editMode;

        OnEditModeChanged?.Invoke(editMode);
    }
}
