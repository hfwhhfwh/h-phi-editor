using Godot;
using System;
using QuickType;
using System.Collections.Generic;
using System.Linq;

public partial class EventEditPanel : BaseEditPanel
{
	private enum VerAlign
	{
		Top, Center, Bottom
	}

	// [ExportGroup("事件特有设置")]
    [Export] private float widthScale = 0.4f;
    [Export] private Texture2D eventHoldTexture;
	[Export] private float hideTextThreshold = 50f;

	[Export] private Color selectedModulate = new Color(1f, 0.223f, 0.947f, 1f);
    [Export] private Color deleteHighlightModulate = new Color(1f, 0.184f, 0, 1f);
    [Export] private Color toAddModulate = new Color(1f, 1f, 1f, 0.588f);

	private Control _textOverlay; // 用于显示上层的提示文字

	public int EditingLayer { get; set; } = 0;


	private readonly HashSet<LineEvent> selectedEvents = new();
    private readonly HashSet<LineEvent> eventsToDelete = new();

	private readonly List<(LineEventEnum Type, LineEvent Evt)> _selectedEventsWithType = new();

	/// <summary>
	/// 当事件被选择时发出，参数:(判定线编号，事件层，事件类型，事件索引，弹窗位置（坐标系：viewportCoord）)
	/// </summary>
	public event Action<int, int, LineEventEnum, int, Vector2> EventSelected;
	/// <summary>
	/// 请求添加一个事件，参数:(判定线编号，事件层，事件类型，起始Beat，结束Beat)
	/// </summary>
	public event Action<int, int, LineEventEnum, Beat, Beat> AddEventRequested;

	/// <summary>
    /// 请求修改 Event 时间，参数:(判定线编号, 事件层, 事件类型, 事件索引, 新StartTime, 新EndTime)
    /// </summary>
    public event Action<int, int, LineEventEnum, int, Beat, Beat> EventTimeChangeRequested;
    public event Action<int, int, LineEventEnum, LineEvent> EventDragStarted;
    public event Action<int, int, LineEventEnum, LineEvent> EventDragEnded;

	/// <summary>
	/// 当有事件被多选时触发
	/// </summary>
	public event Action EventMultiSelected;

	private LineEventClipBoard _lineEventClipBoard;
	private Beat _pasteTargetBeat;
	private Beat _pasteBeatDelta;

	public Beat PasteTargetBeat => _pasteTargetBeat;

	public IReadOnlyCollection<(LineEventEnum Type, LineEvent Evt)> SelectedEventsWithType 
		=> _selectedEventsWithType;

	/// <summary>
	/// 字典：每一种事件类型对应的竖线编号
	/// 键：LineEventEnum，值：竖线编号(int)
	/// </summary>
	/// <returns></returns>
	private Dictionary<LineEventEnum, int> EventTypeToIndexDic = new()
    {
		{LineEventEnum.MoveX, 0},
		{LineEventEnum.MoveY, 1},
		{LineEventEnum.Rotate, 2},
		{LineEventEnum.Alpha, 3},
		{LineEventEnum.Speed, 4},
	};

	private Dictionary<int, LineEventEnum> IndexToEventTypeDic = new()
	{
		{0, LineEventEnum.MoveX},
		{1, LineEventEnum.MoveY},
		{2, LineEventEnum.Rotate},
		{3, LineEventEnum.Alpha},
		{4, LineEventEnum.Speed},
	};

	/// <summary>
	/// 获取某种事件类型在面板上的谱面X坐标
	/// 注：事件实际上没有谱面X坐标，但是为了方便，类比NoteEditPanel，给予事件[-675,675]的X坐标
	/// </summary>
	/// <param name="lineEventEnum"></param>
	/// <returns></returns>
	private float EventTypeToChartX(LineEventEnum lineEventEnum)
	{
		int verLineIndex = EventTypeToIndexDic[lineEventEnum];
		float chartX = -675f + (1350f / (VerLineCount - 1)) * verLineIndex;

		return chartX;
	}

	private float EventTypeToRatioX(LineEventEnum lineEventEnum)
	{
		int verLineIndex = EventTypeToIndexDic[lineEventEnum];
		float ratioX = verLineIndex * 1f / (VerLineCount - 1);

		return ratioX;
	}

