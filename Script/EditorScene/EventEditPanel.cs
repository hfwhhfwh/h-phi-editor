using Godot;
using System;
using QuickType;
using System.Collections.Generic;
using System.Linq;

public partial class EventEditPanel : BaseEditPanel
{

	[ExportGroup("事件特有设置")]
    [Export] private float widthScale = 0.4f;
    [Export] private Texture2D eventHoldTexture;

	// ---- Multimesh ----
	// private MultiMesh multiMesh;
	// private MultiMeshInstance2D multiMeshInstance;

	private List<LineEvent> selectedEvents = new();
    private List<LineEvent> eventsToDelete = new();

	/// <summary>
	/// 请求删除若干个事件，参数:(判定线编号，LineEvent列表)
	/// </summary>
	public event Action<int, List<LineEvent>> NoteDeleteRequested;

    public override void _Ready()
    {
		base._Ready();

		// 固定竖线数为5
        verLineCount = 5;
        //InitializeNodePool(50, CreateEventNode);

		//设置multiMeshInstance
		// multiMeshInstance = new MultiMeshInstance2D();
		// multiMeshInstance.Texture = eventHoldTexture;

		// //设置Multimesh
		// multiMesh = new MultiMesh
		// {
		// 	TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
        //     InstanceCount = 10000,
        //     VisibleInstanceCount = 0
		// };
        // multiMeshInstance.Multimesh = multiMesh;
        
        // // 创建 QuadMesh 并设置尺寸
        // var quad = new QuadMesh();
		// quad.Size = new Vector2(100, -100); // 根据场景调整
		// multiMeshInstance.Multimesh.Mesh = quad;

		// AddChild(multiMeshInstance);

		RegisterMultiMesh("Event", eventHoldTexture);
    }

	// private Node2D CreateEventNode()
    // {
    //     var node = new Node2D();
    //     var sprite = new Sprite2D();
    //     sprite.Name = "bodySprite";
    //     sprite.Scale = new Vector2(widthScale, 1);
    //     node.AddChild(sprite);
    //     return node;
    // }


    protected override void RenderContent()
    {
        // 如果没有可用的谱面或判定线，则隐藏所有池节点
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Count)
		{
			//HideAllNodes();
			return;
		}

