using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class NoteInfoPanel : Panel
{
	[Export] private InfoEditPanel infoEditPanel;

	private int editingLineId;
	private int editingNoteIndex;
	private Note _note;

	/// <summary>场景级状态中心，用于按对象引用重新定位音符</summary>
	private EditorContext _context;

	/// <summary>编辑命令入口，由 EditorScene 注入</summary>
	private ChartEditService _editService;

	/// <summary>防止刷新过程中再次触发刷新</summary>
	private bool _refreshing;

	[Signal] public delegate void OnConfirmedEventHandler();

	/// <summary>
	/// 注入依赖。面板不再把属性修改抛给上级转发，直接执行命令；
	/// 同时订阅历史变化，撤销/重做后把数据回填到界面。
	/// </summary>
	public void Initialize(EditorContext context, ChartEditService editService)
	{
		_context = context;
		_editService = editService;

		if (_editService != null)
		{
			_editService.HistoryChanged += OnHistoryChanged;
		}
	}
	

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
		if (_editService != null)
		{
			_editService.HistoryChanged -= OnHistoryChanged;
		}

        base._ExitTree();

		//断开信号，防止内存泄漏
		infoEditPanel.PropertyChanged -= OnPropertyChanged;
    }

	/// <summary>
	/// 撤销/重做（或其它来源的命令）之后，把谱面数据的最新值同步到界面。
	/// 音符时间变化会让列表重排，因此这里按对象引用重新定位索引。
	/// </summary>
	private void OnHistoryChanged()
	{
		if (_refreshing || !Visible || _note == null) return;

		int index = IndexOfEditedNote();
		if (index < 0)
		{
			// 音符已被删除（或重做了一次删除），面板没有可编辑对象了
			Visible = false;
			return;
		}

		editingNoteIndex = index;

		_refreshing = true;
		try
		{
			// 只回填数值，不重建控件 —— 重建会打断用户正在进行的拖动
			infoEditPanel.RefreshValues(BuildData(_note, editingNoteIndex));
		}
		finally
		{
			_refreshing = false;
		}
	}

	/// <summary>
	/// 按对象引用查找当前音符在列表中的索引，找不到返回 -1
	/// </summary>
	private int IndexOfEditedNote()
	{
		Chart chart = _context?.EditingChart;
		if (chart?.JudgeLineList == null) return -1;
		if (editingLineId < 0 || editingLineId >= chart.JudgeLineList.Count) return -1;

		List<Note> notes = chart.JudgeLineList[editingLineId].Notes;
		return notes?.IndexOf(_note) ?? -1;
	}


	public void ShowInfo(Note note, int lineId, int noteIndex)
	{
		_note = note;
		editingLineId = lineId;
		editingNoteIndex = noteIndex;

		//更新infoEditPanel的显示内容
		infoEditPanel.ShowInfos(BuildData(note, noteIndex));
	}

	/// <summary>
	/// 把 Note 的当前值整理成 InfoEditPanel 需要的数据
	/// </summary>
	private InfoEditPanel.Data BuildData(Note note, int noteIndex)
	{
		InfoEditPanel.Data data = new();
		data.Name = $"音符{noteIndex}";

		//时间节拍
		Beat startBeat = new Beat(note.StartTime);
		Beat endBeat = new Beat(note.EndTime);

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

		//透明度
		data.Properties["Alpha"] = note.Alpha;

		return data;
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

			case "Alpha":
				propertyType = NotePropertyEnum.Alpha;
				convertedValue = Convert.ToInt32(value);
				break;

			default:
				GD.PrintErr($"[{this.Name}] 未知的键: {key}");
				return;
		}

		// 触发统一的属性变更
		_editService?.SetNoteProperty(editingLineId, editingNoteIndex, propertyType, convertedValue);
	}

}
