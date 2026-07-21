using Godot;
using QuickType;
using System;

public partial class NoteInfoPanel : Panel
{
	[Export] private InfoEditPanel infoEditPanel;

	[Signal] public delegate void OnConfirmedEventHandler();

	public Action<Beat> OnStartTimeChanged;
	public Action<Beat> OnEndTimeChanged;
	public Action<NoteType> OnTypeChanged;
	public Action<float> OnPosXChanged;
	

    public override void _Ready()
    {
        base._Ready();

		//infoEditPanel = GetChild<InfoEditPanel>(0);

		//连接信号
		infoEditPanel.OnConfirmed += () =>
		{
			EmitSignal(SignalName.OnConfirmed);	
		};

		infoEditPanel.PropertyChanged += OnPropertyChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();

		//断开信号，防止内存泄漏
		infoEditPanel.PropertyChanged -= OnPropertyChanged;
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

	public void OnPropertyChanged(string key, object value)
	{
		if(key == "StartTime")
		{
			Beat beat = (Beat)value;
			OnStartTimeChanged?.Invoke(beat);
		}
		else if(key == "EndTime")
		{
			Beat beat = (Beat)value;
			OnEndTimeChanged?.Invoke(beat);
		}
		else if(key == "Type")
		{
			NoteType type = (NoteType)value;
			OnTypeChanged?.Invoke(type);
		}
		else if(key == "PositionX")
		{
			float posX = Convert.ToSingle(value);
			OnPosXChanged?.Invoke(posX);
		}
		else
		{
			GD.PrintErr($"[{this.Name}] 未知的键:{key}");
		}
	}

}
