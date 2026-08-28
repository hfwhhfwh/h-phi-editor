using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class BpmEditPanel : BaseEditPanel
{
	[Export] private float bpmEventScale = 0.1f;
    [Export] private float bpmTextOffset = 60f;
    [Export] private int bpmTextFontSize = 24;
    
    /// <summary> 被选中时的颜色滤镜 </summary>
    [Export] private Color selectedModulate = new Color(1f, 0.223f, 0.947f, 1f);
    [Export] private Color deleteHighlightModulate = new Color(1f, 0.184f, 0, 1f);
    [Export] private Color toAddModulate = new Color(1f, 1f, 1f, 0.588f);

    /// <summary> 放置新BPM事件时的默认值 </summary>
    [Export] public float PlacingBpm { get; set; } = 120f;

	[Export] private Texture2D _texture;
	public const string MultiMeshKey = "BpmEvent";

	private HashSet<BpmEvent> selectedEvents = new();
    private HashSet<BpmEvent> eventsToDelete = new();
    private Control _textOverlay; // 显示数值文字

	/// <summary>
	/// int index, Vector2 clickViewportPos
	/// </summary>
	public event Action<int, Vector2> EventSelected;
    
    /// <summary>
    /// 请求添加一个BPM事件，参数为(BPM值，起始Beat)
    /// </summary>
    public event Action<float, Beat> EventAddRequested;
    
    /// <summary>
    /// 请求删除BPM事件，参数为(要删除的BPM事件列表)
    /// </summary>
    public event Action<List<BpmEvent>> EventDeleteRequested;

    /// <summary>
    /// 当BPM事件的时间被修改时触发，参数:(事件索引，新StartTime)
    /// </summary>
    public event Action<int, Beat> EventTimeChanged;

	public override void _Ready()
    {
        base._Ready();

		VerLineCount = 2;

		//设置multiMesh
		RegisterMultiMesh(MultiMeshKey, _texture, 128, 1);

        // 设置数值文字
        _textOverlay = new Control
        {
            Name = "TextOverlay",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 2,
        };
        _textOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _textOverlay.Draw += OnDrawTextOverlay;
        AddChild(_textOverlay);
		
        // ---- 订阅拖动事件 ----
        _dragMoveComponent.Moved += OnEventDragMoved;
    }

	public override void _ExitTree()
    {
        _dragMoveComponent.Moved -= OnEventDragMoved;
        
        if (_textOverlay != null)
        {
            _textOverlay.Draw -= OnDrawTextOverlay;
        }

        base._ExitTree();
    }

    private void OnDrawTextOverlay()
    {
        if (editingChart?.BpmList == null)
        {
            return;
        }

        GetVisibleBeatRange(out float minBeat, out float maxBeat);
        float textX = VerMargin + bpmTextOffset;

        foreach (BpmEvent bpmEvent in editingChart.BpmList)
        {
            float beatValue = bpmEvent.StartTime[0]
                + bpmEvent.StartTime[1] * 1f / bpmEvent.StartTime[2];
            if (beatValue < minBeat || beatValue > maxBeat)
            {
                continue;
            }

            float panelPosY = _coordComponent.GetPanelPosY(beatValue);
            string text = $"{bpmEvent.Bpm:F1}";
            Vector2 textSize = font.GetStringSize(text, fontSize: bpmTextFontSize);
            float drawY = panelPosY + (font.GetAscent(bpmTextFontSize) - font.GetDescent(bpmTextFontSize)) / 2;

            _textOverlay.DrawString(
                font,
                new Vector2(textX, drawY),
                text,
                fontSize: bpmTextFontSize,
                alignment: HorizontalAlignment.Left,
                width: textSize.X);
        }
    }


    protected override void RenderContent()
    {
		_textOverlay?.QueueRedraw();

        // 如果没有可用的谱面，则隐藏所有池节点
		if (editingChart == null || editingChart.BpmList == null)
		{
			return;
		}

        List<BpmEvent> bpmEvents = editingChart.BpmList;

		// ============= 渲染视口范围内的 bpmEvent ============= 
        GetVisibleBeatRange(out float minBeat, out float maxBeat);

        // 额外绘制即将创建的BPM事件
        if(_dragPlaceComponent.IsDragging){
            RenderObject(
                key: MultiMeshKey,
                localX: VerMargin, // 第一列
                beat: _dragPlaceComponent.EndBeat,
                offset: Vector2.Zero,
                scale: bpmEventScale,
                renderEffect: ToAddRender
            );
        }

        //绘制谱面中的bpmEvent 
        if(bpmEvents != null)
        {
            for (int i = 0; i < bpmEvents.Count; i++)
            {
                BpmEvent bpmEvent = bpmEvents[i];

                // ---- 快速视口裁剪：利用预计算的 beat 值 ----
                float beatValue = bpmEvent.StartTime[0] + bpmEvent.StartTime[1] * 1f / bpmEvent.StartTime[2];
                if (beatValue > maxBeat || beatValue < minBeat) continue;

                Action<MultiMesh, int> renderEffect = null;
                //选中效果
                if (selectedEvents.Contains(bpmEvent))
                {
                    renderEffect = SelectedRender;
                }
                //即将删除的高亮效果
                if (eventsToDelete.Contains(bpmEvent))
                {
                    renderEffect = AboutToDeleteRender;
                }

                RenderObject(
					key: MultiMeshKey,
					localX: VerMargin, // 第一列
					beat: new Beat(bpmEvent.StartTime),
					offset: Vector2.Zero,
					scale: bpmEventScale,
					renderEffect: renderEffect
				);
            }
        }
    }

	private void ToAddRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, toAddModulate);
    }

	private void SelectedRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, selectedModulate);
    }

	private void AboutToDeleteRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, deleteHighlightModulate);
    }

	/// <summary>
    /// 找到距离点击位置最近的bpmEvent，若未找到返回-1
    /// </summary>
    /// <param name="pos">点击位置，坐标系：Control本地坐标</param>
    /// <returns>距离点击位置最近的bpmEvent的索引</returns>
	private int FindNearestEventIndex(Vector2 pos)
	{
		List<BpmEvent> bpmEvents = editingChart.BpmList;

        int nearestEventIndex = -1;
        float nearestDistSquared = 99999f;

        if(bpmEvents == null || bpmEvents.Count == 0) return -1;

        for (int i = 0; i < bpmEvents.Count; i++)
        {
            BpmEvent bpmEvent = bpmEvents[i];
            
			//计算event位置
			float beatValue = bpmEvent.StartTime[0] + bpmEvent.StartTime[1] * 1f / bpmEvent.StartTime[2];
			Vector2 eventPos = new Vector2(VerMargin, _coordComponent.GetPanelPosY(beatValue));

			float distSquared = pos.DistanceSquaredTo(eventPos);

            if(distSquared < nearestDistSquared)
            {
                nearestDistSquared = distSquared;
                nearestEventIndex = i;
            }
        }

        //判断距离是否小于阈值
        float distance = (float)Math.Sqrt(nearestDistSquared);
        if(distance > distanceThreshold)
        {
            return -1;
        }

        return nearestEventIndex;
	}

    protected override void OnButtonDown(Vector2 pos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            int eventIndex = FindNearestEventIndex(pos);
            if (eventIndex == -1)
            {
                // 没有命中，由 OnButtonUp 负责 DeselectAll
                return;
            }

			List<BpmEvent> bpmEvents = editingChart.BpmList;
			BpmEvent bpmEvent = bpmEvents[eventIndex];

            // BPM事件是点事件，只有Body拖动模式
            DragMoveComponent.DragMode mode = DragMoveComponent.DragMode.Body;
            float initialChartX = 0;
            Beat initialBeat = new Beat(bpmEvent.StartTime);

            _dragMoveComponent.Start(bpmEvent, mode, initialChartX, initialBeat);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            // BPM只有一列，verLineIndex固定为0；BPM是点事件
            _dragPlaceComponent.StartDrag(0, snappedBeat);
            _dragPlaceComponent.Mode = DragPlaceComponent.PlaceMode.Point;
        }
        else if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            Vector2 dataPos = new Vector2(
                _coordComponent.GetChartPosX(pos.X),
                _coordComponent.GetBeatValue(pos.Y)
            );
            _boxSelectController.StartDrag(dataPos);
        }
    }

	protected override void OnMotionInput(Vector2 position, Vector2 relative)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            if (!_inputController.IsDragging || !_dragMoveComponent.IsDragging) return;

            // BPM事件不允许X移动，只允许Y（时间）移动
            _dragMoveComponent.Update(position, _coordComponent, allowX: false, allowY: true);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            float beatValue = _coordComponent.GetBeatValue(position.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            _dragPlaceComponent.Move(0, snappedBeat);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            Vector2 dataPos = new Vector2(
                _coordComponent.GetChartPosX(position.X),
                _coordComponent.GetBeatValue(position.Y)
            );
            _boxSelectController.Move(dataPos);
        }
    }

    protected override void OnButtonUp(Vector2 pos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            if (_inputController.IsDragging)
            {
                _dragMoveComponent.End();
                return; // 拖动结束，不触发点击选择
            }

            // 点击选择逻辑
            int eventIndex = FindNearestEventIndex(pos);
            if (eventIndex == -1) 
            {
                DeselectAll();
            }
            else 
            {
                OnEventTapped(eventIndex, pos);
            }
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            _dragPlaceComponent.EndDrag(0, snappedBeat);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            Vector2 dataPos = new Vector2(
                _coordComponent.GetChartPosX(pos.X),
                _coordComponent.GetBeatValue(pos.Y)
            );
            _boxSelectController.EndDrag(dataPos);
        }
    }

    private void OnEventTapped(int eventIndex, Vector2 localPos)
    {
        List<BpmEvent> bpmEvents = editingChart.BpmList;
        BpmEvent bpmEvent = bpmEvents[eventIndex];
        
        if(selectMode == SelectMode.Single)
        {
            selectedEvents = [bpmEvent];
            //坐标转换 本地坐标 -> viewport坐标
            Vector2 viewportPos = GetGlobalTransformWithCanvas() * localPos;
		    Vector2 popupPos = viewportPos + new Vector2(30, 30);
            EventSelected?.Invoke(eventIndex, popupPos);
        }
        else if(selectMode == SelectMode.Multi)
        {
            if (selectedEvents.Contains(bpmEvent))
            {
                selectedEvents.Remove(bpmEvent);
            }
            else
            {
                selectedEvents.Add(bpmEvent);
            }
        }
        else
        {
            GD.PrintErr($"[{this.Name}] 未设置的选择模式:{selectMode}");
        }
    }

    // -------- 拖动响应 --------
    private void OnEventDragMoved(object targetId, DragMoveComponent.DragMode mode,
                                 float newChartX, Beat newBeat)
    {
        BpmEvent bpmEvent = (BpmEvent)targetId;
        List<BpmEvent> bpmEvents = editingChart.BpmList;
        int eventIndex = bpmEvents.IndexOf(bpmEvent);

        // 如果对象已被删除，优雅终止拖动，避免崩溃
        if (eventIndex < 0)
        {
            GD.Print($"[{Name}] 拖动的事件已被删除，终止拖动");
            _dragMoveComponent.End();
            return;
        }

        // BPM事件只有时间变化
        EventTimeChanged?.Invoke(eventIndex, newBeat);
    }

    public void DeselectAll()
    {
        selectedEvents.Clear();
    }

    protected override void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos)
    {
        boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
        boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

        if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            //检测范围内的bpmEvent
            Rect2 rect = RectUtil.TwoPointsToRect(startDataPos, endDataPos); // 坐标系：(ChartPosX, BeatValue)

            List<int> eventsIndex = GetEventsInRect(rect);

            eventsToDelete.Clear();
            List<BpmEvent> bpmEvents = editingChart.BpmList;
            foreach(int i in eventsIndex)
            {
                eventsToDelete.Add(bpmEvents[i]);
            }
        }
    }

    protected override void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos)
    {
        boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
        boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

        if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            //检测范围内的bpmEvent
            Rect2 rect = RectUtil.TwoPointsToRect(startDataPos, endDataPos); // 坐标系：(ChartPosX, BeatValue)
            List<int> eventsToDeleteIndex = GetEventsInRect(rect);

            //转换为List<BpmEvent>数据格式
            List<BpmEvent> bpmEventsToDelete = new();
            List<BpmEvent> bpmEvents = editingChart.BpmList;
            foreach(int index in eventsToDeleteIndex) bpmEventsToDelete.Add(bpmEvents[index]);

            //触发事件，请求删除bpmEvent
            EventDeleteRequested?.Invoke(bpmEventsToDelete);

            //清除高亮显示
            eventsToDelete.Clear();
        }
    }

    /// <summary>
    /// 获得矩形内的所有bpmEvent（坐标系：(ChartPosX, BeatValue)）
    /// </summary>
    /// <param name="rect">矩形（坐标系：(ChartPosX, BeatValue)）</param>
    /// <returns></returns>
    private List<int> GetEventsInRect(Rect2 rect)
    {
        List<BpmEvent> bpmEvents = editingChart.BpmList;
        List<int> result = new();

        if(bpmEvents == null) return result;

        for(int i = 0; i < bpmEvents.Count; i++)
        {
            BpmEvent bpmEvent = bpmEvents[i];
            float beatValue = bpmEvent.StartTime[0] + bpmEvent.StartTime[1] * 1f / bpmEvent.StartTime[2];
            
            // BPM只有一列，只判断Y轴（beat值）是否在矩形范围内
            if(rect.HasPoint(new Vector2(0, beatValue)))
            {
                result.Add(i);
            }
        }

        return result;
    }

    protected override void OnDragEnded(int verLineIndex, Beat startBeat, Beat endBeat)
    {
        // BPM事件是点事件，使用endBeat作为startTime
        EventAddRequested?.Invoke(PlacingBpm, endBeat);
    }
}