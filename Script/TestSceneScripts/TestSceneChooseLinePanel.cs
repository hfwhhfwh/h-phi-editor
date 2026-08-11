using Godot;
using System;
using System.Collections.Generic;

public partial class TestSceneChooseLinePanel : Node
{
    [Export] private ChooseLinePanel chooseLinePanel;
    [Export] private Theme theme;

    public override void _Ready()
    {
        base._Ready();

        PopupMenuHelper.SetTheme(theme);

        List<ChooseLinePanel.LineInfo> infos = [
            new ChooseLinePanel.LineInfo
            {
                Id = 0, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 1, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 2, NoteCount = 789, NextEventTime = 123.4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 0, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 1, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 2, NoteCount = 789, NextEventTime = 123.4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 0, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 1, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 2, NoteCount = 789, NextEventTime = 123.4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 0, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 1, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 2, NoteCount = 789, NextEventTime = 123.4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 0, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 1, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 2, NoteCount = 789, NextEventTime = 123.4f
            },
        ];

        chooseLinePanel.ShowInfos(infos);
        chooseLinePanel.SetEventLayer(2);

        chooseLinePanel.LineSelected += (id) =>
        {
            GD.Print($"[{this.Name}] 用户选择了Line:{id}");
        };

        chooseLinePanel.DeleteLineRequested += (id) =>
        {
            GD.Print($"[{this.Name}] 请求删除Line:{id}");
        };

        chooseLinePanel.AddLineRequested += () =>
        {
            GD.Print($"[{this.Name}] 请求添加Line");
        };

        chooseLinePanel.LayerSelected += (int index) =>
        {
            GD.Print($"[{this.Name}] 用户选择了事件层:{index}");
        };

    }

}
