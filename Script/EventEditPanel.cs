using Godot;
using System;
using QuickType;
using System.Collections.Generic;
using System.Linq;

public partial class EventEditPanel : Panel
{
	[ExportGroup("网格布局设置")]
	[Export] private float horMargin = 50; // 横线的左右留空
	private int verLineCount = 5; // 固定为5个轨道，不支持修改
	[Export] private float verMargin = 100; // 竖线的左右留空
	[Export] private int subBeatCount = 4; // 每个Beat被分割为多少个音符
	[Export] private float widthScale = 0.7f; // 宽度缩放

	[ExportGroup("网格样式设置")]
	[Export] private Color horColor = Colors.Red;
	[Export] private float horWidth = 1;
	[Export] private Color verColor = Colors.Green;
	[Export] private float verWidth = 1;
	[Export] private Color horSubColor = Colors.Yellow;
	[Export] private float horSubWidth = 1;

	[ExportGroup("纹理贴图")]
	[Export] private Texture2D eventHoldTexture;

	//字体
	Font font = ThemeDB.FallbackFont;

	public float horOffsetSmoothed; // 用于使竖直滚动更平滑
	public float horSeparationSmoothed; // 用于使竖直缩放更平滑

	public Chart editingChart; //正在编辑的铺面，由上级设置
	private int editingLineId; // 正在编辑的线号

	// 节点池：存储预先创建好的 Node2D，每个内部含有一个 Sprite2D
	private List<Node2D> eventNodePool = new List<Node2D>();
	private int poolSize = 0;

	/// <summary>
	/// 预创建指定数量的 event 显示节点，并作为子节点隐藏。
	/// </summary>
	private void InitializeEventPool(int capacity)
	{
		for (int i = 0; i < capacity; i++)
		{
			var eventNode = new Node2D();
			eventNode.Name = $"EventPool_{i}";
			eventNode.Visible = false;
			eventNode.ZIndex = 10;

			var bodySprite = new Sprite2D();
			bodySprite.Name = "bodySprite";
			bodySprite.Scale = new Vector2(widthScale, 1f);   // 或根据面板动态调整
			eventNode.AddChild(bodySprite);
			
			AddChild(eventNode);
			eventNodePool.Add(eventNode);
		}
		poolSize = capacity;
	}

	private void HideAllEventNodes()
	{
		foreach (var node in eventNodePool)
			node.Visible = false;
	}

	private void ExpandEventPool(int additional)
	{
		for (int i = 0; i < additional; i++)
		{
			var noteNode = new Node2D();
			noteNode.Visible = false;
			noteNode.ZIndex = 10;

			var bodySprite = new Sprite2D();
			bodySprite.Name = "bodySprite";
			bodySprite.Scale = new Vector2(widthScale, 1f);   // 或根据面板动态调整
			noteNode.AddChild(bodySprite);
			
			AddChild(noteNode);
			eventNodePool.Add(noteNode);
		}
		poolSize += additional;
	}

	/// <summary>
	/// 每帧调用，根据当前谱面状态刷新所有 event 节点的视觉表现。
	/// </summary>
	private void UpdateEventVisuals()
	{
		// 如果没有可用的谱面或判定线，则隐藏所有池节点
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Length)
		{
			HideAllEventNodes();
			return;
		}

