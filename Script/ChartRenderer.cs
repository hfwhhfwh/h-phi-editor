using Godot;
using System;
using System.Collections.Generic;

public partial class ChartRenderer : BaseChartRenderer
{
    private enum NoteSpriteType
	{
        Tap, Drag, Flick, HoldHead, HoldBody, HoldEnd,
        TapMh, DragMh, FlickMh, HoldHeadMh, HoldBodyMh, HoldEndMh
	}
	private readonly NoteSpriteType[] allNoteSpriteTypes = (NoteSpriteType[])Enum.GetValues(typeof(NoteSpriteType));

    #region 默认纹理贴图
    [ExportGroup("默认纹理贴图")]
    [Export] private Texture2D _defaultTapTexture;
    [Export] private Texture2D _defaultDragTexture;
    [Export] private Texture2D _defaultFlickTexture;
    [Export] private Texture2D _defaultHoldHeadTexture;
    [Export] private Texture2D _defaultHoldBodyTexture;
    [Export] private Texture2D _defaultHoldEndTexture;

    [Export] private Texture2D _defaultTapTextureMh;
    [Export] private Texture2D _defaultDragTextureMh;
    [Export] private Texture2D _defaultFlickTextureMh;
    [Export] private Texture2D _defaultHoldHeadTextureMh;
    [Export] private Texture2D _defaultHoldBodyTextureMh;
    [Export] private Texture2D _defaultHoldEndTextureMh;

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
    private Vector2 _holdHeadMhSize;
    private Vector2 _holdBodyMhSize;
    private Vector2 _holdEndMhSize;
    


    // ---- Multimesh ---- 
	private Dictionary<NoteSpriteType, MultiMesh> multiMeshes = new();
	private Dictionary<NoteSpriteType, MultiMeshInstance2D> multiMeshInstances = new();
	private Dictionary<NoteSpriteType, int> visibleCounts = new();

    private MultiMesh lineMultiMesh;
    private MultiMeshInstance2D lineMultiMeshInstance;
    private int lineVisibleCount = 0;

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

    public override void UseDefaultResource()
    {
        TapTexture = _defaultTapTexture;
        DragTexture = _defaultDragTexture;
        FlickTexture = _defaultFlickTexture;
        HoldHeadTexture = _defaultHoldHeadTexture;
        HoldBodyTexture = _defaultHoldBodyTexture;
        HoldEndTexture = _defaultHoldEndTexture;

        TapMhTexture = _defaultTapTextureMh;
        DragMhTexture = _defaultDragTextureMh;
        FlickMhTexture = _defaultFlickTextureMh;
        HoldHeadMhTexture = _defaultHoldHeadTextureMh;
        HoldBodyMhTexture = _defaultHoldBodyTextureMh;
        HoldEndMhTexture = _defaultHoldEndTextureMh;
    }


    public override void Initialize(Control parent)
    {
        // 预计算常量
        _holdHeadSize = HoldHeadTexture.GetSize();
        _holdBodySize = HoldBodyTexture.GetSize();
        _holdEndSize = HoldEndTexture.GetSize();

        _holdHeadMhSize = HoldHeadMhTexture.GetSize();
        _holdBodyMhSize = HoldBodyMhTexture.GetSize();
        _holdEndMhSize = HoldEndMhTexture.GetSize();

        Parent = parent;
        UpdateNoteScale();

        //设置note的宽度缩放
        Parent.Resized += () =>
        {
            UpdateNoteScale();
        };

        // 初始化MultiMesh
        InitMultiMesh();
    }

