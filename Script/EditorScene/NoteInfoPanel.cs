using Godot;
using QuickType;
using System;

public partial class NoteInfoPanel : Panel
{
	[Export] private InfoEditPanel infoEditPanel;

	private int editingLineId;
	private int editingNoteIndex;

	[Signal] public delegate void OnConfirmedEventHandler();

	public Action <int, int, NotePropertyEnum, object> OnNotePropertyChanged;
	

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
		editingLineId = lineId;
		editingNoteIndex = noteIndex;
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
		NotePropertyEnum propertyType;
		object convertedValue;

		switch (key)
		{
			case "StartTime":
				propertyType = NotePropertyEnum.StartTime;
				// infoEditPanel 中存储的是 Beat 对象，需提取其 values 数组
				convertedValue = (Beat)value;
				break;

			case "EndTime":
				propertyType = NotePropertyEnum.EndTime;
				convertedValue = (Beat)value;
				break;

			case "Type":
				propertyType = NotePropertyEnum.Type;
				// value 已经是 NoteType 枚举，直接传递
				convertedValue = value;
				break;

			case "PositionX":
				propertyType = NotePropertyEnum.PosX;
				convertedValue = Convert.ToSingle(value);
				break;

			default:
				GD.PrintErr($"[{this.Name}] 未知的键: {key}");
				return;
		}

		// 触发统一的属性变更事件
		OnNotePropertyChanged?.Invoke(editingLineId, editingNoteIndex, propertyType, convertedValue);
	}

}
