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
                Id = 3, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 4, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 5, NoteCount = 789, NextEventTime = 123.4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 6, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 7, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 8, NoteCount = 789, NextEventTime = 123.4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 9, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 10, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 11, NoteCount = 789, NextEventTime = 123.4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 12, NoteCount = 123, NextEventTime = 0f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 13, NoteCount = 456, NextEventTime = 4f
            },
            new ChooseLinePanel.LineInfo
            {
                Id = 14, NoteCount = 789, NextEventTime = 123.4f
            },
        ];

        chooseLinePanel.ShowInfos(infos);
        chooseLinePanel.SetEventLayer(2);

        chooseLinePanel.LineSelected += (id) =>
        {
            FileLogger.Log($"[{this.Name}] 用户选择了Line:{id}");
        };

        chooseLinePanel.DeleteLineRequested += (id) =>
        {
            FileLogger.Log($"[{this.Name}] 请求删除Line:{id}");
        };

        chooseLinePanel.DeleteSelectedLinesRequested += (lineIds) =>
        {
            FileLogger.Log($"[{this.Name}] 请求批量删除Line:{string.Join(",", lineIds)}");
        };

        chooseLinePanel.AddLineRequested += () =>
        {
            FileLogger.Log($"[{this.Name}] 请求添加Line");
        };

        chooseLinePanel.LayerSelected += (int index) =>
        {
            FileLogger.Log($"[{this.Name}] 用户选择了事件层:{index}");
        };

    }

}