    private void UpdateNoteScale()
    {
        if (Parent == null || TapTexture == null)
        {
            NoteScale = 1f;
            return;
        }

        float textureWidth = TapTexture.GetWidth();
        if (textureWidth <= 0f)
        {
            NoteScale = 1f;
            return;
        }

        NoteScale = Parent.Size.X * 0.16f / textureWidth;
        if (float.IsNaN(NoteScale) || float.IsInfinity(NoteScale) || NoteScale <= 0f)
            NoteScale = 1f;
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
				NoteSpriteType.Tap => TapTexture,
				NoteSpriteType.Drag => DragTexture,
				NoteSpriteType.Flick => FlickTexture,
				NoteSpriteType.HoldHead => HoldHeadTexture,
				NoteSpriteType.HoldBody => HoldBodyTexture,
				NoteSpriteType.HoldEnd => HoldEndTexture,
                NoteSpriteType.TapMh => TapMhTexture,
                NoteSpriteType.DragMh => DragMhTexture,
                NoteSpriteType.FlickMh => FlickMhTexture,
                NoteSpriteType.HoldHeadMh => HoldHeadMhTexture,
                NoteSpriteType.HoldBodyMh => HoldBodyMhTexture,
                NoteSpriteType.HoldEndMh => HoldEndMhTexture,
				_ => TapTexture
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

    public override void Render(
        JudgeLineRenderData[] lineData, int lineCount,
        NoteRenderData[] noteData, int noteCount)
    {
        #if TOOLS
        _noteCount = 0;
        #endif

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
        for (int i = 0; i < lineCount; i++)
        {
            JudgeLineRenderData lineRenderData = lineData[i];

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
        for (int i = 0; i < noteCount; i++)
        {
            var noteRenderData = noteData[i];
            NoteType type = noteRenderData.Type;

            if(type != NoteType.Hold) // 处理非Hold音符
            {
                // 剔除屏幕外
                if (!InViewport(noteRenderData.HeadPos)) continue;

                //选择SpriteType
                NoteSpriteType spriteType = type switch
                {
                    NoteType.Tap => noteRenderData.IsMultiHold ? NoteSpriteType.TapMh : NoteSpriteType.Tap,
                    NoteType.Drag => noteRenderData.IsMultiHold ? NoteSpriteType.DragMh : NoteSpriteType.Drag,
                    NoteType.Flick => noteRenderData.IsMultiHold ? NoteSpriteType.FlickMh : NoteSpriteType.Flick,
                    _ => noteRenderData.IsMultiHold ? NoteSpriteType.TapMh : NoteSpriteType.Tap
                };

                // 动态扩容
                if(visibleCounts[spriteType] + 1 > multiMeshes[spriteType].InstanceCount)
                {
                    EnsureNoteMultiMeshCapacity(spriteType, visibleCounts[spriteType] + 1);
                }

                #if TOOLS
                _noteCount++;
                #endif

                Vector2 position = noteRenderData.HeadPos;
                float rotate = noteRenderData.Rotate; //单位：度
                float alpha = noteRenderData.Alpha; // [0, 255]
                // rotate 是角度（度）
                float rad = Mathf.DegToRad(rotate);
                float sizeX = noteRenderData.SizeX;

                //Transform2D transform = new Transform2D(rad, position);
                Transform2D transform = Transform2D.Identity
                    .Scaled(new Vector2(NoteScale * sizeX, NoteScale))          // 缩放
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

                #if TOOLS
                _noteCount++;
                #endif

                Vector2 headPos = noteRenderData.HeadPos;
                Vector2 endPos = noteRenderData.EndPos;
                float rotate = noteRenderData.Rotate;
                float rad = Mathf.DegToRad(rotate);
                float alpha = noteRenderData.Alpha; // [0, 255]
                float sizeX = noteRenderData.SizeX;

                NoteSpriteType holdHeadType = noteRenderData.IsMultiHold
                    ? NoteSpriteType.HoldHeadMh
                    : NoteSpriteType.HoldHead;
                NoteSpriteType holdBodyType = noteRenderData.IsMultiHold
                    ? NoteSpriteType.HoldBodyMh
                    : NoteSpriteType.HoldBody;
                NoteSpriteType holdEndType = noteRenderData.IsMultiHold
                    ? NoteSpriteType.HoldEndMh
                    : NoteSpriteType.HoldEnd;
                
                float headSizeY = noteRenderData.IsMultiHold
                    ? _holdHeadSize.Y
                    : _holdHeadMhSize.Y;
                float bodySizeY = noteRenderData.IsMultiHold
                    ? _holdBodySize.Y
                    : _holdBodyMhSize.Y;
                float endSizeY = noteRenderData.IsMultiHold
                    ? _holdEndSize.Y
                    : _holdEndMhSize.Y;

                // ---- 1. 渲染 Hold 头部 ----
                if(noteRenderData.HeadVisible){
                    // 动态扩容
                    if(visibleCounts[holdHeadType] + 1 > multiMeshes[holdHeadType].InstanceCount)
                    {
                        EnsureNoteMultiMeshCapacity(holdHeadType, visibleCounts[holdHeadType] + 1);
                    }

                    Transform2D transform = Transform2D.Identity
                        .Translated(new Vector2(0, headSizeY / 2f)) // 让上边对齐
                        .Scaled(new Vector2(NoteScale * sizeX, NoteScale))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(headPos);  // 平移
                    
                    multiMeshes[holdHeadType].SetInstanceTransform2D(
                        visibleCounts[holdHeadType],
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
                    multiMeshes[holdHeadType].SetInstanceColor(visibleCounts[holdHeadType], color);
                    
                    visibleCounts[holdHeadType]++;
                }

                // ---- 2. 渲染 Hold 身体（拉伸条） ----
                {
                    // 动态扩容
                    if(visibleCounts[holdBodyType] + 1 > multiMeshes[holdBodyType].InstanceCount)
                    {
                        EnsureNoteMultiMeshCapacity(holdBodyType, visibleCounts[holdBodyType] + 1);
                    }

                    Vector2 bodyPos = (headPos + endPos) / 2f;
                    
                    float bodyLength = headPos.DistanceTo(endPos);   // 正数表示向下延伸
                    // 计算 Y 方向缩放：长度 / 纹理高度（纹理高度可自定，这里假设为 1900，与原注释一致）
					float scaleY = bodyLength / bodySizeY;

					Transform2D transform = Transform2D.Identity
                        .Scaled(new Vector2(NoteScale * sizeX, scaleY))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(bodyPos);  // 平移

                    multiMeshes[holdBodyType].SetInstanceTransform2D(
                        visibleCounts[holdBodyType], transform
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
                    multiMeshes[holdBodyType].SetInstanceColor(visibleCounts[holdBodyType], color);

					visibleCounts[holdBodyType]++;
                    
                }

                // ---- 3. 渲染 Hold 尾部 ----
                {
                    // 动态扩容
                    if(visibleCounts[holdEndType] + 1 > multiMeshes[holdEndType].InstanceCount)
                    {
                        EnsureNoteMultiMeshCapacity(holdEndType, visibleCounts[holdEndType] + 1);
                    }

                    Transform2D transform = Transform2D.Identity
                        .Translated(new Vector2(0, -endSizeY / 2f)) // 让下边对齐
                        .Scaled(new Vector2(NoteScale * sizeX, NoteScale))          // 缩放
                        .Rotated(rad)           // 旋转
                        .Translated(endPos);  // 平移
                    
                    multiMeshes[holdEndType].SetInstanceTransform2D(
                        visibleCounts[holdEndType], transform
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
                    multiMeshes[holdEndType].SetInstanceColor(visibleCounts[holdEndType], color);

                    visibleCounts[holdEndType]++;
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
