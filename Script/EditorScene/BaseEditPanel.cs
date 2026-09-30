using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public abstract partial class BaseEditPanel : Panel
{
	# region 桥接到_gridDrawer的属性
    // ---- 网格布局 ----
	public float HorMargin
    {
        get => _gridDrawer.HorMargin;
		set { if (_gridDrawer != null) _gridDrawer.HorMargin = value; }
	}

	public float VerMargin
	{
		get => _gridDrawer.VerMargin;
		set { if (_gridDrawer != null) _gridDrawer.VerMargin = value; }
	}

	public int SubBeatCount
	{
		get => _gridDrawer.SubBeatCount;
		set { if (_gridDrawer != null) _gridDrawer.SubBeatCount = value; }
	}

	public int VerLineCount
	{
		get => _gridDrawer.VerLineCount;
		set { if (_gridDrawer != null) _gridDrawer.VerLineCount = value; }
	}

	public float GroundY
	{
		get => _gridDrawer.GroundY;
		set { if (_gridDrawer != null) _gridDrawer.GroundY = value; }
	}
	// ---- 网格样式 ----
	public Color HorColor
	{
		get => _gridDrawer.HorColor;
		set { if (_gridDrawer != null) _gridDrawer.HorColor = value; }
	}

	public float HorWidth
	{
		get => _gridDrawer.HorWidth;
		set { if (_gridDrawer != null) _gridDrawer.HorWidth = value; }
	}

	public Color VerColor
	{
		get => _gridDrawer.VerColor;
		set { if (_gridDrawer != null) _gridDrawer.VerColor = value; }
	}

	public float VerWidth
	{
		get => _gridDrawer.VerWidth;
		set { if (_gridDrawer != null) _gridDrawer.VerWidth = value; }
	}

	public Color HorSubColor
	{
		get => _gridDrawer.HorSubColor;
		set { if (_gridDrawer != null) _gridDrawer.HorSubColor = value; }
	}

	public float HorSubWidth
	{
		get => _gridDrawer.HorSubWidth;
		set { if (_gridDrawer != null) _gridDrawer.HorSubWidth = value; }
	}

	public Color GroundLineColor
	{
		get => _gridDrawer.GroundLineColor;
		set { if (_gridDrawer != null) _gridDrawer.GroundLineColor = value; }
	}

	public float GroundLineWidth
	{
		get => _gridDrawer.GroundLineWidth;
		set { if (_gridDrawer != null) _gridDrawer.GroundLineWidth = value; }
	}

	#endregion

	// ---- 滚动/缩放 ----
	public float HorOffsetSmoothed { get; set; }

	public float HorSeparationSmoothed { get; set; }

    // ---- 数据 ----
    public Chart editingChart;
    protected int editingLineId;
	public int EditingLineId
	{
		get => editingLineId;
		set => editingLineId = value;
	}

    // ---- 字体 ----
    protected Font font = ThemeDB.FallbackFont;

	public enum SelectModeEnum
    {
        Single, // 单选
        Multi // 多选
    }
    public SelectModeEnum SelectMode { get; set; } = SelectModeEnum.Single;

	protected bool _isPasteMode = false;

	protected bool _isBoxSelectMode = false;

	public virtual bool IsBoxSelectMode
    {
        get => _isBoxSelectMode;
        set
        {
            _isBoxSelectMode = value;
        }
    }

	/// <summary>选择时点击位置与实际位置的最大距离</summary>
    [Export] protected float distanceThreshold = 40f;

	[Export] protected Color boxColor = new Color(1f, 0, 0, 0.471f);

	protected InputController _inputController;
    protected BoxSelectController _boxSelectController;
    protected CoordinateComponent _coordComponent;
    protected DragPlaceComponent _dragPlaceComponent;
	protected DragMoveComponent _dragMoveComponent;
	protected GridDrawer _gridDrawer;

	/// <summary>
    /// 框选矩形框的起始坐标 坐标系：Control坐标
    /// </summary>
    protected Vector2 boxStartPos;
    /// <summary>
    /// 框选矩形框的结束坐标 坐标系：Control坐标
    /// </summary>
    protected Vector2 boxEndPos;

	// ---- Multimesh ---- 
	private Dictionary<string, MultiMesh> multiMeshes = new();
	private Dictionary<string, MultiMeshInstance2D> multiMeshInstances = new();
	private Dictionary<string, int> visibleCounts = new();

	// 开关
	public bool Disabled { get; set; } = false; // 禁用所有刷新
	public bool GridDisabled { get; set; } = false; // 禁用渲染网格
	public bool ContentDisabled { get; set; } = false; // 禁用渲染物体

	/// <summary>
	/// 当用户点击空白处取消选择所有对象时发出
	/// </summary>
	public event Action AllDeselected;

	protected void EmitAllDeselected()
	{
		AllDeselected?.Invoke();
	}

	protected void RegisterMultiMesh(string key, Texture2D texture, int instanceCount, int zIndex = 1)
	{
		//设置Multimesh
		MultiMesh multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
			InstanceCount = 0,
			VisibleInstanceCount = 0,
			UseColors = true, // 用于提示选中
		};
		multiMesh.InstanceCount = instanceCount;
		multiMeshes[key] = multiMesh;

		MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
		multiMeshInstance.Texture = texture;
		multiMeshInstance.Multimesh = multiMesh;
		multiMeshInstance.ZIndex = zIndex;
		multiMeshInstances[key] = multiMeshInstance;

		// 根据纹理实际尺寸创建 QuadMesh
		var quad = new QuadMesh();
		quad.Size = new Vector2(texture.GetSize().X, -texture.GetSize().Y);   // 保持宽高比，去掉负值
		multiMeshInstance.Multimesh.Mesh = quad;

		AddChild(multiMeshInstance);
		multiMeshInstances[key] = multiMeshInstance;
		multiMeshes[key] = multiMesh;

		visibleCounts[key] = 0;
	}

	protected void ResetVisibleCount()
	{
		foreach (string key in visibleCounts.Keys)
		{
			visibleCounts[key] = 0;
		}
	}

	protected void ApplyVisibleCount()
	{
		foreach (string key in visibleCounts.Keys)
		{
			multiMeshes[key].VisibleInstanceCount = visibleCounts[key];
		}
	}

    public override void _Ready()
    {
        base._Ready();

		// 设置inputController
        _inputController = new InputController();
        _inputController.PointerDown += OnButtonDown;
        _inputController.PointerUp += OnButtonUp;
        _inputController.PointerDrag += OnMotionInput;

        // 设置_boxSelectController
        _boxSelectController = new BoxSelectController();
        _boxSelectController.BoxUpdated += OnBoxUpdated;
        _boxSelectController.BoxEnded += OnBoxEnded;

        // 设置_coordinateConverter
        _coordComponent = new CoordinateComponent();

        //设置_dragPlaceComponent
        _dragPlaceComponent = new DragPlaceComponent();
        _dragPlaceComponent.DragEnded += OnDragEnded;

		// 设置_dragMoveComponent
		_dragMoveComponent = new DragMoveComponent();

		// 设置_gridDrawer
		_gridDrawer = new GridDrawer();
		_gridDrawer.Parent = this;
		_gridDrawer.Font = font;
		_gridDrawer.ZIndex = 0; // 最底层
		AddChild(_gridDrawer);
    }
	
	public override void _ExitTree()
    {
        base._ExitTree();

        // 设置inputController
        _inputController.PointerDown -= OnButtonDown;
        _inputController.PointerUp -= OnButtonUp;
        _inputController.PointerDrag -= OnMotionInput;
        _inputController = null;

        // 设置_boxSelectController
        _boxSelectController.BoxUpdated -= OnBoxUpdated;
        _boxSelectController.BoxEnded -= OnBoxEnded;
        _boxSelectController = null;

        // 设置_coordinateConverter
        _coordComponent = null;

        //设置_dragPlaceComponent
        _dragPlaceComponent.DragEnded -= OnDragEnded;
        _dragPlaceComponent = null;

		// 设置_dragMoveComponent
		_dragMoveComponent = null;

		// 设置_gridDrawer
		_gridDrawer = null;
        
    }

    public override void _Draw()
    {
		// ============= 绘制框选矩形 ============= 
        if(_boxSelectController.IsDragging){
            Vector2 pos = new Vector2(
                Mathf.Min(boxStartPos.X, boxEndPos.X),
                Mathf.Min(boxStartPos.Y, boxEndPos.Y)
            );
            Vector2 size = new Vector2(
                Mathf.Abs(boxStartPos.X - boxEndPos.X),
                Mathf.Abs(boxStartPos.Y - boxEndPos.Y)
            );
            Rect2 rect = new Rect2(pos, size);
            DrawRect(
                rect: rect,
                color: boxColor,
                filled: false,
                width: 5
            );

        }
    }

    public void UpdateVisuals()
	{
		if(Disabled) return;

		//同步_coordinateConverter
        _coordComponent.horMargin = HorMargin;
        _coordComponent.verMargin = VerMargin;
        _coordComponent.subBeatCount = SubBeatCount;
        _coordComponent.verLineCount = VerLineCount;
        _coordComponent.horOffsetSmoothed = HorOffsetSmoothed;
        _coordComponent.horSeparationSmoothed = HorSeparationSmoothed;
        _coordComponent.parentSize = Size;
		_coordComponent.GroundY = GroundY;

		// 同步_gridDrawer
		_gridDrawer.HorOffset = HorOffsetSmoothed;
		_gridDrawer.HorSeparation = HorSeparationSmoothed;

		if (!ContentDisabled)
		{
			//归零可见数量
			ResetVisibleCount();

			// 更新渲染内容 (由子类重写)
			RenderContent();

			// 更新所有 MultiMesh 的可见实例数量
			ApplyVisibleCount();
		}

		if(!GridDisabled) _gridDrawer.QueueRedraw();// 触发网格重绘

		//绘制框选矩形
		QueueRedraw();
	}

	protected abstract void RenderContent();

	protected void RenderObject(string key, float localX, Beat beat, Vector2 offset, float scale, Action<MultiMesh, int> renderEffect)
	{
		// 动态扩容
		if(visibleCounts[key] + 1 > multiMeshes[key].InstanceCount)
		{
			EnsureMultiMeshCapacity(key, visibleCounts[key] + 1);
		}

		float beatBalue = beat[0] + beat[1] * 1f / beat[2];
		float localY = _coordComponent.GetPanelPosY(beatBalue);

		// 裁切：超出面板范围则不渲染
        if (localX < 0 || localX > Size.X || localY < 0 || localY > Size.Y) return;

		// 构建变换：位置 + 固定缩放
        // Transform2D transform = Transform2D.Identity;
        // transform.Origin = new Vector2(localX, localY);
        // transform.X = new Vector2(scale, 0);
        // transform.Y = new Vector2(0, scale);

		Transform2D transform = Transform2D.Identity
			.Translated(offset)                        // 对齐
			.Scaled(new Vector2(scale, scale))         // 缩放
			//.Rotated(rad)                            // 旋转
			.Translated(new Vector2(localX, localY));  // 平移

        multiMeshes[key].SetInstanceTransform2D(visibleCounts[key], transform);
        multiMeshes[key].SetInstanceColor(visibleCounts[key], Colors.White);

        // 渲染效果
        renderEffect?.Invoke(multiMeshes[key], visibleCounts[key]);

        visibleCounts[key]++;
	}

	protected void RenderLongObject(string key, float localX, Beat startBeat, Beat endBeat, Vector2 offset, float scale, Action<MultiMesh, int> renderEffect)
	{
		// 动态扩容
		if(visibleCounts[key] + 1 > multiMeshes[key].InstanceCount)
		{
			EnsureMultiMeshCapacity(key, visibleCounts[key] + 1);
		}
		
		float startBeatBalue = startBeat[0] + startBeat[1] * 1f / startBeat[2];
		float startLocalY = _coordComponent.GetPanelPosY(startBeatBalue);

		float endBeatBalue = endBeat[0] + endBeat[1] * 1f / endBeat[2];
		float endLocalY = _coordComponent.GetPanelPosY(endBeatBalue);


		float bodyLength = startLocalY - endLocalY;   // 正数表示向下延伸
            
		float midLocalY = (startLocalY + endLocalY) / 2f;

		// 计算 Y 方向缩放：长度 / 纹理高度（纹理高度可自定，这里假设为 1900，与原注释一致）
		Texture2D texture = multiMeshInstances[key].Texture;
		float scaleY = bodyLength / texture.GetSize().Y;

		Transform2D transform = Transform2D.Identity
			.Translated(offset)                        // 对齐
			.Scaled(new Vector2(scale, scaleY))         // 缩放
			//.Rotated(rad)                            // 旋转
			.Translated(new Vector2(localX, midLocalY));  // 平移

		
		multiMeshes[key].SetInstanceTransform2D(visibleCounts[key], transform);
		multiMeshes[key].SetInstanceColor(visibleCounts[key], Colors.White);
		
		// 渲染效果
        renderEffect?.Invoke(multiMeshes[key], visibleCounts[key]);

        visibleCounts[key]++;
	}

	public Vector2 GetScreenPosition(float beatValue, float posX)
    {
        Vector2 localPos = _coordComponent.GetPanelPosition(posX, beatValue);

        return GetScreenPosition(localPos);
    }

	public Vector2 GetScreenPosition(Vector2 localPos)
	{
		// 1. 获取视口（Viewport）的屏幕变换
        Transform2D screenTransform = GetViewport().GetScreenTransform();

        // 2. 获取节点自身的全局画布变换
        Transform2D globalCanvasTransform = GetGlobalTransformWithCanvas();

        // 3. 按顺序相乘：屏幕变换 * 全局画布变换 * 局部坐标
        Vector2 screenPos = screenTransform * (globalCanvasTransform * localPos);

		// GD.Print($"localPos:{localPos}, viewportPos:{globalCanvasTransform * localPos}, screenPos:{screenPos}");

        return screenPos;
	}

	public Vector2 GetViewportPos(Vector2 localPos)
	{
		Transform2D globalCanvasTransform = GetGlobalTransformWithCanvas();

		Vector2 viewportPos = globalCanvasTransform * localPos;

		return viewportPos;
	}


	public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);

		if(@event.Device == -1) return; // 拦截模拟输入

        // 只处理左键和触摸，其余事件（滚轮、中键）忽略
        bool handled = false;
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }
        else if (@event is InputEventMouseMotion mouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }
        else if (@event is InputEventScreenTouch touch)
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }
        else if (@event is InputEventScreenDrag drag)
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }

        if (handled) AcceptEvent(); // 标记事件已处理，阻止向上冒泡
        
    }

	/// <summary>
	/// 获取当前面板可见的 Beat 范围 [minBeat, maxBeat]
	/// </summary>
	protected void GetVisibleBeatRange(out float minBeat, out float maxBeat)
	{
		// 坐标公式: y = GroundY + horOffsetSmoothed - beatValue * horSeparationSmoothed
		// 反推: beatValue = (GroundY + horOffsetSmoothed - y) / horSeparationSmoothed
		float zeroLine = GroundY + HorOffsetSmoothed; // 零刻度线的Y坐标
		minBeat = (zeroLine - Size.Y) / HorSeparationSmoothed; // 底部对应较小 beat
		maxBeat = zeroLine / HorSeparationSmoothed;             // 顶部对应较大 beat
		if (minBeat < 0) minBeat = 0;
	}

	/// <summary>
	/// 动态扩容
	/// </summary>
	/// <param name="key"></param>
	/// <param name="needed"></param>
	protected void EnsureMultiMeshCapacity(string key, int needed)
	{
		if (needed <= 0) return;
		var mm = multiMeshes[key];
		if (needed <= mm.InstanceCount) return; // 容量足够，直接返回
		
		mm.InstanceCount = MathUtil.NextPowerOfTwo(needed);
		GD.Print($"[{Name}] MultiMesh '{key}' 扩容至 {mm.InstanceCount}");
	}

	protected abstract void OnButtonDown(Vector2 pos);

    protected abstract void OnButtonUp(Vector2 pos);
    
    protected abstract void OnMotionInput(Vector2 position, Vector2 relative);

	protected abstract void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos);

    protected abstract void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos);

	protected abstract void OnDragEnded(int verLineIndex, Beat startBeat, Beat endBeat);

	public abstract void DeselectAll();
    
    
}
