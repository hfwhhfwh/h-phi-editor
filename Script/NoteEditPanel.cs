using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

public partial class NoteEditPanel : Panel
{

	[ExportGroup("网格布局设置")]
	[Export] private float horMargin = 50; // 横线的左右留空
	[Export] private int verLineCount;
	[Export] private float verMargin = 100; // 竖线的左右留空
	[Export] private int subBeatCount = 4; // 每个Beat被分割为多少个音符

	[ExportGroup("网格样式设置")]
	//线条参数
	[Export] private Color horColor = Colors.Red;
	[Export] private float horWidth = 1;
	[Export] private Color verColor = Colors.Green;
	[Export] private float verWidth = 1;
	[Export] private Color horSubColor = Colors.Yellow;
	[Export] private float horSubWidth = 1;

	[ExportGroup("纹理贴图")]
    [Export] public Texture2D tapTexture;
    [Export] public Texture2D dragTexture;
    [Export] public Texture2D flickTexture;
    [Export] public Texture2D holdHeadTexture;
    [Export] public Texture2D holdBodyTexture;
    [Export] public Texture2D holdEndTexture;
    [Export] public Texture2D lineTexture;

	//字体
	Font font = ThemeDB.FallbackFont;
	
	public float horOffsetSmoothed; // 用于使竖直滚动更平滑
	public float horSeparationSmoothed; // 用于使竖直缩放更平滑

	public Chart editingChart; //正在编辑的铺面，由上级设置
	private int editingLineId; // 正在编辑的线号

	// 节点池：存储预先创建好的 Node2D，每个内部含有一个 Sprite2D
	private List<Node2D> noteNodePool = new List<Node2D>();
	private int poolSize = 0;

    public override void _Ready()
    {
		InitializeNotePool(500); // 根据谱面复杂度调整容量 TODO
    }

	/// <summary>
	/// 预创建指定数量的 note 显示节点，并作为子节点隐藏。
	/// </summary>
	private void InitializeNotePool(int capacity)
	{
		for (int i = 0; i < capacity; i++)
		{
			var noteNode = new Node2D();
			noteNode.Name = $"NotePool_{i}";
			noteNode.Visible = false;
			noteNode.ZIndex = 10;

			var sprite = new Sprite2D();
			sprite.Name = "Sprite2D";
			sprite.Scale = new Vector2(0.1f, 0.1f);   // 或根据面板动态调整
			noteNode.AddChild(sprite);

			//针对Hold的特殊贴图
			var bodySprite = new Sprite2D();
			bodySprite.Name = "bodySprite";
			bodySprite.Scale = new Vector2(0.1f, 0.1f);   // 或根据面板动态调整
			noteNode.AddChild(bodySprite);

			var endSprite = new Sprite2D();
			endSprite.Name = "endSprite";
			endSprite.Scale = new Vector2(0.1f, 0.1f);   // 或根据面板动态调整
			noteNode.AddChild(endSprite);
			
			AddChild(noteNode);
			noteNodePool.Add(noteNode);
		}
		poolSize = capacity;
	}

	/// <summary>
	/// 每帧调用，根据当前谱面状态刷新所有 note 节点的视觉表现。
	/// </summary>
	private void UpdateNoteVisuals()
	{
		// 如果没有可用的谱面或判定线，则隐藏所有池节点
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Length)
		{
			HideAllNoteNodes();
			return;
		}

		Note[] notes = editingChart.JudgeLineList[editingLineId].Notes;
		if(notes == null)
		{
			HideAllNoteNodes();
			return;
		}
		
		// 动态扩展池的大小
		if (notes.Length > poolSize)
		{
			ExpandNotePool(notes.Length - poolSize);
		}

