using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

public partial class NoteEditPanel : BaseEditPanel
{
	private enum SpriteType
	{
		Tap, Drag, Flick, HoldHead, HoldBody, HoldEnd
	}
	private readonly SpriteType[] allSpriteTypes = (SpriteType[])Enum.GetValues(typeof(SpriteType));

	[Export] public float noteScale = 0.1f;

	[ExportGroup("音符贴图")]
    [Export] public Texture2D tapTexture;
    [Export] public Texture2D dragTexture;
    [Export] public Texture2D flickTexture;
    [Export] public Texture2D holdHeadTexture;
    [Export] public Texture2D holdBodyTexture;
    [Export] public Texture2D holdEndTexture;

	// ---- Multimesh ---- 
	private Dictionary<SpriteType, MultiMesh> multiMeshes = new();
	private Dictionary<SpriteType, MultiMeshInstance2D> multiMeshInstances = new();
	private Dictionary<SpriteType, int> visibleCounts = new();

    public override void _Ready()
    {
		//InitializeNodePool(50, CreateNoteNode);

		//设置multiMeshInstance
		foreach(SpriteType type in allSpriteTypes)
		{
			Texture2D texture = type switch
			{
				SpriteType.Tap => tapTexture,
				SpriteType.Drag => dragTexture,
				SpriteType.Flick => flickTexture,
				SpriteType.HoldHead => holdHeadTexture,
				SpriteType.HoldBody => holdBodyTexture,
				SpriteType.HoldEnd => holdEndTexture,
				_ => tapTexture
			};

			MultiMesh multiMesh = new MultiMesh();
			multiMeshes[type] = multiMesh;

			MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
			multiMeshInstances[type] = multiMeshInstance;
			multiMeshInstance.Texture = texture;

			//设置Multimesh
			multiMesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
				InstanceCount = 10000,
				VisibleInstanceCount = 0
			};
			multiMeshInstance.Multimesh = multiMesh;
			
			// 根据纹理实际尺寸创建 QuadMesh
			var quad = new QuadMesh();
			quad.Size = new Vector2(texture.GetSize().X, -texture.GetSize().Y);   // 保持宽高比，去掉负值
			multiMeshInstance.Multimesh.Mesh = quad;

			AddChild(multiMeshInstance);
			multiMeshInstances[type] = multiMeshInstance;
            multiMeshes[type] = multiMesh;
		}
		
    }

	// private Node2D CreateNoteNode()
    // {
    //     var node = new Node2D();
    //     var sprite = new Sprite2D { Name = "Sprite2D", Scale = new Vector2(0.1f, 0.1f) };
    //     var body = new Sprite2D { Name = "bodySprite", Scale = new Vector2(0.1f, 0.1f) };
    //     var end = new Sprite2D { Name = "endSprite", Scale = new Vector2(0.1f, 0.1f) };
    //     node.AddChild(sprite);
    //     node.AddChild(body);
    //     node.AddChild(end);
    //     return node;
    // }

    /// <summary>
    /// 获取某个物体在面板上的坐标
    /// </summary>
    /// <param name="beatTime">时间（单位为拍数）</param>
    /// <param name="posX">X坐标，[-675, 675]</param>
    /// <returns>物体在面板上的坐标</returns>
    private Vector2 GetPanelPosition(float beatTime, float posX)
    {
        // 计算面板 X 坐标（谱面坐标 -675~675 映射到面板水平范围）
        float ratio = (posX - (-675f)) / 1350f;
        float panelX = verMargin + ratio * (Size.X - 2 * verMargin);
        // 起始 Y 坐标（向上为负）
        float panelY = GetPanelPosY(beatTime);

        return new Vector2(panelX, panelY);
    }

    /// <summary>
    /// 获取某个物体在面板上的Y坐标
    /// </summary>
    /// <param name="beatTime">时间（单位为拍数）</param>
    /// <returns>物体在面板上的Y坐标</returns>
    private float GetPanelPosY(float beatTime)
    {
        // 起始 Y 坐标（向上为负）
        float panelY = Size.Y / 2f + horOffsetSmoothed - beatTime * horSeparationSmoothed;

        return panelY;
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

		Note[] notes = editingChart.JudgeLineList[editingLineId].Notes;
		if(notes == null)
		{
			HideAllNodes();
			return;
		}
		
		//归零可见数量
		foreach(SpriteType spriteType in allSpriteTypes)
		{
			visibleCounts[spriteType] = 0;
		}

		// 为视口范围内的 note 激活池节点
		for (int i = 0; i < notes.Length; i++)
		{
			Note note = notes[i];

            // 计算起始拍数
            float startBeat = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
            Vector2 panelPos = GetPanelPosition(startBeat, note.PositionX);
            float panelX = panelPos.X;
            float startY = panelPos.Y;

            // 处理非 Hold 音符（Tap, Drag, Flick）
            if (note.Type != 2)
            {
                // 裁切：超出面板范围则不渲染
                if (panelX < 0 || panelX > Size.X || startY < 0 || startY > Size.Y)
                    continue;

                SpriteType type = note.Type switch
                {
                    1 => SpriteType.Tap,
                    3 => SpriteType.Flick,
                    4 => SpriteType.Drag,
                    _ => SpriteType.Tap
                };

                // 构建变换：位置 + 固定缩放
                Transform2D transform = Transform2D.Identity;
                transform.Origin = new Vector2(panelX, startY);
                transform.X = new Vector2(noteScale, 0);
                transform.Y = new Vector2(0, noteScale);

                multiMeshes[type].SetInstanceTransform2D(visibleCounts[type], transform);
                visibleCounts[type]++;
            }
            else // Hold 音符（Type == 2）
            {
                // 计算结束拍数和结束 Y 坐标
                float endBeat = note.EndTime[0] + note.EndTime[1] * 1f / note.EndTime[2];
                float endY = GetPanelPosY(endBeat);
                // float endY = Size.Y / 2f + horOffsetSmoothed - endBeat * horSeparationSmoothed;

                // 裁切：若头部和尾部都在面板外且不可见，则跳过（但若部分可见仍渲染）
                if (panelX < 0 || panelX > Size.X || startY < 0f || endY > Size.Y)
                    continue;

                // ---- 1. 渲染 Hold 头部 ----
                {
                    Transform2D transform = Transform2D.Identity;
                    transform.Origin = new Vector2(panelX, startY);
                    transform.X = new Vector2(noteScale, 0);
                    transform.Y = new Vector2(0, noteScale);
                    multiMeshes[SpriteType.HoldHead].SetInstanceTransform2D(
						visibleCounts[SpriteType.HoldHead],
						transform
					);
                    visibleCounts[SpriteType.HoldHead]++;
                }

                // ---- 2. 渲染 Hold 身体（拉伸条） ----
                {
                    float bodyLength = startY - endY;   // 正数表示向下延伸
                    
					float midY = (startY + endY) / 2f;
					// 计算 Y 方向缩放：长度 / 纹理高度（纹理高度可自定，这里假设为 1900，与原注释一致）
					float scaleY = bodyLength / holdBodyTexture.GetSize().Y;

					Transform2D transform = Transform2D.Identity;
					transform.Origin = new Vector2(panelX, midY);
					transform.X = new Vector2(noteScale, 0);
					transform.Y = new Vector2(0, scaleY);
					multiMeshes[SpriteType.HoldBody].SetInstanceTransform2D(
						visibleCounts[SpriteType.HoldBody], transform
					);
					visibleCounts[SpriteType.HoldBody]++;
                    
                }

                // ---- 3. 渲染 Hold 尾部 ----
                {
                    Transform2D transform = Transform2D.Identity;
                    transform.Origin = new Vector2(panelX, endY);
                    transform.X = new Vector2(noteScale, 0);
                    transform.Y = new Vector2(0, noteScale);
                    multiMeshes[SpriteType.HoldEnd].SetInstanceTransform2D(
						visibleCounts[SpriteType.HoldEnd], transform
					);
                    visibleCounts[SpriteType.HoldEnd]++;
                }
            }
        }

        // 更新所有 MultiMesh 的可见实例数量
        foreach (SpriteType type in allSpriteTypes)
        {
            multiMeshes[type].VisibleInstanceCount = visibleCounts[type];
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);

        if (@event is InputEventMouseButton mouseBtn)
        {
            HandleMouseBtnInput(@mouseBtn);
        }

        else if(@event is InputEventScreenTouch touchEvent)
        {
            HandleTouchInput(@touchEvent);
        }
        
        //HandleKeyInput(@event);
        
    }

    private void HandleMouseBtnInput(InputEventMouseButton mouseBtn)
    {
        // 鼠标左键点击
        if (mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (mouseBtn.Pressed)
            {
                Vector2 pos = mouseBtn.Position;
                Note note = FildNearestNote(pos);
            }
        }
        
    }

    private void HandleTouchInput(InputEventScreenTouch touchEvent)
    {
        //TODO
    }

    /// <summary>
    /// 找到距离点击位置最近的note，若未找到返回null
    /// </summary>
    /// <param name="pos">点击位置</param>
    /// <returns>距离点击位置最近的note</returns>
    private Note FildNearestNote(Vector2 pos)
    {
        Note[] notes = editingChart.JudgeLineList[editingLineId].Notes;

        Note nearestNote = null;
        float nearestDistSquared = 99999f;

        foreach(Note note in notes)
        {
            float distSquared;
            if(note.Type != 2)
            {
                //计算note位置
                float beatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
                Vector2 notePos = GetPanelPosition(beatValue, note.PositionX);

                distSquared = pos.DistanceSquaredTo(notePos);
            }
            else // 特殊处理hold
            {
                float startBeat = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
                float endBeat = note.EndTime[0] + note.EndTime[1] * 1f / note.EndTime[2];
                Vector2 startPos = GetPanelPosition(startBeat, note.PositionX);
                Vector2 endPos = GetPanelPosition(endBeat, note.PositionX);

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
            }

            if(distSquared < nearestDistSquared)
            {
                nearestDistSquared = distSquared;
                nearestNote = note;
            }
        }

        GD.Print($"[{this.Name}] 点击位置:{pos} 最近的note:{Array.IndexOf(notes,nearestNote)}, 距离:{Math.Sqrt(nearestDistSquared)}");
        return nearestNote;

        
    }

	
}