    public override void _Ready()
    {
		base._Ready();

		// 固定竖线数为5
        VerLineCount = 5;

		//设置multiMesh
		RegisterMultiMesh("Event", eventHoldTexture, 4096, 1);

		// 设置_textOverlay
		_textOverlay = new Control();
		_textOverlay.Name = "TextOverlay";
		_textOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		_textOverlay.ZIndex = 2; // 显示在最上层
		_textOverlay.Draw += OnDrawTextOverlay;
		_textOverlay.MouseFilter = MouseFilterEnum.Ignore; // 放置拦截点击
		AddChild(_textOverlay);

		_dragMoveComponent.Moved += OnEventDragMoved;
        _dragMoveComponent.Started += OnEventDragStarted;
        _dragMoveComponent.Ended += OnEventDragEnded;
    }

    public override void _ExitTree()
    {
		_dragMoveComponent.Moved -= OnEventDragMoved;
        _dragMoveComponent.Started -= OnEventDragStarted;
        _dragMoveComponent.Ended -= OnEventDragEnded;

        base._ExitTree();
    }


	private void DrawTextAligned(Vector2 pos, string text, int fontSize, VerAlign align)
	{
		// 1. 测量文本尺寸
		Vector2 textSize = font.GetStringSize(text, fontSize: fontSize);

		float descent = font.GetDescent(fontSize);
		float ascent = font.GetAscent(fontSize);

		float drawY = pos.Y + (ascent - descent) / 2;
		drawY = align switch
		{
			VerAlign.Top => pos.Y + ascent,
			VerAlign.Center => pos.Y + (ascent - descent) / 2,
			VerAlign.Bottom => pos.Y - descent,
			_ => pos.Y + (ascent - descent) / 2,
		};
		// {
		// 	Vector2 from = new Vector2(pos.X - textSize.X / 2, drawY);
		// 	Vector2 to = new Vector2(pos.X + textSize.X / 2, drawY);
		// 	_textOverlay.DrawLine(from, to, Colors.SkyBlue, 2);
		// }
		

		// 2. 计算绘制位置（水平居中 + 垂直居中）
		//    水平：中心点减去半宽
		//    垂直：因为 pos 是基线位置，简单近似可用 center + 半高
		Vector2 drawPos = new Vector2(
			pos.X - textSize.X / 2,
			drawY
		);

		// Vector2 drawPos = pos;

		_textOverlay.DrawString(font, drawPos, text, fontSize: fontSize, alignment: HorizontalAlignment.Center);
}

	private void OnDrawTextOverlay()
	{
		// ================ 绘制事件的起始和末尾值 ================
		// 如果没有可用的谱面或判定线，则隐藏所有池节点
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Count)
		{
			return;
		}

		GetVisibleBeatRange(out float minBeat, out float maxBeat);

		foreach(LineEventEnum eventType in AllTypes.allLineEventTypes)
		{
			List<LineEvent> lineEvents = 
				editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer].GetLineEvents(eventType);

			float ratioX = EventTypeToIndexDic[eventType] * 1f / (AllTypes.allLineEventTypes.Length-1);
			float panelPosX = VerMargin + ratioX * (Size.X - 2 * VerMargin);
			
