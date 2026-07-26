using Godot;
using System;
using System.Collections.Generic;

public partial class ChartRenderer : BaseChartRenderer
{
    private enum SpriteType
	{
		Tap, Drag, Flick, HoldHead, HoldBody, HoldEnd
	}
	private readonly SpriteType[] allSpriteTypes = (SpriteType[])Enum.GetValues(typeof(SpriteType));

    #region 纹理贴图
    [ExportGroup("纹理贴图")]
    [Export] public Texture2D tapTexture;
    [Export] public Texture2D dragTexture;
    [Export] public Texture2D flickTexture;
    [Export] public Texture2D holdHeadTexture;
    [Export] public Texture2D holdBodyTexture;
    [Export] public Texture2D holdEndTexture;
    [Export] public Texture2D lineTexture;

    #endregion


    // ---- Multimesh ---- 
	private Dictionary<SpriteType, MultiMesh> multiMeshes = new();
	private Dictionary<SpriteType, MultiMeshInstance2D> multiMeshInstances = new();
	private Dictionary<SpriteType, int> visibleCounts = new();

    private MultiMesh lineMultiMesh;
    private MultiMeshInstance2D lineMultiMeshInstance;
    private int lineVisibleCount = 0;

    /// <summary>note的宽度大小缩放</summary>
    public float noteScale;

    public override void Initialize(Control parent)
    {
        Parent = parent;
        //设置note的宽度缩放
        Parent.Resized += () =>
        {
            noteScale = Parent.Size.X * 0.16f / tapTexture.GetWidth();
            GD.Print($"[{this.Name}] parent.Size.X:{Parent.Size.X}, tapTexture.GetWidth():{tapTexture.GetWidth()}, noteScale:{noteScale}");
        };

        // 初始化MultiMesh
        InitMultiMesh();
    }


    /// <summary>
    /// 初始化MultiMesh
    /// </summary>
    private void InitMultiMesh()
    {
        //设置note的multiMeshInstance
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

            //设置Multimesh
			MultiMesh multiMesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
				InstanceCount = 0,
				VisibleInstanceCount = 0,
                UseColors = true, // 用于提示选中
			};
            multiMesh.InstanceCount = 100000;
			multiMeshes[type] = multiMesh;

			MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
            multiMeshInstance.Texture = texture;
            multiMeshInstance.Multimesh = multiMesh;
            multiMeshInstance.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
			multiMeshInstances[type] = multiMeshInstance;

			// 根据纹理实际尺寸创建 QuadMesh
			var quad = new QuadMesh();
			quad.Size = new Vector2(texture.GetSize().X, -texture.GetSize().Y);   // 保持宽高比，去掉负值
			multiMeshInstance.Multimesh.Mesh = quad;

