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
		get => Context?.GroundY ?? (_gridDrawer?.GroundY ?? 0f);
		set
		{
			if (Context != null) Context.GroundY = value;
			else if (_gridDrawer != null) _gridDrawer.GroundY = value;
		}
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
	// 统一从 EditorContext 读取，EditorScene 不再逐帧推送到面板。
	public float HorOffsetSmoothed
	{
		get => Context?.HorOffsetSmoothed ?? 0f;
		set { if (Context != null) Context.HorOffsetSmoothed = value; }
	}

	public float HorSeparationSmoothed
	{
		get => Context?.HorSeparationSmoothed ?? 0f;
		set { if (Context != null) Context.HorSeparationSmoothed = value; }
	}

    // ---- 场景级状态中心 / 编辑命令入口（由 EditorScene 注入）----

    /// <summary>场景级状态中心，谱面、判定线、事件层、视图与时间状态都由它统一持有</summary>
    public EditorContext Context { get; private set; }

    /// <summary>编辑命令入口。面板不再直接修改 Chart，所有修改都通过它执行。</summary>
    protected ChartEditService EditService { get; private set; }

    /// <summary>
    /// 由 EditorScene 在初始化时调用，注入场景级依赖。
    /// </summary>
    public void Initialize(EditorContext context, ChartEditService editService)
    {
        Context = context;
        EditService = editService;

        OnContextInjected();
    }

    /// <summary>子类可重写，用于在拿到 Context / EditService 之后做额外初始化</summary>
    protected virtual void OnContextInjected() { }

    // ---- 数据 ----
    /// <summary>正在编辑的谱面，来自 EditorContext</summary>
    public Chart editingChart => Context?.EditingChart;

    /// <summary>正在编辑的判定线编号，来自 EditorContext</summary>
    public int EditingLineId
	{
		get => Context?.EditingLineId ?? 0;
		set { if (Context != null) Context.EditingLineId = value; }
	}

    // ---- 字体 ----
    protected Font font = ThemeDB.FallbackFont;

	public enum SelectModeEnum
    {
        Single, // 单选
        Multi // 多选
    }
    public SelectModeEnum SelectMode
    {
        get => Context?.SelectMode ?? SelectModeEnum.Single;
        set { if (Context != null) Context.SelectMode = value; }
    }

	protected bool _isPasteMode = false;

	// protected bool _isBoxSelectMode = false;

	public virtual bool IsBoxSelectMode
    {
        get => Context?.IsBoxSelectMode ?? false;
        set
        {
            // _isBoxSelectMode = value;
            if (Context != null) Context.IsBoxSelectMode = value;
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

	/// <summary>GUI 输入过滤器（左键/拖拽/触摸 → InputController）</summary>
	private PanelInputHandler _panelInputHandler;

	/// <summary>
	/// MultiMesh 渲染器：注册池、每帧渲染、动态扩容、视口裁剪。
	/// 子类渲染时用 <c>Meshes.RenderObject(...)</c> / <c>Meshes.RenderLongObject(...)</c>。
	/// </summary>
	protected MultiMeshRenderer Meshes { get; private set; }

	/// <summary>
    /// 框选矩形框的起始坐标 坐标系：Control坐标
    /// </summary>
    protected Vector2 boxStartPos;
    /// <summary>
    /// 框选矩形框的结束坐标 坐标系：Control坐标
    /// </summary>
    protected Vector2 boxEndPos;

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

    public override void _Ready()
    {
        base._Ready();

		// 设置inputController
        _inputController = new InputController();
        _inputController.PointerDown += OnButtonDown;
        _inputController.PointerUp += OnButtonUp;
        _inputController.PointerDrag += OnMotionInput;

        // 设置_panelInputHandler
        _panelInputHandler = new PanelInputHandler(_inputController);

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

		// 设置 MultiMesh 渲染器（依赖 _coordComponent，必须在它之后创建）
		Meshes = new MultiMeshRenderer(this, _coordComponent);

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

        // 设置_panelInputHandler
        _panelInputHandler = null;

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

		// 设置 MultiMesh 渲染器
		Meshes = null;

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
		if(Disabled || Meshes == null || _coordComponent == null) return;

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
		_gridDrawer.GroundY = GroundY;

		if (!ContentDisabled)
		{
			//归零可见数量
			Meshes.BeginFrame();

			// 更新渲染内容 (由子类重写)
			RenderContent();

			// 更新所有 MultiMesh 的可见实例数量
			Meshes.EndFrame();
		}

		if(!GridDisabled) _gridDrawer.QueueRedraw();// 触发网格重绘

		//绘制框选矩形
		QueueRedraw();
	}

	protected abstract void RenderContent();

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

        // 过滤左键/拖拽/触摸后交给 InputController；已处理则阻止事件继续冒泡
        if (_panelInputHandler != null && _panelInputHandler.ProcessGuiInput(@event))
        {
            AcceptEvent();
        }
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

	protected abstract void OnButtonDown(Vector2 pos);

    protected abstract void OnButtonUp(Vector2 pos);

    protected abstract void OnMotionInput(Vector2 position, Vector2 relative);

	protected abstract void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos);

    protected abstract void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos);

	protected abstract void OnDragEnded(int verLineIndex, Beat startBeat, Beat endBeat);

	public abstract void DeselectAll();
    
    
}