		// 为实际存在的 note 激活池节点
		for (int i = 0; i < notes.Length; i++)
		{
			Note note = notes[i];
			Node2D noteNode = noteNodePool[i];
			Sprite2D sprite = noteNode.GetNode<Sprite2D>("Sprite2D");
			if (sprite == null)
			{
				GD.PrintErr($"Node {noteNode.Name} 缺少 Sprite2D 子节点");
				continue;
			}

			// 选择对应的纹理
			sprite.Texture = note.Type switch
			{
				1 => tapTexture,
				2 => holdHeadTexture,
				3 => flickTexture,
				4 => dragTexture,
				_ => tapTexture
			};
			if(note.Type == 2)
			{
				sprite.Offset = new Vector2(0, holdHeadTexture.GetHeight() / 2f); // 顶部居中
			}

			float beatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
			// 计算位置
			{
				float ratio = (note.PositionX - (-675f)) / 1350f;
				float panelX = verMargin + ratio * (Size.X - 2 * verMargin);
				float panelY = Size.Y/2f + horOffsetSmoothed - beatValue * horSeparationSmoothed;

				noteNode.Position = new Vector2(panelX, panelY);
				noteNode.Visible = true;
			}

			noteNode.ZIndex = 10;
			

			//特殊处理Hold
			if(note.Type == 2)
			{
				noteNode.ZIndex = 9; // hold显示在其他note下面
				float endLocalY;
				//渲染尾部
				{
					float endBeatValue = note.EndTime[0] + note.EndTime[1] * 1f / note.EndTime[2];
					endLocalY = -(endBeatValue - beatValue) * horSeparationSmoothed;

					Sprite2D endSprite = noteNode.GetNode<Sprite2D>("endSprite");
					endSprite.Texture = holdEndTexture;
					endSprite.Scale = new Vector2(0.1f, 0.1f);
					endSprite.Position = new Vector2(0, endLocalY);
					endSprite.Offset = new Vector2(0, -holdHeadTexture.GetHeight() / 2f); // 底部居中
				}

				//渲染身体
				{
					float bodyLocalY = endLocalY / 2f;
					float sizeY = -endLocalY;
					Sprite2D bodySprite = noteNode.GetNode<Sprite2D>("bodySprite");
					bodySprite.Texture = holdBodyTexture;
					bodySprite.Scale = new Vector2(0.1f, sizeY/1900f);
					bodySprite.Position = new Vector2(0, bodyLocalY);
				}
			}
			

		}

		// 剩余的池节点隐藏
		for (int i = notes.Length; i < poolSize; i++)
		{
			noteNodePool[i].Visible = false;
		}
	}

	private void HideAllNoteNodes()
	{
		foreach (var node in noteNodePool)
			node.Visible = false;
	}

	private void ExpandNotePool(int additional)
	{
		for (int i = 0; i < additional; i++)
		{
			var noteNode = new Node2D();
			noteNode.Visible = false;
			noteNode.ZIndex = 10;

			var sprite = new Sprite2D();
			sprite.Scale = new Vector2(0.1f, 0.1f);
			sprite.Name = "Sprite2D";
			noteNode.AddChild(sprite);

			//针对Hold的特殊贴图
			var bodySprite = new Sprite2D();
			bodySprite.Name = "bodySprite";
			bodySprite.Scale = new Vector2(0.1f, 0.1f);   // 或根据面板动态调整
			noteNode.AddChild(bodySprite);

			var endSprite = new Sprite2D();
			endSprite.Name = "endSprite";
			endSprite.Scale = new Vector2(0.1f, 0.1f);   // 或根据面板动态调整
			noteNode.AddChild(endSprite);
			
			AddChild(noteNode);
			noteNodePool.Add(noteNode);
		}
		poolSize += additional;
	}

    public override void _Process(double delta)
    {
		// 刷新 note 显示
    	UpdateNoteVisuals();

		QueueRedraw();
    }

	// public void DrawNotes()
	// {
	// 	Note[] notes = editingChart.JudgeLineList[editingLineId].Notes;
	// 	BpmEvent[] bpmEvents = editingChart.BpmList;
	// 	foreach(Note note in notes)
	// 	{
	// 		// 获取note的谱面坐标和打击时间
	// 		int[] beat = note.StartTime;
	// 		float time = Util.BeatToSeconds(beat, bpmEvents);

	// 		// 谱面坐标系范围为[-675,675]x[-450,450]
	// 		// 计算panel上的X坐标
	// 		float ratio = (note.PositionX - (-675f)) / 1350f;
	// 		float panelX = verMargin + ratio * (Size.X - 2*verMargin);
			
	// 		// 计算panel上的Y坐标
	// 		float panelY = time * horSeparationSmoothed + horOffsetSmoothed;

	// 		Vector2 panelPosition = new Vector2(panelX, panelY);

	// 		// 渲染note
	// 		Texture2D noteTexture;
	// 		switch (note.Type)
	// 		{
	// 			case 1: //tap
	// 				noteTexture = tapTexture;
	// 				break;
	// 			case 2: //hold
	// 				noteTexture = holdHeadTexture;
	// 				break;
	// 			case 3: //flick
	// 				noteTexture = flickTexture;
	// 				break;
	// 			case 4: //drag
	// 				noteTexture = dragTexture;
	// 				break;
	// 			default:
	// 				noteTexture = tapTexture;
	// 				break;
	// 		}
	// 		//创建note节点
	// 		Node2D noteNode = new Node2D();
	// 		AddChild(noteNode);

	// 		//挂载Sprite2D
	// 		Sprite2D sprite = new Sprite2D();
	// 		sprite.Texture = noteTexture;
	// 		sprite.Name = "Sprite2D";
	// 		sprite.Scale = new Vector2(0.1f, 0.1f);
	// 		noteNode.AddChild(sprite);
	// 	}
	// }


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
