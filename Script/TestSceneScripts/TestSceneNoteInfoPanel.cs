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

        // 面板现在直接调用 ChartEditService 执行属性修改命令（面板通过 Initialize 注入服务），
        // 本测试场景没有谱面数据，因此只做界面展示。
    }

}
