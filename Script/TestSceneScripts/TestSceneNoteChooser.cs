using Godot;
using System;

public partial class TestSceneNoteChooser : Node
{
    [Export] private NoteChooser noteChooser;

    public override void _Ready()
    {
        base._Ready();

        noteChooser.NoteChoosed += OnNoteChoosed;
        noteChooser.Deselected += OnDeselected;
        //noteChooser.DeleteButtonChoosed += OnDeleteButtonChoosed;

    }

    public override void _ExitTree()
    {
        base._ExitTree();

        noteChooser.NoteChoosed -= OnNoteChoosed;
        noteChooser.Deselected -= OnDeselected;
        //noteChooser.DeleteButtonChoosed -= OnDeleteButtonChoosed;
    }


    private void OnNoteChoosed(NoteType noteType)
    {
        FileLogger.Log($"用户选择了note:{noteType}");
    }

    private void OnDeselected()
    {
        FileLogger.Log($"用户取消选择");
    }

    private void OnDeleteButtonChoosed()
    {
        FileLogger.Log($"用户选择了删除模式");
    }

}
