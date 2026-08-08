using Godot;
using System;
using System.Collections.Generic;

public partial class ChartRenderer : BaseChartRenderer
{
    private enum NoteSpriteType
	{
		Tap, Drag, Flick, HoldHead, HoldBody, HoldEnd
	}
	private readonly NoteSpriteType[] allNoteSpriteTypes = (NoteSpriteType[])Enum.GetValues(typeof(NoteSpriteType));

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

    // 类顶部增加缓存，优化性能
    private readonly Color _white = Colors.White;
    private readonly Color _lineBaseColor = new Color
    {
        R8 = 237,
        G8 = 236,
        B8 = 175,
        A8 = 255
    };
    private Vector2 _holdHeadSize;
    private Vector2 _holdBodySize;
    private Vector2 _holdEndSize;


    // ---- Multimesh ---- 
	private Dictionary<NoteSpriteType, MultiMesh> multiMeshes = new();
	private Dictionary<NoteSpriteType, MultiMeshInstance2D> multiMeshInstances = new();
	private Dictionary<NoteSpriteType, int> visibleCounts = new();

    private MultiMesh lineMultiMesh;
    private MultiMeshInstance2D lineMultiMeshInstance;
    private int lineVisibleCount = 0;

    /// <summary>note的宽度大小缩放</summary>
    public float noteScale;

    #if TOOLS
    // ---- 性能优化 ----
    private int _noteCount = 0;
    #endif

    public override void _Ready()
    {
        base._Ready();

        #if TOOLS
        Performance.AddCustomMonitor("ChartRenderer/NoteCount", Callable.From(() => _noteCount));
        #endif
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        #if TOOLS
        Performance.RemoveCustomMonitor("ChartRenderer/NoteCount");
        #endif
    }



