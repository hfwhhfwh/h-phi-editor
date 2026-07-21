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

        noteInfoPanel.OnStartTimeChanged += (Beat beat) => 
        {
            GD.Print($"[{this.Name}] 用户修改了StartTime:[{beat.values[0]},{beat.values[1]},{beat.values[2]}]");
        };

        noteInfoPanel.OnEndTimeChanged += (Beat beat) => 
        {
            GD.Print($"[{this.Name}] 用户修改了EndTime:[{beat.values[0]},{beat.values[1]},{beat.values[2]}]");
        };

        noteInfoPanel.OnPosXChanged += (float posX) =>
        {
            GD.Print($"[{this.Name}] 用户修改了posX:{posX}");
        };

        noteInfoPanel.OnTypeChanged += (NoteType type) =>
        {
            GD.Print($"[{this.Name}] 用户修改了NoteType:{type}");
        };
    }

}
