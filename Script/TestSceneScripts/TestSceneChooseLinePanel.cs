using Godot;
using System;
using System.Collections.Generic;

public partial class TestSceneChooseLinePanel : Node
{
    [Export] private ChooseLinePanel chooseLinePanel;

    public override void _Ready()
    {
        base._Ready();

        List<ChooseLinePanel.LineInfo> infos = [
            new ChooseLinePanel.LineInfo
            {
                Id = 1, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 2, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 3, NoteCount = 789, NextEventTime = 123.4f
            },
        ];

        chooseLinePanel.ShowInfos(infos);

        chooseLinePanel.LineSelected += (id) =>
        {
            GD.Print($"[{this.Name}] 用户选择了Line:{id}");
        };

    }

}
