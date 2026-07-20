using Godot;
using QuickType;
using System;

public partial class TestSceneNoteInfoPanel : Node
{
    [Export] private NoteInfoPanel noteInfoPanel;

    public override void _Ready()
    {
        base._Ready();

        Note note = new Note
        {
            StartTime = [1, 2, 3],
            EndTime = [4, 5, 6],
            Type = 1,
            PositionX = 100
        };

        noteInfoPanel.ShowInfo(note, 0, 123);
    }

}
