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
            // 计算面板 X 坐标（谱面坐标 -675~675 映射到面板水平范围）
            float ratio = (note.PositionX - (-675f)) / 1350f;
            float panelX = verMargin + ratio * (Size.X - 2 * verMargin);
            // 起始 Y 坐标（向上为负）
            float startY = Size.Y / 2f + horOffsetSmoothed - startBeat * horSeparationSmoothed;

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
                float endY = Size.Y / 2f + horOffsetSmoothed - endBeat * horSeparationSmoothed;

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
	
}
