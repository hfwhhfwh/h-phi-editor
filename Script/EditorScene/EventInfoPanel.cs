// using Godot;
// using QuickType;
// using System;

// public partial class EventInfoPanel : Panel
// {
// 	[Export] private InfoEditPanel infoEditPanel;

// 	private int editingLineId;
// 	private LineEventEnum editingLineEventEnum;
// 	private int editingEventIndex;

// 	private EasingData _currentEasingData; // 用于跟踪当前缓动数据的快照，以便比较变化

// 	[Signal] public delegate void OnConfirmedEventHandler();

// 	/// <summary>
// 	/// 当事件的属性发生变化时触发，参数:(判定线编号，事件类型，事件索引，事件属性枚举，修改值)
// 	/// </summary>
// 	public Action <int, LineEventEnum, int, LineEventPropertyType, object> EventPropertyChanged;

// 	public override void _Ready()
//     {
//         base._Ready();

// 		//infoEditPanel = GetChild<InfoEditPanel>(0);

// 		//连接信号
// 		infoEditPanel.OnConfirmed += () =>
// 		{
// 			EmitSignal(SignalName.OnConfirmed);	
// 		};

// 		infoEditPanel.PropertyChanged += OnPropertyChanged;
//     }

//     public override void _ExitTree()
//     {
//         base._ExitTree();

// 		//断开信号，防止内存泄漏
// 		infoEditPanel.PropertyChanged -= OnPropertyChanged;
//     }

// 	public void ShowInfo(LineEvent lineEvent, int lineId, LineEventEnum lineEventEnum, int index)
// 	{
// 		editingLineId = lineId;
// 		editingLineEventEnum = lineEventEnum;
// 		editingEventIndex = index;

//         //更新infoEditPanel的显示内容
//         InfoEditPanel.Data data = new();
//         data.Name = $"事件{lineEventEnum}_{index}";

// 		//时间节拍
//         Beat startBeat = new Beat(lineEvent.StartTime);
//         Beat endBeat = new Beat(lineEvent.EndTime);
        
//         data.Properties["StartTime"] = startBeat;
//         data.Properties["EndTime"] = endBeat;

// 		//数值
// 		data.Properties["Start"] = lineEvent.Start;
// 		data.Properties["End"] = lineEvent.End;

// 		// 缓动
// 		ValueTuple<EasingFunc, EasingIO> tuple = EasingHelper.Convert.NumberToEasing(lineEvent.EasingType);
// 		EasingData easingData = new EasingData
// 		{
// 			EasingFunc = tuple.Item1,
// 			EasingIO = tuple.Item2,
// 			EasingLeft = lineEvent.EasingLeft,
// 			EasingRight = lineEvent.EasingRight
// 		};
// 		data.Properties["Easing"] = easingData;

// 		// 保存当前 EasingData 的深拷贝（用于后续比较）
// 		_currentEasingData = easingData.Duplicate();

//         infoEditPanel.ShowInfos(data);
// 	}

// 	public void OnPropertyChanged(string key, object value)
// 	{
// 		LineEventPropertyType propertyType;
// 		object convertedValue;

// 		switch (key)
// 		{
// 			case "StartTime":
// 				propertyType = LineEventPropertyType.StartTime;
// 				convertedValue = (Beat)value;
// 				break;

// 			case "EndTime":
// 				propertyType = LineEventPropertyType.EndTime;
// 				convertedValue = (Beat)value;
// 				break;

// 			case "Start":
// 				propertyType = LineEventPropertyType.Start;
// 				convertedValue = (float)value;
// 				break;

// 			case "End":
// 				propertyType = LineEventPropertyType.End;
// 				convertedValue = Convert.ToSingle(value);
// 				break;

// 			case "Easing":
// 				// 处理缓动复合属性
// 				EasingData newEasing = (EasingData)value;

// 				// 比较并触发各个子属性的变更
// 				bool funcOrIOChanged = (newEasing.EasingFunc != _currentEasingData.EasingFunc) ||
// 										(newEasing.EasingIO != _currentEasingData.EasingIO);
// 				if (funcOrIOChanged)
// 				{
// 					int newType = EasingHelper.Convert.EasingToNumber(newEasing.EasingFunc, newEasing.EasingIO);
// 					// 注意：若新类型无效（-1），则不触发或处理默认值
// 					if (newType != -1)
// 					{
// 						EventPropertyChanged?.Invoke(editingLineId, editingLineEventEnum, editingEventIndex,
// 							LineEventPropertyType.EasingType, newType);
// 					}
// 				}
// 				if (newEasing.EasingLeft != _currentEasingData.EasingLeft)
// 				{
// 					EventPropertyChanged?.Invoke(editingLineId, editingLineEventEnum, editingEventIndex,
// 						LineEventPropertyType.EasingLeft, newEasing.EasingLeft);
// 				}
// 				if (newEasing.EasingRight != _currentEasingData.EasingRight)
// 				{
// 					EventPropertyChanged?.Invoke(editingLineId, editingLineEventEnum, editingEventIndex,
// 						LineEventPropertyType.EasingRight, newEasing.EasingRight);
// 				}

// 				// 更新快照为当前值
// 				_currentEasingData = newEasing.Duplicate();
// 				return; // 已经手动触发事件，直接返回，避免再触发复合事件
			
// 			default:
// 				GD.PrintErr($"[{this.Name}] 未知的键: {key}");
// 				return;
// 		}

// 		// 触发单一属性变更事件（非 Easing 分支）
//     	EventPropertyChanged?.Invoke(editingLineId, editingLineEventEnum, editingEventIndex, propertyType, convertedValue);
// 	}
// }
