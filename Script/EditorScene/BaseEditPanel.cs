using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public abstract partial class BaseEditPanel : Panel
{
    // ---- 网格布局 ----
    [ExportGroup("网格布局设置")]
    [Export] protected float horMargin = 50;
    [Export] protected float verMargin = 100;
    [Export] protected int subBeatCount = 4;
    [Export] protected int verLineCount = 5; // 子类可重写默认值

    // ---- 网格样式 ----
    [ExportGroup("网格样式设置")]
    [Export] protected Color horColor = new Color(1f, 0, 0, 0.686f);
    [Export] protected float horWidth = 1;
    [Export] protected Color verColor = new Color(0, 1f, 0, 0.588f);
    [Export] protected float verWidth = 1;
    [Export] protected Color horSubColor = new Color(1f, 1f, 0, 0.588f);
    [Export] protected float horSubWidth = 1;

    // ---- 滚动/缩放 ----
    public float horOffsetSmoothed;
    public float horSeparationSmoothed;

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

	protected enum SelectMode
    {
        Single, // 单选
        Multi // 多选
    }
    protected SelectMode selectMode = SelectMode.Single;

	/// <summary>选择note时点击位置与实际位置的最大距离</summary>
    [Export] protected float distanceThreshold = 40f;

	[Export] protected Color boxColor = new Color(1f, 0, 0, 0.471f);

	protected InputController _inputController;
    protected BoxSelectController _boxSelectController;
    protected CoordinateComponent _coordComponent;
    protected DragPlaceComponent _dragPlaceComponent;

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

	protected void RegisterMultiMesh(string key, Texture2D texture)
	{
		//设置Multimesh
		MultiMesh multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
			InstanceCount = 0,
			VisibleInstanceCount = 0,
			UseColors = true, // 用于提示选中
		};
		multiMesh.InstanceCount = 10000;
		multiMeshes[key] = multiMesh;

		MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
		multiMeshInstance.Texture = texture;
		multiMeshInstance.Multimesh = multiMesh;
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
        
    }

    // ---- 绘制网格（基类统一实现） ----
    public override void _Draw()
    {
        DrawMainBeats();
        DrawSubBeats();
        DrawVerticalLines();

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

    private void DrawMainBeats()
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

    }

    private void DrawSubBeats()
    {
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

    private void DrawVerticalLines()
    {
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

    }

    // ---- 刷新框架 ----
    public override void _Process(double delta)
    {
        UpdateVisuals();      // 子类实现具体对象位置/纹理更新
        QueueRedraw();        // 触发网格重绘
    }

    protected void UpdateVisuals()
	{
		//同步_coordinateConverter
        _coordComponent.horMargin = horMargin;
        _coordComponent.verMargin = verMargin;
        _coordComponent.subBeatCount = subBeatCount;
        _coordComponent.verLineCount = verLineCount;
        _coordComponent.horOffsetSmoothed = horOffsetSmoothed;
        _coordComponent.horSeparationSmoothed = horSeparationSmoothed;
        _coordComponent.parentSize = Size;

		//归零可见数量
		ResetVisibleCount();

		// 更新渲染内容 (由子类重写)
		RenderContent();

		// 更新所有 MultiMesh 的可见实例数量
        ApplyVisibleCount();
	}

	protected abstract void RenderContent();

	protected void RenderObject(string key, float localX, Beat beat, Vector2 offset, float scale, Action<MultiMesh, int> renderEffect)
	{
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
	protected abstract void OnButtonDown(Vector2 pos);

    protected abstract void OnButtonUp(Vector2 pos);
    
    protected abstract void OnMotionInput(Vector2 position, Vector2 relative);

	protected abstract void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos);

    protected abstract void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos);

	protected abstract void OnDragEnded(int verLineIndex, Beat startBeat, Beat endBeat);
    
    
}