		LineEvent[] moveXEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].MoveXEvents;
		LineEvent[] moveYEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].MoveYEvents;
		LineEvent[] rotateEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].RotateEvents;
		LineEvent[] alphaEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].AlphaEvents;
		LineEvent[] speedEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].SpeedEvents;

		// int visibleCount = 0;

		var allEvents = new (LineEvent[] events, float xRatio)[]
        {
            (moveXEvents, 0.0f),
            (moveYEvents, 0.25f),
            (rotateEvents, 0.5f),
            (alphaEvents, 0.75f),
            (speedEvents, 1.0f)
        };

		// 为实际存在的 event 激活池节点
		foreach((LineEvent[] events, float xRatio) type in allEvents)
		{
			for (int i = 0; i < type.events.Length; i++)
			{
				LineEvent lineEvent = type.events[i];

				//1. 计算位置和缩放
				// float startBeatValue = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
				// float endBeatValue = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
				// //位置
				// float localX = verMargin + type.xRatio * (Size.X - 2 * verMargin);
				// float startPanelY = Size.Y/2f + horOffsetSmoothed - startBeatValue * horSeparationSmoothed;
				// float endPanelY = Size.Y/2f + horOffsetSmoothed - endBeatValue * horSeparationSmoothed;
				// float panelY = (startPanelY + endPanelY) / 2f;
				// //缩放
				// float sizeY = startPanelY - endPanelY;
				// float scaleY = sizeY / eventHoldTexture.GetSize().Y;

				// //判断是否需要渲染
				// if(panelX < 0f || panelX > Size.X || startPanelY < 0f || endPanelY > Size.Y)
				// {
				// 	continue;
				// }

				float localX = verMargin + type.xRatio * (Size.X - 2 * verMargin);
				Beat startBeat = new Beat(lineEvent.StartTime);
				Beat endBeat = new Beat(lineEvent.EndTime);

				//使用MultimeshInstance渲染
				RenderLongObject(
					key: "Event",
					localX: localX,
					startBeat: startBeat,
					endBeat: endBeat,
					offset: Vector2.Zero,
					scale: widthScale,
					renderEffect: null
				);
				// Transform2D transform = Transform2D.Identity;
				// transform.X = new Vector2(widthScale, 0);
				// transform.Y = new Vector2(0, scaleY);
				// transform.Origin = new Vector2(panelX, panelY);
				
				// multiMesh.SetInstanceTransform2D(visibleCount, transform);
				// visibleCount++;
				
			}
		}

		// multiMesh.VisibleInstanceCount = visibleCount;

		// // 剩余的池节点隐藏
		// for (int i = visibleCount; i < poolSize; i++)
		// {
		// 	nodePool[i].Visible = false;
		// }
    }


    protected override void OnButtonDown(Vector2 pos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            
			float chartX = _coordComponent.GetChartPosX(pos.X);
			int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

			float beatValue = _coordComponent.GetBeatValue(pos.Y);
			Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

			_dragPlaceComponent.StartDrag(verLineIndex, snappedBeat);
            
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

    protected override void OnButtonUp(Vector2 pos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
			// TODO 实现EventEditPanel单点编辑
            // int noteIndex = FildNearestNoteIndex(pos);
            // if(noteIndex == -1) // -1代表没有选中
            // {
            //     DeselectAll();
            // }
            // else
            // {
            //     OnNoteTaped(noteIndex);
            // }
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            
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

    protected override void OnMotionInput(Vector2 position, Vector2 relative)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            
			float chartX = _coordComponent.GetChartPosX(position.X);
			int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

			float beatValue = _coordComponent.GetBeatValue(position.Y);
			Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

			_dragPlaceComponent.Move(verLineIndex, snappedBeat);
            
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

    protected override void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
			boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
        	boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

            //检测范围内的event
            Rect2 rect = RectUtil.TwoPointsToRect(startDataPos, endDataPos); // 坐标系：(ChartPosX, BeatValue)

            List<ValueTuple<float, LineEvent>> lineEvents = GetEventsInRect(rect);

            eventsToDelete.Clear();
            
            foreach(ValueTuple<float, LineEvent> lineEvent in lineEvents)
            {
                eventsToDelete.Add(lineEvent.Item2);
            }

        }
    }

    protected override void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
			boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
        	boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

            //检测范围内的event
            Rect2 rect = RectUtil.TwoPointsToRect(startDataPos, endDataPos); // 坐标系：(ChartPosX, BeatValue)

            List<ValueTuple<float, LineEvent>> lineEvents = GetEventsInRect(rect);

			List<LineEvent> deletingEvents = new();
			foreach(ValueTuple<float, LineEvent> lineEvent in lineEvents)
            {
                deletingEvents.Add(lineEvent.Item2);
            }

			//触发事件，请求删除note
            NoteDeleteRequested?.Invoke(EditingLineId, deletingEvents);

			//清除高亮显示
            eventsToDelete.Clear();

        }
    }

    protected override void OnDragEnded(int verLineIndex, Beat startBeat, Beat endBeat)
    {
        // throw new NotImplementedException();
    }

	private List<ValueTuple<float, LineEvent>> GetEventsInRect(Rect2 rect)
	{
		EventLayer eventLayer = editingChart.JudgeLineList[editingLineId].EventLayers[0]; // TODO 选择不同的事件层

		List<ValueTuple<float, LineEvent>> allEvents = new();
		foreach(LineEvent lineEvent in eventLayer.MoveXEvents){
			allEvents.Add((-675 + 1350f * 0f, lineEvent));
		}
		foreach(LineEvent lineEvent in eventLayer.MoveYEvents){
			allEvents.Add((-675 + 1350f * 0.25f, lineEvent));
		}
		foreach(LineEvent lineEvent in eventLayer.RotateEvents){
			allEvents.Add((-675 + 1350f * 0.5f, lineEvent));
		}
		foreach(LineEvent lineEvent in eventLayer.AlphaEvents){
			allEvents.Add((-675 + 1350f * 0.75f, lineEvent));
		}
		foreach(LineEvent lineEvent in eventLayer.SpeedEvents){
			allEvents.Add((-675 + 1350f * 1f, lineEvent));
		}

		return RectUtil.GetEventsInRect(
			allEvents,
			rect
		);
	}
}