			for (int i = 0; i < lineEvents.Count; i++)
			{
				LineEvent lineEvent = lineEvents[i];

				// ---- 快速视口裁剪：利用预计算的 startSec 或 beat 值 ----
				float startBeatValue = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
				float endBeatValue = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
				if (startBeatValue > maxBeat || endBeatValue < minBeat) continue;

				// 绘制文字
				float startPanelPosY = _coordComponent.GetPanelPosY(startBeatValue);
				float endPanelPosY = _coordComponent.GetPanelPosY(endBeatValue);

				if(startPanelPosY - endPanelPosY < hideTextThreshold) continue;
				
				DrawTextAligned(new Vector2(panelPosX, startPanelPosY), $"{lineEvent.Start:F1}", 24, VerAlign.Bottom);
				DrawTextAligned(new Vector2(panelPosX, endPanelPosY), $"{lineEvent.End:F1}", 24, VerAlign.Top);
			}
				
		}

	}



    protected override void RenderContent()
    {
        // 如果没有可用的谱面或判定线，则隐藏所有
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Count ||
			EditingLayer < 0 ||
			EditingLayer >= editingChart.JudgeLineList[editingLineId].EventLayers.Count)
		{
			return;
		}

		GetVisibleBeatRange(out float minBeat, out float maxBeat);

		List<LineEvent> moveXEvents = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer].MoveXEvents;
		List<LineEvent> moveYEvents = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer].MoveYEvents;
		List<LineEvent> rotateEvents = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer].RotateEvents;
		List<LineEvent> alphaEvents = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer].AlphaEvents;
		List<LineEvent> speedEvents = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer].SpeedEvents;

		// int visibleCount = 0;

		var allEvents = new (List<LineEvent> events, float xRatio)[]
        {
            (moveXEvents, EventTypeToRatioX(LineEventEnum.MoveX)),
            (moveYEvents, EventTypeToRatioX(LineEventEnum.MoveY)),
            (rotateEvents, EventTypeToRatioX(LineEventEnum.Rotate)),
            (alphaEvents, EventTypeToRatioX(LineEventEnum.Alpha)),
            (speedEvents, EventTypeToRatioX(LineEventEnum.Speed))
        };

		// ========== 2. 动态扩容 ==========
		{
			int count = 0;
			if(moveXEvents != null) count += moveXEvents.Count;
			if(moveYEvents != null) count += moveYEvents.Count;
			if(rotateEvents != null) count += rotateEvents.Count;
			if(alphaEvents != null) count += alphaEvents.Count;
			if(speedEvents != null) count += speedEvents.Count;
            
            EnsureMultiMeshCapacity("Event", count);
        }

		// 为实际存在的 event 渲染
		foreach((List<LineEvent> events, float xRatio) type in allEvents)
		{
			if(type.events == null || type.events.Count == 0) continue;
			for (int i = 0; i < type.events.Count; i++)
			{
				LineEvent lineEvent = type.events[i];

				// ---- 快速视口裁剪：利用预计算的 startSec 或 beat 值 ----
				float startBeatValue = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
				float endBeatValue = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
				if (startBeatValue > maxBeat || endBeatValue < minBeat) continue;

				float localX = VerMargin + type.xRatio * (Size.X - 2 * VerMargin);
				Beat startBeat = new Beat(lineEvent.StartTime);
				Beat endBeat = new Beat(lineEvent.EndTime);

				Action<MultiMesh, int> renderEffect = null;
				//选中效果
				if (selectedEvents.Contains(lineEvent))
				{
					renderEffect = SelectedRender;
				}
				//即将删除的高亮效果
				if (eventsToDelete.Contains(lineEvent))
				{
					renderEffect = AboutToDeleteRender;
				}

				//使用MultimeshInstance渲染
				RenderLongObject(
					key: "Event",
					localX: localX,
					startBeat: startBeat,
					endBeat: endBeat,
					offset: Vector2.Zero,
					scale: widthScale,
					renderEffect: renderEffect
				);
				
			}
		}
		
		// 额外绘制即将创建的Event
        if(_dragPlaceComponent.IsDragging){
            float chartPosX = -675 + _dragPlaceComponent.verLineIndex * (1350f / (VerLineCount - 1));
			float localX = _coordComponent.GetPanelPosX(chartPosX);

            RenderLongObject(
                key: "Event",
				localX: localX,
				startBeat: _dragPlaceComponent.StartBeat, // 确保startBeat和endBeat的大小关系正确
                endBeat: _dragPlaceComponent.EndBeat,
				offset: Vector2.Zero,
				scale: widthScale,
				renderEffect: ToAddRender
            );
        }

		if (_isPasteMode && _lineEventClipBoard != null && _lineEventClipBoard.Events != null && _lineEventClipBoard.Events.Count != 0)
		{
			foreach (var item in _lineEventClipBoard.Events)
			{
				if (_pasteBeatDelta == null) break;
				float localX = VerMargin + EventTypeToRatioX(item.Type) * (Size.X - 2 * VerMargin);
				Beat startBeat = new Beat(item.Snapshot.StartTime) + _pasteBeatDelta;
				Beat endBeat = new Beat(item.Snapshot.EndTime) + _pasteBeatDelta;
				RenderLongObject(
					key: "Event",
					localX: localX,
					startBeat: startBeat,
					endBeat: endBeat,
					offset: Vector2.Zero,
					scale: widthScale,
					renderEffect: ToAddRender
				);
			}
		}

		// ============== 绘制事件值提示文本 ==============
		_textOverlay.QueueRedraw();
    }

	private void SelectedRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, selectedModulate);
    }

    private void AboutToDeleteRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, deleteHighlightModulate);
    }

    private void ToAddRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, toAddModulate);
    }

	public override void DeselectAll()
	{
		selectedEvents.Clear();

		_selectedEventsWithType.Clear();

		EmitAllDeselected();
	}

	public void StartPaste(LineEventClipBoard lineEventClipBoard)
	{
		_isPasteMode = true;
		_lineEventClipBoard = lineEventClipBoard;

		GD.Print($"[{Name}] 正在粘贴事件: Line{lineEventClipBoard.SourceLineId} Layer{lineEventClipBoard.SourceLayer} Beat:{lineEventClipBoard.SourceStartBeat}");
	}

	public void ExitPasteMode()
	{
		if (!_isPasteMode) return;
		_isPasteMode = false;

		GD.Print($"[{Name}] 用户退出了事件粘贴模式");
	}

	private void OnEventTapped(LineEventEnum lineEventEnum, int index, Vector2 localPos)
	{
		EventLayer eventLayer = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer];
		LineEvent lineEvent = eventLayer.GetLineEvents(lineEventEnum)[index];

		Vector2 viewportPos = GetGlobalTransformWithCanvas() * localPos;
		Vector2 popupPos = viewportPos + new Vector2(30, 30);
		// GD.Print($"pos:{localPos}, viewportPos:{GetGlobalTransformWithCanvas() * localPos}, ab em pos:{GetScreenTransform() * localPos} screenPos:{screenPos}");
        
        if(SelectMode == SelectModeEnum.Single)
        {
			selectedEvents.Clear();
			selectedEvents.Add(lineEvent);

			_selectedEventsWithType.Clear();
			_selectedEventsWithType.Add((lineEventEnum, lineEvent));

            EventSelected?.Invoke(editingLineId, EditingLayer, lineEventEnum, index, popupPos);
        }
        else if(SelectMode == SelectModeEnum.Multi)
        {
            if (selectedEvents.Contains(lineEvent))
            {
                selectedEvents.Remove(lineEvent);

				_selectedEventsWithType.Remove((lineEventEnum, lineEvent));
            }
            else
            {
                selectedEvents.Add(lineEvent);

				_selectedEventsWithType.Add((lineEventEnum, lineEvent));
            }

			EventMultiSelected?.Invoke();
        }
        else
        {
            GD.PrintErr($"[{this.Name}] 未设置的选择模式:{SelectMode}");
        }
	}

    protected override void OnButtonDown(Vector2 pos)
    {
        if (_isPasteMode)
        {
			// Event 不允许 X 移动，只允许 Y（时间）移动
            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            _pasteTargetBeat = snappedBeat;
            _pasteBeatDelta = _pasteTargetBeat - _lineEventClipBoard.SourceStartBeat;
            return;
        }

		if (_isBoxSelectMode)
		{
			Vector2 dataPos = new Vector2(
				_coordComponent.GetChartPosX(pos.X),
				_coordComponent.GetBeatValue(pos.Y)
			);
			_boxSelectController.StartDrag(dataPos);
			return;
		}

        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            (LineEventEnum?, int) hit = FindNearestEvent(pos);
            if (hit.Item1 == null || hit.Item2 < 0) return;

            LineEventEnum evtType = hit.Item1.Value;
            int evtIndex = hit.Item2;
            EventLayer eventLayer = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer];
            LineEvent evt = eventLayer.GetLineEvents(evtType)[evtIndex];

			// 只有已选中的对象才能拖动
			// if(!selectedEvents.Contains(evt)) return;

            // 判断拖动部位（与 Hold 逻辑完全一致）
            float startBeatValue = evt.StartTime[0] + evt.StartTime[1] * 1f / evt.StartTime[2];
            float endBeatValue   = evt.EndTime[0]   + evt.EndTime[1]   * 1f / evt.EndTime[2];
            float chartX = EventTypeToChartX(evtType);

            Vector2 startPos = _coordComponent.GetPanelPosition(chartX, startBeatValue);
            Vector2 endPos   = _coordComponent.GetPanelPosition(chartX, endBeatValue);

            float distStart = pos.DistanceSquaredTo(startPos);
            float distEnd   = pos.DistanceSquaredTo(endPos);

            DragMoveComponent.DragMode mode;
            Beat initialBeat;

            float thresholdSq = distanceThreshold * distanceThreshold;

            if (distStart < thresholdSq && distStart < distEnd)
            {
                mode = DragMoveComponent.DragMode.Head;
                initialBeat = new Beat(evt.StartTime);
            }
            else if (distEnd < thresholdSq && distEnd < distStart)
            {
                mode = DragMoveComponent.DragMode.Tail;
                initialBeat = new Beat(evt.EndTime);
            }
            // else if (pos.Y <= startPos.Y && pos.Y >= endPos.Y)
            // {
            //     // 点击在长条中间：整体拖动
            //     mode = DragMoveComponent.DragMode.Body;
            //     initialBeat = new Beat(evt.StartTime); // 以 Start 为基准
            // }
            else
            {
				GD.Print($"未选中Event头尾, 不滑动Event:{evtIndex}");
                return; // 未命中有效区域
            }

            // Event 的标识用 ValueTuple 包装
            _dragMoveComponent.Start((evtType, evt), mode, chartX, initialBeat);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            
			float chartX = _coordComponent.GetChartPosX(pos.X);
			int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

			float beatValue = _coordComponent.GetBeatValue(pos.Y);
			Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

			_dragPlaceComponent.StartDrag(verLineIndex, snappedBeat);
			_dragPlaceComponent.Mode = DragPlaceComponent.PlaceMode.LongStraight;
            
        }
    }

    protected override void OnMotionInput(Vector2 position, Vector2 relative)
    {
        if (_isPasteMode)
        {
			// Event 不允许 X 移动，只允许 Y（时间）移动
            float beatValue = _coordComponent.GetBeatValue(position.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            if (snappedBeat != _pasteTargetBeat)
            {
                _pasteTargetBeat = snappedBeat;
                _pasteBeatDelta = _pasteTargetBeat - _lineEventClipBoard.SourceStartBeat;
            }

            return;
        }

		if (_isBoxSelectMode)
		{
			Vector2 dataPos = new Vector2(
				_coordComponent.GetChartPosX(position.X),
				_coordComponent.GetBeatValue(position.Y)
			);
			_boxSelectController.Move(dataPos);
			return;
		}

        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            if (!_inputController.IsDragging || !_dragMoveComponent.IsDragging) return;

            // Event 不允许 X 移动，只允许 Y（时间）移动
            _dragMoveComponent.Update(position, _coordComponent, allowX: false, allowY: true);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            
			float chartX = _coordComponent.GetChartPosX(position.X);
			int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

			float beatValue = _coordComponent.GetBeatValue(position.Y);
			Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

			_dragPlaceComponent.Move(verLineIndex, snappedBeat);
            
        }
    }

	protected override void OnButtonUp(Vector2 pos)
    {
		if (_isPasteMode)
		{
			return;
		}

		if (_isBoxSelectMode)
		{
			Vector2 dataPos = new Vector2(
				_coordComponent.GetChartPosX(pos.X),
				_coordComponent.GetBeatValue(pos.Y)
			);
			_boxSelectController.EndDrag(dataPos);
			return;
		}

        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
			if (_inputController.IsDragging)
            {
                _dragMoveComponent.End();
                return; // 拖动结束，不触发点击选择
            }

			// 点击选择逻辑
            ValueTuple<LineEventEnum?, int> tuple = FindNearestEvent(pos);
            if(tuple.Item1 == null || tuple.Item2 < 0) // 代表没有选中
            {
                DeselectAll();
				
            }
            else
            {
                OnEventTapped(tuple.Item1.Value, tuple.Item2, pos);
            }
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            float chartX = _coordComponent.GetChartPosX(pos.X);
			int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

			float beatValue = _coordComponent.GetBeatValue(pos.Y);
			Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

			_dragPlaceComponent.EndDrag(verLineIndex, snappedBeat);
        }
    }

    private void OnEventDragStarted(object targetId, DragMoveComponent.DragMode mode)
    {
        if (targetId is not ValueTuple<LineEventEnum, LineEvent> tuple) return;
        EventDragStarted?.Invoke(editingLineId, EditingLayer, tuple.Item1, tuple.Item2);
    }

    private void OnEventDragEnded(object targetId, DragMoveComponent.DragMode mode)
    {
        if (targetId is not ValueTuple<LineEventEnum, LineEvent> tuple) return;
        EventDragEnded?.Invoke(editingLineId, EditingLayer, tuple.Item1, tuple.Item2);
    }

	// -------- 拖动响应 --------
    private void OnEventDragMoved(object targetId, DragMoveComponent.DragMode mode,
                                  float newChartX, Beat newBeat)
    {
        // 取出对象引用
		(LineEventEnum evtType, LineEvent evt) = (ValueTuple<LineEventEnum, LineEvent>)targetId;
        if (evt == null)
		{
			GD.Print($"OnEventDragMoved() evt == null");
			return;
		}

        var eventLayer = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer];

        // 通过引用反向查找当前索引和类型（防止列表重排/增删导致索引失效）
        int evtIndex = -1;

        foreach (LineEventEnum type in EventTypeToIndexDic.Keys)
        {
            var list = eventLayer.GetLineEvents(type);
            if (list == null) continue;
            int idx = list.IndexOf(evt);
            if (idx >= 0)
            {
                evtIndex = idx;
                evtType = type;
                break;
            }
        }

        // 如果对象已被删除，优雅终止拖动，避免崩溃
        if (evtIndex < 0)
        {
            GD.Print($"[{Name}] 拖动的事件已被删除或不在当前层，终止拖动");
            _dragMoveComponent.End();
            return;
        }

        Beat newStart = new Beat(evt.StartTime);
        Beat newEnd   = new Beat(evt.EndTime);

        switch (mode)
        {
            case DragMoveComponent.DragMode.Head:
                newStart = newBeat;
                break;
            case DragMoveComponent.DragMode.Tail:
                newEnd = newBeat;
                break;
            case DragMoveComponent.DragMode.Body:
                Beat duration = newEnd - newStart;
                newStart = newBeat;
                newEnd = newBeat + duration;
                break;
        }

        EventTimeChangeRequested?.Invoke(editingLineId, EditingLayer, evtType, evtIndex, newStart, newEnd);
    }

	/// <summary>
	/// 获取点击位置最近的event
	/// </summary>
	/// <param name="pos">点击位置 坐标系：Control本地坐标</param>
	/// <returns>ValueTuple<LineEventEnum?, int>，参数:(事件类型，索引)，若Item1为null，表示没有找到</returns>
	private ValueTuple<LineEventEnum?, int> FindNearestEvent(Vector2 pos)
	{
		EventLayer eventLayer = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer];
		Dictionary<LineEventEnum, List<LineEvent> > allLineEvents = new(){
			{LineEventEnum.MoveX, eventLayer.MoveXEvents},
			{LineEventEnum.MoveY, eventLayer.MoveYEvents},
			{LineEventEnum.Rotate, eventLayer.RotateEvents},
			{LineEventEnum.Alpha, eventLayer.AlphaEvents},
			{LineEventEnum.Speed, eventLayer.SpeedEvents},
		};

		LineEventEnum? nearestEventType = null;
        int nearestEventIndex = -1;
        float nearestDistSquared = 99999f;
		
		foreach(LineEventEnum lineEventEnum in allLineEvents.Keys)
		{
			List<LineEvent> lineEvents = allLineEvents[lineEventEnum];

            for (int i = 0; i < lineEvents.Count; i++)
			{
                LineEvent lineEvent = lineEvents[i];

                float distSquared;

				float startBeat = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
                float endBeat = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
				float chartPosX = EventTypeToChartX(lineEventEnum);
                Vector2 startPos = _coordComponent.GetPanelPosition(chartPosX, startBeat);
                Vector2 endPos = _coordComponent.GetPanelPosition(chartPosX, endBeat);

                if(pos.Y < endPos.Y)
                {
                    //计算点击位置和结束点（最上方）的距离
                    distSquared = pos.DistanceSquaredTo(endPos);

                }
                else if(pos.Y > startPos.Y)
                {
                    //计算点击位置和开始点（最下方）的距离
                    distSquared = pos.DistanceSquaredTo(startPos);
                }
                else
                {
                    // 点击位置在hold两侧，计算水平距离
                    distSquared = (float)Math.Pow(pos.X - startPos.X, 2);
                }

				if(distSquared < nearestDistSquared)
				{
					nearestDistSquared = distSquared;
					nearestEventType = lineEventEnum;
					nearestEventIndex = i;
				}
			}
		}

        //判断距离是否小于阈值
        float distance = (float)Math.Sqrt(nearestDistSquared);
        if(distance > distanceThreshold)
        {
            // GD.Print($"[{this.Name}] 点击位置:{pos}, 未选中, 距离过大:{distance}");
            return new (null, -1);
        }

        // GD.Print($"[{this.Name}] 点击位置:{pos} 最近的event:{nearestEventType}-{nearestEventIndex}, 距离:{distance}");
        return new(nearestEventType, nearestEventIndex);
	}

    protected override void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos)
    {
		boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
		boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

		if (_isBoxSelectMode)
		{
			UpdateSelectionInRect(RectUtil.TwoPointsToRect(startDataPos, endDataPos));
		}
    }

    protected override void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos)
    {
		boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
		boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

		if (_isBoxSelectMode)
		{
			UpdateSelectionInRect(RectUtil.TwoPointsToRect(startDataPos, endDataPos));
		}
    }

	private void UpdateSelectionInRect(Rect2 rect)
	{
		HashSet<(LineEventEnum Type, LineEvent Evt)> previousSelection = new(_selectedEventsWithType);
		List<ValueTuple<float, (LineEventEnum Type, LineEvent Evt)>> lineEvents = GetEventsInRect(rect);

		if (SelectMode == SelectModeEnum.Single)
		{
			selectedEvents.Clear();
			_selectedEventsWithType.Clear();
		}

		foreach (ValueTuple<float, (LineEventEnum Type, LineEvent Evt)> lineEvent in lineEvents)
		{
			(LineEventEnum Type, LineEvent Evt) selectedEvent = lineEvent.Item2;
			selectedEvents.Add(selectedEvent.Evt);
			if (!_selectedEventsWithType.Contains(selectedEvent))
			{
				_selectedEventsWithType.Add(selectedEvent);
			}
		}
		
		// 如果选择的数量发生变化，意味着需要向上级通知选择情况
		if (!previousSelection.SetEquals(_selectedEventsWithType))
		{
			if (_selectedEventsWithType.Count == 0)
			{
				EmitAllDeselected();
			}
			else
			{
				EventMultiSelected?.Invoke();
			}
		}
	}

    protected override void OnDragEnded(int verLineIndex, Beat startBeat, Beat endBeat)
    {
        LineEventEnum lineEventEnum = IndexToEventTypeDic[verLineIndex];

		AddEventRequested?.Invoke(editingLineId, EditingLayer, lineEventEnum, startBeat, endBeat);

    }

	private List<ValueTuple<float, (LineEventEnum Type, LineEvent Evt)>> GetEventsInRect(Rect2 rect)
	{
		EventLayer eventLayer = editingChart.JudgeLineList[editingLineId].EventLayers[EditingLayer];

		List<ValueTuple<float, (LineEventEnum Type, LineEvent Evt)>> allEvents = new();

		foreach(LineEvent lineEvent in eventLayer.MoveXEvents){
			allEvents.Add((-675 + 1350f * 0f, (LineEventEnum.MoveX, lineEvent)));
		}
		foreach(LineEvent lineEvent in eventLayer.MoveYEvents){
			allEvents.Add((-675 + 1350f * 0.25f, (LineEventEnum.MoveY, lineEvent)));
		}
		foreach(LineEvent lineEvent in eventLayer.RotateEvents){
			allEvents.Add((-675 + 1350f * 0.5f, (LineEventEnum.Rotate, lineEvent)));
		}
		foreach(LineEvent lineEvent in eventLayer.AlphaEvents){
			allEvents.Add((-675 + 1350f * 0.75f, (LineEventEnum.Alpha, lineEvent)));
		}
		foreach(LineEvent lineEvent in eventLayer.SpeedEvents){
			allEvents.Add((-675 + 1350f * 1f, (LineEventEnum.Speed, lineEvent)));
		}

		return RectUtil.GetEventsInRect(
			allEvents,
			rect
		);
	}
}