		LineEvent[] moveXEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].MoveXEvents;
		LineEvent[] moveYEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].MoveYEvents;
		LineEvent[] rotateEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].RotateEvents;
		LineEvent[] alphaEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].AlphaEvents;
		SpeedEvent[] speedEvents = editingChart.JudgeLineList[editingLineId].EventLayers[0].SpeedEvents;
		
		// 计算事件的总数量
		int totalEventCount = 0;
		totalEventCount += moveXEvents.Length;
		totalEventCount += moveYEvents.Length;
		totalEventCount += rotateEvents.Length;
		totalEventCount += alphaEvents.Length;
		totalEventCount += speedEvents.Length;

		// 动态扩展池的大小
		if (totalEventCount > poolSize)
		{
			ExpandEventPool(totalEventCount - poolSize);
		}

		// 为实际存在的 event 激活池节点
		int poolStartIndex = 0;
		ShowSingleEvent(moveXEvents, 0f, poolStartIndex);
		poolStartIndex += moveXEvents.Length;

		ShowSingleEvent(moveYEvents, 0.25f, poolStartIndex);
		poolStartIndex += moveYEvents.Length;

		ShowSingleEvent(rotateEvents, 0.5f, poolStartIndex);
		poolStartIndex += rotateEvents.Length;

		ShowSingleEvent(alphaEvents, 0.75f, poolStartIndex);
		poolStartIndex += alphaEvents.Length;

		ShowSingleEvent(speedEvents, 1f, poolStartIndex);
		poolStartIndex += speedEvents.Length;


		// 剩余的池节点隐藏 TODO
		for (int i = totalEventCount; i < poolSize; i++)
		{
			eventNodePool[i].Visible = false;
		}
	}

	/// <summary>
	/// 渲染某一条线的某一个事件层的某一种事件
	/// </summary>
	/// <param name="lineEvents">事件列表</param>
	/// <param name="xRatio">这一列事件在面板上的位置比例，0为最左侧，1为左右侧</param>
	/// <param name="startIndex">使用对象池中索引的起点</param>
	private void ShowSingleEvent(LineEvent[] lineEvents, float xRatio, int startIndex)
	{
		for (int i = 0; i < lineEvents.Length; i++)
		{
			LineEvent lineEvent = lineEvents[i];
			Node2D eventNode = eventNodePool[startIndex + i];
			Sprite2D sprite = eventNode.GetNode<Sprite2D>("bodySprite");
			if (sprite == null)
			{
				GD.PrintErr($"[{this.Name}] Event {eventNode.Name} 缺少 Sprite2D 子节点");
				continue;
			}

			// 选择对应的纹理
			sprite.Texture = eventHoldTexture;

			float startBeatValue = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
			float endBeatValue = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
			// 计算位置和缩放
			{
				//位置
				float panelX = verMargin + xRatio * (Size.X - 2 * verMargin);

				float startPanelY = Size.Y/2f + horOffsetSmoothed - startBeatValue * horSeparationSmoothed;
				float endPanelY = Size.Y/2f + horOffsetSmoothed - endBeatValue * horSeparationSmoothed;
				float panelY = (startPanelY + endPanelY) / 2f;

				eventNode.Position = new Vector2(panelX, panelY);

				//缩放
				float sizeY = startPanelY - endPanelY;
				eventNode.Scale = new Vector2(widthScale, sizeY / eventHoldTexture.GetSize().Y);
			}
			

			eventNode.Visible = true;
			eventNode.ZIndex = 10;
			
		}
	}

	/// <summary>
	/// 渲染某一条线的某一个事件层的速度事件
	/// </summary>
	/// <param name="lineEvents">速度事件列表</param>
	/// <param name="xRatio">这一列事件在面板上的位置比例，0为最左侧，1为左右侧</param>
	/// <param name="startIndex">使用对象池中索引的起点</param>
	private void ShowSingleEvent(SpeedEvent[] lineEvents, float xRatio, int startIndex)
	{
		for (int i = 0; i < lineEvents.Length; i++)
		{
			SpeedEvent lineEvent = lineEvents[i];
			Node2D eventNode = eventNodePool[startIndex + i];
			Sprite2D sprite = eventNode.GetNode<Sprite2D>("bodySprite");
			if (sprite == null)
			{
				GD.PrintErr($"[{this.Name}] Event {eventNode.Name} 缺少 Sprite2D 子节点");
				continue;
			}

			// 选择对应的纹理
			sprite.Texture = eventHoldTexture;

			float startBeatValue = lineEvent.StartTime[0] + lineEvent.StartTime[1] * 1f / lineEvent.StartTime[2];
			float endBeatValue = lineEvent.EndTime[0] + lineEvent.EndTime[1] * 1f / lineEvent.EndTime[2];
			// 计算位置和缩放
			{
				//位置
				float panelX = verMargin + xRatio * (Size.X - 2 * verMargin);

				float startPanelY = Size.Y/2f + horOffsetSmoothed - startBeatValue * horSeparationSmoothed;
				float endPanelY = Size.Y/2f + horOffsetSmoothed - endBeatValue * horSeparationSmoothed;
				float panelY = (startPanelY + endPanelY) / 2f;

				eventNode.Position = new Vector2(panelX, panelY);

				//缩放
				float sizeY = startPanelY - endPanelY;
				eventNode.Scale = new Vector2(widthScale, sizeY / eventHoldTexture.GetSize().Y);
			}
			

			eventNode.Visible = true;
			eventNode.ZIndex = 10;
			
		}
	}

    public override void _Process(double delta)
    {
        // 刷新 event 显示
    	UpdateEventVisuals();

		QueueRedraw();
    }


    public override void _Draw()
    {
        //画横线
		//先画上半部分
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Ceil(horOffsetBeat);
			float y = Size.Y/2 - (Mathf.Ceil(horOffsetBeat) - horOffsetBeat) * horSeparationSmoothed;
			for(int i=0;i<=100 && y>=0;i++)
			{
				Vector2 from = new Vector2(horMargin,y);
				Vector2 to = new Vector2(Size.X - horMargin, y);
				DrawLine(from, to, horColor, horWidth, true);

				Vector2 charPos = new Vector2(horMargin / 2f, y);
				DrawString(font, charPos, $"{num}", HorizontalAlignment.Center, modulate:Colors.White, fontSize:20);

				y -= horSeparationSmoothed;   //逐步向上移动
				num++;
			}
		}

		//下半部分同理，注意不能绘制0以下
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Floor(horOffsetBeat);
			float y = Size.Y/2 + (horOffsetBeat - Mathf.Floor(horOffsetBeat)) * horSeparationSmoothed;
			for(int i=0;i<=100 && y<=Size.Y;i++)
			{
				Vector2 from = new Vector2(horMargin,y);
				Vector2 to = new Vector2(Size.X - horMargin, y);
				DrawLine(from, to, horColor, horWidth, true);

				Vector2 charPos = new Vector2(horMargin / 2f, y);
				DrawString(font, charPos, $"{num}", HorizontalAlignment.Center, modulate:Colors.White, fontSize:20);

				y += horSeparationSmoothed;   //逐步向上移动
				num--;
				if(num < 0) break;
			}
		}

		//画竖线
		{
			float verSeparation = (Size.X - 2*verMargin) / (verLineCount - 1);
			for(int i = 0; i < verLineCount; i++)
			{
				float x = verMargin + i*verSeparation;
				Vector2 from = new Vector2(x,0);
				Vector2 to = new Vector2(x,Size.Y);
				DrawLine(from, to, verColor, verWidth, true);
			}
		}

		//画小横线
		//先画上半部分
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Ceil(horOffsetBeat);
			float y = Size.Y/2 - (Mathf.Ceil(horOffsetBeat) - horOffsetBeat) * horSeparationSmoothed;
			for(int i=0;i<=100 && y>=0;i++)
			{
				//找到基准节拍线，向上画subBeatCount-1条横线
				for(int j = 1; j <= subBeatCount - 1; j++)
				{
					float subY = y - (horSeparationSmoothed / subBeatCount * j);
					//不让横线超出边界
					if(subY < 0) break;
					Vector2 from = new Vector2(horMargin,subY);
					Vector2 to = new Vector2(Size.X - horMargin, subY);
					DrawLine(from, to, horSubColor, horSubWidth, true);
				}
				y -= horSeparationSmoothed;   //逐步向上移动
				num++;
			}
		}
		//下半部分同理
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Floor(horOffsetBeat);
			float y = Size.Y/2 + (horOffsetBeat - Mathf.Floor(horOffsetBeat)) * horSeparationSmoothed;
			for(int i=0;i<=100 && y<=Size.Y + horSeparationSmoothed;i++) // Size.Y + horSeparationSmoothed防止最底部因为节拍线不显示导致小横线也不显示
			{
				//找到基准节拍线，向上画subBeatCount-1条横线
				for(int j = 1; j <= subBeatCount - 1; j++)
				{
					float subY = y - (horSeparationSmoothed / subBeatCount * j);
					//不让横线超出边界
					if(subY < 0) break;
					Vector2 from = new Vector2(horMargin,subY);
					Vector2 to = new Vector2(Size.X - horMargin, subY);
					DrawLine(from, to, horSubColor, horSubWidth, true);
				}
				y += horSeparationSmoothed;   //逐步向上移动
				num--;
				if(num < 0) break;
			}
		}
    }

}
