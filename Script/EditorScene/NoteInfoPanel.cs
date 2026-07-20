using Godot;
using QuickType;
using System;

public partial class NoteInfoPanel : Panel
{
	[Export] private InfoEditPanel infoEditPanel;

	[Signal] public delegate void OnConfirmedEventHandler();

    public override void _Ready()
    {
        base._Ready();

		//infoEditPanel = GetChild<InfoEditPanel>(0);

		//连接信号
		infoEditPanel.OnConfirmed += () =>
		{
			EmitSignal(SignalName.OnConfirmed);	
		};
    }

	public void ShowInfo(Note note, int lineId, int noteIndex)
	{
        //更新infoEditPanel的显示内容
        InfoEditPanel.Data data = new();
        data.Name = $"音符{noteIndex}";

		//时间节拍
        Beat startBeat = new Beat
        {
            values = note.StartTime
        };
        Beat endBeat = new Beat
        {
            values = note.EndTime
        };
        data.Properties["StartTime"] = startBeat;
        data.Properties["EndTime"] = endBeat;

		//类型
		data.Properties["Type"] = note.Type switch
		{
			1 => NoteType.Tap,
			2 => NoteType.Hold,
			3 => NoteType.Flick,
			4 => NoteType.Drag,
			_ => NoteType.Tap
		};

		//位置
		data.Properties["PositionX"] = note.PositionX;

        infoEditPanel.ShowInfos(data);
	}

}
