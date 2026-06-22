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
	private MultiMesh multiMesh;
	private MultiMeshInstance2D multiMeshInstance;

    public override void _Ready()
    {
		// 固定竖线数为5
        verLineCount = 5;
        //InitializeNodePool(50, CreateEventNode);

		//设置multiMeshInstance
		multiMeshInstance = new MultiMeshInstance2D();
		multiMeshInstance.Texture = eventHoldTexture;

		//设置Multimesh
		multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            InstanceCount = 10000,
            VisibleInstanceCount = 0
		};
        multiMeshInstance.Multimesh = multiMesh;
        
        // 创建 QuadMesh 并设置尺寸
        var quad = new QuadMesh();
		quad.Size = new Vector2(100, -100); // 根据场景调整
		multiMeshInstance.Multimesh.Mesh = quad;

		AddChild(multiMeshInstance);
    }

	private Node2D CreateEventNode()
    {
        var node = new Node2D();
        var sprite = new Sprite2D();
        sprite.Name = "bodySprite";
        sprite.Scale = new Vector2(widthScale, 1);
        node.AddChild(sprite);
        return node;
    }

	protected override void UpdateVisuals()
    {
        // 如果没有可用的谱面或判定线，则隐藏所有池节点
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Length)
		{
			HideAllNodes();
			return;
		}

		LineEvent[] moveXEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].MoveXEvents;
		LineEvent[] moveYEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].MoveYEvents;
		LineEvent[] rotateEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].RotateEvents;
		LineEvent[] alphaEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].AlphaEvents;
		LineEvent[] speedEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].SpeedEvents;

		int visibleCount = 0;

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
				float startBeatValue = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
				float endBeatValue = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
				//位置
				float panelX = verMargin + type.xRatio * (Size.X - 2 * verMargin);
				float startPanelY = Size.Y/2f + horOffsetSmoothed - startBeatValue * horSeparationSmoothed;
				float endPanelY = Size.Y/2f + horOffsetSmoothed - endBeatValue * horSeparationSmoothed;
				float panelY = (startPanelY + endPanelY) / 2f;
				//缩放
				float sizeY = startPanelY - endPanelY;
				float scaleY = sizeY / eventHoldTexture.GetSize().Y;

				//判断是否需要渲染
				if(panelX < 0f || panelX > Size.X || startPanelY < 0f || endPanelY > Size.Y)
				{
					continue;
				}

				//使用MultimeshInstance渲染
				Transform2D transform = Transform2D.Identity;
				transform.X = new Vector2(widthScale, 0);
				transform.Y = new Vector2(0, scaleY);
				transform.Origin = new Vector2(panelX, panelY);
				
				multiMesh.SetInstanceTransform2D(visibleCount, transform);
				visibleCount++;
				
			}
		}

		multiMesh.VisibleInstanceCount = visibleCount;

		// 剩余的池节点隐藏
		for (int i = visibleCount; i < poolSize; i++)
		{
			nodePool[i].Visible = false;
		}
    }

}