    public override void Initialize(Control parent)
    {
        // 预计算常量
        _holdHeadSize = holdHeadTexture.GetSize();
        _holdBodySize = holdBodyTexture.GetSize();
        _holdEndSize = holdEndTexture.GetSize();

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
        // ---- 设置line的multimesh ----
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
            lineMultiMesh.InstanceCount = 64;

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

        // ---- 设置note的MultiMesh ----
        //倒序遍历，先添加hold，再添加其他note，确保hold渲染在其他note下面
        for (int i = allNoteSpriteTypes.Length - 1; i >= 0; i--)
		{
            NoteSpriteType type = allNoteSpriteTypes[i];

            Texture2D texture = type switch
			{
				NoteSpriteType.Tap => tapTexture,
				NoteSpriteType.Drag => dragTexture,
				NoteSpriteType.Flick => flickTexture,
				NoteSpriteType.HoldHead => holdHeadTexture,
				NoteSpriteType.HoldBody => holdBodyTexture,
				NoteSpriteType.HoldEnd => holdEndTexture,
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
            multiMesh.InstanceCount = 128;
			multiMeshes[type] = multiMesh;

			MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
            multiMeshInstance.Texture = texture;
            multiMeshInstance.Multimesh = multiMesh;
            multiMeshInstance.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
			multiMeshInstances[type] = multiMeshInstance;

			// 根据纹理实际尺寸创建 QuadMesh
			var quad = new QuadMesh();
			quad.Size = new Vector2(texture.GetSize().X, -texture.GetSize().Y);
			multiMeshInstance.Multimesh.Mesh = quad;

			Parent.AddChild(multiMeshInstance);
			multiMeshInstances[type] = multiMeshInstance;
            multiMeshes[type] = multiMesh;
		}

    }

    /// <summary>
	/// 动态扩容(仅限note的MultiMesh)
	/// </summary>
	/// <param name="type"></param>
	/// <param name="needed"></param>
	private void EnsureNoteMultiMeshCapacity(NoteSpriteType type, int needed)
	{
		if (needed <= 0) return;

        MultiMesh mm = multiMeshes[type];

        if (needed <= mm.InstanceCount) return; // 容量足够，直接返回
		
		mm.InstanceCount = MathUtil.NextPowerOfTwo(needed);

		GD.Print($"[{Name}] MultiMesh '{type}' 扩容至 {mm.InstanceCount}");
	}

    /// <summary>
	/// 动态扩容(仅限line的MultiMesh)
	/// </summary>
	/// <param name="type"></param>
	/// <param name="needed"></param>
	private void EnsureLineMultiMeshCapacity(int needed)
	{
		if (needed <= 0) return;

        MultiMesh mm = lineMultiMesh;

        if (needed <= mm.InstanceCount) return; // 容量足够，直接返回
		
		mm.InstanceCount = MathUtil.NextPowerOfTwo(needed);

		GD.Print($"[{Name}] MultiMesh 'Line' 扩容至 {mm.InstanceCount}");
	}

    public override void Render(List<JudgeLineRenderData> lineRenderDatas, List<NoteRenderData> noteRenderDatas)
    {
        _noteCount = 0;
        if(Disabled) return;

        // ---- 缓存视口参数 ----
        Vector2 vpSize = Parent.Size;
        float margin = 300f; // 留边距，避免贴图边缘突然消失
        float minX = -margin, maxX = vpSize.X + margin;
        float minY = -margin, maxY = vpSize.Y + margin;

        // 快速判断辅助函数
        bool InViewport(Vector2 p) => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;
        
        // 对于 Hold：head 和 end 都在同一侧屏幕外时才剔除
        bool HoldInViewport(Vector2 head, Vector2 end)
        {
            if ((head.X < minX && end.X < minX) || (head.X > maxX && end.X > maxX)) return false;
            if ((head.Y < minY && end.Y < minY) || (head.Y > maxY && end.Y > maxY)) return false;
            return true;
        }

		//归零可见数量
        lineVisibleCount = 0;
		foreach(NoteSpriteType spriteType in allNoteSpriteTypes)
		{
			visibleCounts[spriteType] = 0;
		}
        
        // -------- 渲染判定线 --------
        foreach(JudgeLineRenderData lineRenderData in lineRenderDatas)
        {
            // 动态扩容
            if(lineVisibleCount + 1 > lineMultiMesh.InstanceCount)
            {
                EnsureLineMultiMeshCapacity(lineVisibleCount + 1);
            }

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
            Color lineColor = _lineBaseColor;
            lineColor.A8 = Mathf.RoundToInt(alpha);
            
            lineMultiMesh.SetInstanceColor(lineVisibleCount, lineColor);

            lineVisibleCount ++;
        }

        // -------- 渲染note --------
        foreach(NoteRenderData noteRenderData in noteRenderDatas)
        {
            NoteType type = noteRenderData.Type;

            if(type != NoteType.Hold) // 处理非Hold音符
            {
                // 剔除屏幕外
                if (!InViewport(noteRenderData.HeadPos)) continue;

                //选择SpriteType
                NoteSpriteType spriteType = type switch
                {
                    NoteType.Tap => NoteSpriteType.Tap,
                    NoteType.Drag => NoteSpriteType.Drag,
                    NoteType.Flick => NoteSpriteType.Flick,
                    _ => NoteSpriteType.Tap
                };

                // 动态扩容
                if(visibleCounts[spriteType] + 1 > multiMeshes[spriteType].InstanceCount)
                {
                    EnsureNoteMultiMeshCapacity(spriteType, visibleCounts[spriteType] + 1);
                }

                _noteCount++;

                Vector2 position = noteRenderData.HeadPos;
                float rotate = noteRenderData.Rotate; //单位：度
                float alpha = noteRenderData.Alpha; // [0, 255]
                // rotate 是角度（度）
                float rad = Mathf.DegToRad(rotate);
                float sizeX = noteRenderData.SizeX;

                //Transform2D transform = new Transform2D(rad, position);
                Transform2D transform = Transform2D.Identity
                    .Scaled(new Vector2(noteScale * sizeX, noteScale))          // 缩放
                    .Rotated(rad)           // 旋转
                    .Translated(position);  // 平移


                multiMeshes[spriteType].SetInstanceTransform2D(
                    visibleCounts[spriteType], transform
                );

                //设置颜色和透明度
                Color color = new Color
                {
                    R8 = 255,
                    G8 = 255,
                    B8 = 255,
                    A8 = Mathf.FloorToInt(alpha)
                };
                multiMeshes[spriteType].SetInstanceColor(visibleCounts[spriteType], color);

                visibleCounts[spriteType] ++;
            }
            else // 处理Hold音符
            {
                // 剔除屏幕外
                if (!HoldInViewport(noteRenderData.HeadPos, noteRenderData.EndPos)) continue;

                _noteCount++;

                Vector2 headPos = noteRenderData.HeadPos;
                Vector2 endPos = noteRenderData.EndPos;
                float rotate = noteRenderData.Rotate;
                float rad = Mathf.DegToRad(rotate);
                float alpha = noteRenderData.Alpha; // [0, 255]
                float sizeX = noteRenderData.SizeX;

                // ---- 1. 渲染 Hold 头部 ----
                if(noteRenderData.HeadVisible){
                    // 动态扩容
                    if(visibleCounts[NoteSpriteType.HoldHead] + 1 > multiMeshes[NoteSpriteType.HoldHead].InstanceCount)
                    {
                        EnsureNoteMultiMeshCapacity(NoteSpriteType.HoldHead, visibleCounts[NoteSpriteType.HoldHead] + 1);
                    }

                    Transform2D transform = Transform2D.Identity
                        .Translated(new Vector2(0, _holdHeadSize.Y / 2f)) // 让上边对齐
                        .Scaled(new Vector2(noteScale * sizeX, noteScale))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(headPos);  // 平移
                    
                    multiMeshes[NoteSpriteType.HoldHead].SetInstanceTransform2D(
						visibleCounts[NoteSpriteType.HoldHead],
						transform
					);

                    //设置颜色和透明度
                    Color color = _white;
                    color.A8 = Mathf.FloorToInt(alpha);
                    // {
                    //     R8 = 255,
                    //     G8 = 255,
                    //     B8 = 255,
                    //     A8 = Mathf.FloorToInt(alpha)
                    // };
                    multiMeshes[NoteSpriteType.HoldHead].SetInstanceColor(visibleCounts[NoteSpriteType.HoldHead], color);
                    
                    visibleCounts[NoteSpriteType.HoldHead]++;
                }

                // ---- 2. 渲染 Hold 身体（拉伸条） ----
                {
                    // 动态扩容
                    if(visibleCounts[NoteSpriteType.HoldBody] + 1 > multiMeshes[NoteSpriteType.HoldBody].InstanceCount)
                    {
                        EnsureNoteMultiMeshCapacity(NoteSpriteType.HoldBody, visibleCounts[NoteSpriteType.HoldBody] + 1);
                    }

                    Vector2 bodyPos = (headPos + endPos) / 2f;
                    
                    float bodyLength = headPos.DistanceTo(endPos);   // 正数表示向下延伸
                    // 计算 Y 方向缩放：长度 / 纹理高度（纹理高度可自定，这里假设为 1900，与原注释一致）
					float scaleY = bodyLength / _holdBodySize.Y;

					Transform2D transform = Transform2D.Identity
                        .Scaled(new Vector2(noteScale * sizeX, scaleY))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(bodyPos);  // 平移

					multiMeshes[NoteSpriteType.HoldBody].SetInstanceTransform2D(
						visibleCounts[NoteSpriteType.HoldBody], transform
					);

                    //设置颜色和透明度
                    Color color = _white;
                    color.A8 = Mathf.FloorToInt(alpha);
                    // {
                    //     R8 = 255,
                    //     G8 = 255,
                    //     B8 = 255,
                    //     A8 = Mathf.FloorToInt(alpha)
                    // };
                    multiMeshes[NoteSpriteType.HoldBody].SetInstanceColor(visibleCounts[NoteSpriteType.HoldBody], color);

					visibleCounts[NoteSpriteType.HoldBody]++;
                    
                }

                // ---- 3. 渲染 Hold 尾部 ----
                {
                    // 动态扩容
                    if(visibleCounts[NoteSpriteType.HoldEnd] + 1 > multiMeshes[NoteSpriteType.HoldEnd].InstanceCount)
                    {
                        EnsureNoteMultiMeshCapacity(NoteSpriteType.HoldEnd, visibleCounts[NoteSpriteType.HoldEnd] + 1);
                    }

                    Transform2D transform = Transform2D.Identity
                        .Translated(new Vector2(0, -_holdEndSize.Y / 2f)) // 让下边对齐
                        .Scaled(new Vector2(noteScale * sizeX, noteScale))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(endPos);  // 平移
                    
                    multiMeshes[NoteSpriteType.HoldEnd].SetInstanceTransform2D(
						visibleCounts[NoteSpriteType.HoldEnd], transform
					);

                    //设置颜色和透明度
                    Color color = _white;
                    color.A8 = Mathf.FloorToInt(alpha);
                    // {
                    //     R8 = 255,
                    //     G8 = 255,
                    //     B8 = 255,
                    //     A8 = Mathf.FloorToInt(alpha)
                    // };
                    multiMeshes[NoteSpriteType.HoldEnd].SetInstanceColor(visibleCounts[NoteSpriteType.HoldEnd], color);

                    visibleCounts[NoteSpriteType.HoldEnd]++;
                }
            }
        }


        // 更新所有 MultiMesh 的可见实例数量
        lineMultiMesh.VisibleInstanceCount = lineVisibleCount;
        foreach (NoteSpriteType type in allNoteSpriteTypes)
        {
            multiMeshes[type].VisibleInstanceCount = visibleCounts[type];
        }
    }

}
