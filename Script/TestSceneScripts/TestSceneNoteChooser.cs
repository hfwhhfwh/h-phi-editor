using Godot;
using System;

public partial class TestSceneNoteChooser : Node
{
    [Export] private NoteChooser noteChooser;

    public override void _Ready()
    {
        base._Ready();

        noteChooser.OnNoteChoosed += OnNoteChoosed;
        noteChooser.OnDeselected += () =>
        {
            GD.Print($"用户取消选择了note");
        };

    }

    private void OnNoteChoosed(NoteType noteType)
    {
        GD.Print($"用户选择了note:{noteType}");
    }

}