			Parent.AddChild(multiMeshInstance);
			multiMeshInstances[type] = multiMeshInstance;
            multiMeshes[type] = multiMesh;
		}

        //设置line的multimesh
        {
            Texture2D texture = lineTexture;

            //设置Multimesh
			lineMultiMesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
				InstanceCount = 0,
				VisibleInstanceCount = 0,
                UseColors = true, // 用于提示选中
			};
            lineMultiMesh.InstanceCount = 10000;

            //设置MultimeshInstance
            MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
            multiMeshInstance.Texture = texture;
            multiMeshInstance.Multimesh = lineMultiMesh;
            multiMeshInstance.TextureFilter = CanvasItem.TextureFilterEnum.Linear;

			// 根据纹理实际尺寸创建 QuadMesh
			var quad = new QuadMesh();
			quad.Size = new Vector2(texture.GetSize().X, -texture.GetSize().Y);   // 保持宽高比，去掉负值
			multiMeshInstance.Multimesh.Mesh = quad;

			Parent.AddChild(multiMeshInstance);

        }
    }

    public override void Render(List<JudgeLineRenderData> lineRenderDatas, List<NoteRenderData> noteRenderDatas)
    {
        if(Disabled) return;

		
		//归零可见数量
        lineVisibleCount = 0;
		foreach(SpriteType spriteType in allSpriteTypes)
		{
			visibleCounts[spriteType] = 0;
		}
        
        // -------- 渲染判定线 --------
        foreach(JudgeLineRenderData lineRenderData in lineRenderDatas)
        {
            Vector2 position = lineRenderData.Pos;
            float rotate = lineRenderData.Rotate; //单位：度
            float alpha = lineRenderData.Alpha; // [0, 255]
            // rotate 是角度（度）
            float rad = Mathf.DegToRad(rotate);

            Transform2D transform = new Transform2D(rad, position);

            lineMultiMesh.SetInstanceTransform2D(
                lineVisibleCount, transform
            );

            //设置透明度
            Color lineColor = new Color
            {
                R8 = 237,
                G8 = 236,
                B8 = 176,
                A8 = Mathf.RoundToInt(alpha),
            };
            lineMultiMesh.SetInstanceColor(lineVisibleCount, lineColor);

            lineVisibleCount ++;
        }

        // -------- 渲染note --------
        foreach(NoteRenderData noteRenderData in noteRenderDatas)
        {
            NoteType type = noteRenderData.Type;

            if(type != NoteType.Hold) // 处理非Hold音符
            {
                Vector2 position = noteRenderData.HeadPos;
                float rotate = noteRenderData.Rotate; //单位：度
                float alpha = noteRenderData.Alpha; // [0, 255]
                // rotate 是角度（度）
                float rad = Mathf.DegToRad(rotate);

                //Transform2D transform = new Transform2D(rad, position);
                Transform2D transform = Transform2D.Identity
                    .Scaled(new Vector2(noteScale, noteScale))          // 缩放
                    .Rotated(rad)           // 旋转
                    .Translated(position);  // 平移

                //选择SpriteType
                SpriteType spriteType = type switch
                {
                    NoteType.Tap => SpriteType.Tap,
                    NoteType.Drag => SpriteType.Drag,
                    NoteType.Flick => SpriteType.Flick,
                    _ => SpriteType.Tap
                };

                multiMeshes[spriteType].SetInstanceTransform2D(
                    visibleCounts[spriteType], transform
                );
                multiMeshes[spriteType].SetInstanceColor(visibleCounts[spriteType], Colors.White);

                visibleCounts[spriteType] ++;
            }
            else // 处理Hold音符
            {
                Vector2 headPos = noteRenderData.HeadPos;
                Vector2 endPos = noteRenderData.EndPos;
                float rotate = noteRenderData.Rotate;
                float rad = Mathf.DegToRad(rotate);
                // ---- 1. 渲染 Hold 头部 ----
                {
                    Transform2D transform = Transform2D.Identity
                        .Translated(new Vector2(0, holdHeadTexture.GetSize().Y / 2f)) // 让上边对齐
                        .Scaled(new Vector2(noteScale, noteScale))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(headPos);  // 平移
                    
                    multiMeshes[SpriteType.HoldHead].SetInstanceTransform2D(
						visibleCounts[SpriteType.HoldHead],
						transform
					);
                    multiMeshes[SpriteType.HoldHead].SetInstanceColor(visibleCounts[SpriteType.HoldHead], Colors.White);
                    
                    visibleCounts[SpriteType.HoldHead]++;
                }

                // ---- 2. 渲染 Hold 身体（拉伸条） ----
                {
                    Vector2 bodyPos = (headPos + endPos) / 2f;
                    
                    float bodyLength = headPos.DistanceTo(endPos);   // 正数表示向下延伸
                    // 计算 Y 方向缩放：长度 / 纹理高度（纹理高度可自定，这里假设为 1900，与原注释一致）
					float scaleY = bodyLength / holdBodyTexture.GetSize().Y;

					Transform2D transform = Transform2D.Identity
                        .Scaled(new Vector2(noteScale, scaleY))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(bodyPos);  // 平移

					multiMeshes[SpriteType.HoldBody].SetInstanceTransform2D(
						visibleCounts[SpriteType.HoldBody], transform
					);
                    multiMeshes[SpriteType.HoldBody].SetInstanceColor(visibleCounts[SpriteType.HoldBody], Colors.White);

					visibleCounts[SpriteType.HoldBody]++;
                    
                }

                // ---- 3. 渲染 Hold 尾部 ----
                {
                    Transform2D transform = Transform2D.Identity
                        .Translated(new Vector2(0, -holdEndTexture.GetSize().Y / 2f)) // 让下边对齐
                        .Scaled(new Vector2(noteScale, noteScale))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(endPos);  // 平移
                    
                    multiMeshes[SpriteType.HoldEnd].SetInstanceTransform2D(
						visibleCounts[SpriteType.HoldEnd], transform
					);
                    multiMeshes[SpriteType.HoldEnd].SetInstanceColor(visibleCounts[SpriteType.HoldEnd], Colors.White);

                    visibleCounts[SpriteType.HoldEnd]++;
                }
            }
        }


        // 更新所有 MultiMesh 的可见实例数量
        lineMultiMesh.VisibleInstanceCount = lineVisibleCount;
        foreach (SpriteType type in allSpriteTypes)
        {
            multiMeshes[type].VisibleInstanceCount = visibleCounts[type];
        }
    }

}
