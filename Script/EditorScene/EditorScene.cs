using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public enum EditPanelType
{
    NoteEdit,
    LineEventEdit,
    BpmEventEdit
}

public partial class EditorScene : Node
{
    [ExportGroup("虚拟摇杆引用")]
	[Export] private VirtualJoystick slideJoystick;
	[Export] private VirtualJoystick zoomJoystick;

    [ExportGroup("灵敏度设置")]
	[Export] private float verMouseSensitivity = 100f; // 鼠标滚轮竖直滚动的灵敏度
    [Export] private float zoomMouseSensitivity = 1f; // 鼠标滚轮竖直缩放的灵敏度
	[Export] private float verJoystickSensitivity = 1500f; // 虚拟摇杆竖直滚动的灵敏度
	[Export] private float zoomJoystickSensitivity = 2f; // 虚拟摇杆缩放的灵敏度
    [Export] private float verJoystickTimeSens = 2f; // 滚动时间时的虚拟摇杆灵敏度

    [ExportGroup("资源引用")]
    [Export] private Theme theme;

    [ExportGroup("")]
    [Export] private NoteEditPanel noteEditPanel;
    [Export] private EventEditPanel eventEditPanel;
    [Export] private BpmEditPanel bpmEditPanel;
    [Export] private BaseChartPlayer chartPlayer;
    [Export] private BaseChartRenderer chartRenderer;
    [Export] private Control chartPlayParent;
    [Export] private TextureRect _bgImageRect;
    [Export] private Control editPanel;
    [Export] private RightPanel rightPanel;
    [Export] private ChooseLinePanel chooseLinePanel;
    [Export] private NoteInfoPanel noteInfoPanel;
    [Export] private LineEventInfoPanel eventInfoPanel;
    [Export] private BpmInfoPanel bpmInfoPanel;
    [Export] private NoteChooser noteChooser;
    [Export] private MenuButton fileMenuButtion;
    [Export] private MenuButton editMenuButtion;
    [Export] private MenuButton viewMenuButton;
    [Export] private MenuButton helpMenuButtion;
    [Export] private Button _othersButton;
    [Export] private SettingsPanel _settingsPanel;
    [Export] private EditorSettingsPanel _editorSettingsPanel;
    [Export] private Button _undoBtn;
    [Export] private Button _redoBtn;
    [Export] private Button _copyBtn;
    [Export] private Button _pasteBtn;
    [Export] private Button _multiSelectBtn;
    [Export] private Button _boxSelectBtn;
    [Export] private Button _pasteConfirmBtn;
    [Export] private Button _pasteCancelBtn;
    [Export] private Button _deleteBtn;



    [Export] private Label editingLineLabel;
    [Export] private Label fpsLabel;
    [Export] private Label editModeLabel;

    private InputManager _inputManager;
    private ChartService _chartService;
    private ChartEditService _chartEditService;
    private EditorSettings _editorSettings;

    /// <summary>
    /// 场景级状态中心：谱面、判定线、事件层、视图与时间状态的唯一来源。
    /// </summary>
    private EditorContext _context;

    // 场景中可调的视图初值，在 _Ready 中写入 EditorContext；此后视图状态只存在于 Context。
    [Export] private float horOffset;
	[Export] private float horSeparation = 100f;
    [Export] private float groundY = 450f; // 当前时间点在EditPanel上的Y坐标（向下偏移）

    private bool isPlaying; // 是否正在播放铺面

    // ---- 兼容既有代码的只读访问器：数据实际存放在 EditorContext 中 ----
    private Chart editingChart => _context?.EditingChart;
    private string editingChartId => _context?.EditingChartId;

    private int EditingLineId
    {
        get => _context?.EditingLineId ?? 0;
        set { if (_context != null) _context.EditingLineId = value; }
    }

    private int EditingLayer
    {
        get => _context?.EditingLayer ?? 0;
        set { if (_context != null) _context.EditingLayer = value; }
    }

    // ---- 视图/时间状态的读写入口，全部转发到 EditorContext ----
    public float BeatValue
    {
        get => _context?.BeatValue ?? 0f;
        set { if (_context != null) _context.BeatValue = value; }
    }

    public double ChartTime
    {
        get => _context?.ChartTime ?? 0d;
        set { if (_context != null) _context.ChartTime = value; }
    }

    // 皮肤资源包
    private ResourcePack _resourcePack;

    private bool _isReady = false;

    // 剪切板
    private EditorClipboard _editorClipboard = new();

    /// <summary>
    /// 当前哪一个面板正在进行多选
    /// </summary>
    private EditPanelType _selectFocusPanel;

    public EditPanelType SelectFocusPanel 
    {
        get => _selectFocusPanel;
        set
        {
            _selectFocusPanel = value;

            // 同时设置所有面板是否选择对象
            List<BaseEditPanel> editPanels = [noteEditPanel, eventEditPanel, bpmEditPanel];
            List<EditPanelType> editPanelTypes = [EditPanelType.NoteEdit, EditPanelType.LineEventEdit, EditPanelType.BpmEventEdit];
            
            for(int i = 0; i < editPanels.Count; i++)
            {
                BaseEditPanel panel = editPanels[i];
                EditPanelType type = editPanelTypes[i];

                if(type != _selectFocusPanel) panel.DeselectAll();
            }
        }
    }

    /// <summary>
    /// 对当前哪一个面板正在进行粘贴操作
    /// </summary>
    private EditPanelType _pasteFocusPanel;


    private bool _isSelecting = false;

    /// <summary>
    /// 是否正在选择某些对象（Note、Event等），单选多选都算
    /// </summary>
    private bool IsSelecting
    {
        get => _isSelecting;
        set
        {
            _isSelecting = value;
            _deleteBtn.Visible = value;
        }
    }

    private readonly List<Action> _unsubscribes = new();

    private const float BlurRadius = 150;

    #if TOOLS
    // ---- 性能分析 ----
    private double _setChartTimeTimeUs = 0;
    private double _logicTimeUs = 0;
    private double _renderTimeUs = 0;
    private double _uiTimeUs = 0;
    private double _drawEditPanelTimeUs = 0;

    #endif

    /// <summary>
	/// 用于缩放
	/// </summary>
	/// <param name="zoomDelta">缩放比例</param>
	public void Zoom(float zoomDelta)
	{
        if (_context == null) return;

		_context.HorSeparation *= 1f + zoomDelta;

        // 限制不能缩放到负数
        if(_context.HorSeparation < 0) _context.HorSeparation = -_context.HorSeparation;

		//确保当前处于的beat不变
		_context.HorOffset = _context.BeatValue * _context.HorSeparation;
	}

    /// <summary>
    /// 按照编辑面板的距离滚动
    /// </summary>
    /// <param name="deltaY"></param>
    public void Slide(float deltaY)
	{
        if (_context == null) return;

        _context.HorOffset += deltaY;
        //限制不能滚动到0以下
        if(_context.HorOffset < 0) _context.HorOffset = 0;

        _context.BeatValue = _context.HorOffset / _context.HorSeparation;
	}

    /// <summary>
    /// 按照时间单位滚动
    /// </summary>
    /// <param name="deltaTime"></param>
    private void SlideTime(float deltaTime)
    {
        if (_context == null) return;

        _context.ChartTime = _context.ChartTime + deltaTime;

        //限制不能滚动到0以下
        if(_context.ChartTime < 0) _context.ChartTime = 0;

        // 此时BeatValue收到牵连改变，需要更新HorOffset
        _context.HorOffset = _context.BeatValue * _context.HorSeparation;
    }

    private void Subscribe<THandler>(THandler handler,
                                     Action<THandler> add,
                                     Action<THandler> remove)
        where THandler : Delegate
    {
        add(handler);
        _unsubscribes.Add(() => remove(handler));
    }

    private void UnsubscribeAll()
    {
        foreach (var u in _unsubscribes) u();
        _unsubscribes.Clear();
    }


    public override async void _Ready()
    {
        #if TOOLS
        // 注册自定义监视器
        Performance.AddCustomMonitor("EditorScene/SetChartTimeTimeUs", Callable.From(() => _setChartTimeTimeUs));
        Performance.AddCustomMonitor("EditorScene/LogicTimeUs", Callable.From(() => _logicTimeUs));
        Performance.AddCustomMonitor("EditorScene/RenderTimeUs", Callable.From(() => _renderTimeUs));
        Performance.AddCustomMonitor("EditorScene/UITimeUs", Callable.From(() => _uiTimeUs));
        Performance.AddCustomMonitor("EditorScene/DrawEditPanelTimeUs", Callable.From(() => _drawEditPanelTimeUs));
        #endif

        //获取节点引用
        _inputManager = GetNode<InputManager>("/root/InputManager");
        if(_inputManager == null)
        {
            GD.PrintErr($"[{this.Name}] inputManager is null");
        }
        _inputManager.IsEnable = true;
        
        _chartService = GetNode<ChartService>("/root/ChartService");
        if(_chartService == null)
        {
            GD.PrintErr($"[{this.Name}] ChartService is null");
        }

        _chartEditService = GetNode<ChartEditService>("/root/ChartEditService");
        if(_chartEditService == null)
        {
            GD.PrintErr($"[{this.Name}] ChartEditService is null");
        }

        _editorSettings = GetNode<EditorSettings>("/root/EditorSettings");
        if (_editorSettings == null)
        {
            GD.PrintErr($"[{this.Name}] EditorSettings is null");
        }
        else
        {
            // 监听编辑器设置变化事件
            _editorSettings.SettingChanged += OnEditorSettingChanged;
        }

        // 创建场景级状态中心，并把 Context / ChartEditService 注入各面板
        InitContext();

		//绑定事件
		_inputManager.Slide += (float x) =>
        {
            Slide(x * verMouseSensitivity);
        };
		_inputManager.Zoom += (float x) =>
        {
            Zoom(x * zoomMouseSensitivity);
        };

        ChartInfo chartInfo = null;
        string chartId = "";
        Image bgImage = null;
        Image bgImageBlurred = null;
        AudioStream audioStream = null;

        List<(string, Func<Task>)> tasks = [
            ("正在读取谱面...", async () => {
                //从global中同步数据
                var global = GetNode<Global>("/root/Global");
                chartId = global.editingChartId;

                // 谱面加载完成前先恢复该谱面的编辑器布局设置。
                _editorSettings.Load(chartId);

                // 设置正在编辑的铺面
                chartInfo = _chartService.GetChartInfo(chartId);

                // 这里 ChartLoader.LoadChart 可能涉及文件读取和 Json 解析，可以改为异步后台
                var sw = Stopwatch.StartNew();
                Chart chart = await Task.Run(() => ChartLoader.LoadChart(chartInfo.ChartPath));
                GD.Print($"[{Name}] 读取谱面用时:{sw.ElapsedMilliseconds} ms");

                // 谱面只在 EditorContext 中保存一份，面板通过 Context 读取
                _context.SetChart(chartId, chart);
                _chartEditService.EditingChart = chart;

                // 将持久化设置应用到所有使用同一网格的编辑面板。
                ApplyEditorSettings();
                
            }),
            ("正在加载资源包...", async () => {
                // ================ 加载资源包 ================
                await Task.Run(() => LoadResourcePack());
                
            }),
            ("正在加载背景和音乐...", async () => {
                // 背景图片
                // 异步加载图片
                (bgImage, string _) = await FileUtil.LoadImageFromFileAsync(chartInfo.PicturePath);
                // await Task.Run(() => bgImage = Image.LoadFromFile(chartInfo.PicturePath));
                if (bgImage == null)
                {
                    GD.PrintErr($"[{this.Name}] 背景图片导入失败: {chartInfo.PicturePath}");
                }
                
                // 加载模糊图片
                string blurredPath = Path.Combine("user://ChartSaves", chartId, $"img_blur_{BlurRadius}.png");
                string blurredPathAbs = ProjectSettings.GlobalizePath(blurredPath);

                if(!Godot.FileAccess.FileExists(blurredPathAbs))
                {
                    // 需要新建一个模糊图片
                    await ImageBlur.Instance.BlurPicture(bgImage, BlurRadius, blurredPathAbs);
                }
                (bgImageBlurred, _) = await FileUtil.LoadImageFromFileAsync(blurredPathAbs);
                if (bgImageBlurred == null)
                {
                    GD.PrintErr($"[{this.Name}] 模糊化背景图片导入失败: {blurredPath}");
                }

                // 音乐
                // 因为MP3文件时解压时动态生成的，所以需要使用 AudioStreamMP3.LoadFromFile 加载 MP3
                await Task.Run(() => audioStream = FileUtil.LoadAudioFromFile(chartInfo.SongPath));
                if (audioStream == null)
                {
                    GD.PrintErr($"[{this.Name}] 音乐文件加载失败: {chartInfo.SongPath}");
                }
            }),
            ("正在初始化谱面播放器...", async () => {
                // ================初始化谱面播放器================
                
                HitEffectPool hitEffectPool = new HitEffectPool();
                hitEffectPool.Name = "HitEffectPool";
                // 这一行需要保证已经设置过资源包
                hitEffectPool.Initialize(chartPlayParent, chartPlayer.HitFrames, 50);

                chartPlayer.Initialize(chartPlayParent, _context.EditingChart, bgImageBlurred, audioStream, hitEffectPool);
                chartRenderer.Initialize(chartPlayParent);

                chartPlayParent.ClipContents = true;

                _bgImageRect.Texture = ImageTexture.CreateFromImage(bgImageBlurred);
                _bgImageRect.SelfModulate = new Color(0.3f, 0.3f ,0.3f ,1);

                SetChartPlayerVisible(false); // 初始不显示
                
            }),
            ("正在初始化编辑器...", async () => {
                InitEditor();
            }),
        ];

        await LoadingManager.Instance.RunTasksAsync("正在进入编辑界面", tasks);
        
        GD.Print($"[{this.Name}] 初始化成功 谱面id:{editingChartId}");
        _isReady = true;
        
    }

    /// <summary>
    /// 创建场景级状态中心，并把依赖注入各面板。
    /// 必须在 _Ready 中第一个 await 之前完成，避免 _Process 提前运行。
    /// </summary>
    private void InitContext()
    {
        _context = new EditorContext { Name = "EditorContext" };
        AddChild(_context);

        // 把场景中导出的视图初值写入 Context
        _context.HorOffset = horOffset;
        _context.HorOffsetSmoothed = horOffset;
        _context.HorSeparation = horSeparation;
        _context.HorSeparationSmoothed = horSeparation;
        _context.GroundY = groundY;

        Subscribe(
            OnEditingLineChanged,
            h => _context.EditingLineChanged += h,
            h => _context.EditingLineChanged -= h);

        // 面板直接依赖 Context（数据）与 ChartEditService（命令）
        foreach (BaseEditPanel panel in new BaseEditPanel[] { noteEditPanel, eventEditPanel, bpmEditPanel })
        {
            panel.Initialize(_context, _chartEditService);
        }
    }

    private void OnEditingLineChanged(int lineId)
    {
        editingLineLabel.Text = $"线{lineId}";
    }

    private void InitEditor()
    {
        // 设置chooseLinePanel
        chooseLinePanel.Visible = false;
        chooseLinePanel.LineSelected += SetEditingLine;
        chooseLinePanel.AddLineRequested += AddLine;
        chooseLinePanel.DeleteLineRequested += DeleteLine;
        chooseLinePanel.RefreshRequested += RefreshChooseLinePanel;
        chooseLinePanel.LayerSelected += (int index) =>
        {
            if(index < 0 || index > 4)
            {
                GD.PrintErr($"[{Name}] EventLayer索引越界:{index}");
                return;
            }

            List<EventLayer> eventLayers = editingChart.JudgeLineList[EditingLineId].EventLayers;

            // 如果列表元素不够，用 null 填充到目标索引
            while (eventLayers.Count <= index)
            {
                eventLayers.Add(null);
            }

            if (eventLayers[index] == null)
            {
                eventLayers[index] = new();
            }

            EditingLayer = index;

            GD.Print($"[{Name}] 切换到事件层:{index}");
        };

        editingLineLabel.Text = $"线{0}";

        // 设置NoteEditPanel
        // 添加/删除/拖动等编辑操作由面板直接调用 ChartEditService，
        // 这里只保留「选择」相关事件（选择焦点、右键菜单仍由 EditorScene 协调）。
        noteEditPanel.OnNoteSelected += OnNoteSelected;
        Subscribe(
            OnNoteMultiSelected,
            h => noteEditPanel.NoteMultiSelected += h,
            h => noteEditPanel.NoteMultiSelected -= h);
        noteEditPanel.Disabled = false;

        // 设置eventEditPanel
        Subscribe(
            OnEventSelected,
            h => eventEditPanel.EventSelected += h,
            h => eventEditPanel.EventSelected -= h);
        Subscribe(
            OnEventMultiSelected,
            h => eventEditPanel.EventMultiSelected += h,
            h => eventEditPanel.EventMultiSelected -= h);
        eventEditPanel.Disabled = false;

        // 设置bpmEditPanel
        Subscribe(
            OnBpmSelected,
            h => bpmEditPanel.EventSelected += h,
            h => bpmEditPanel.EventSelected -= h);
        Subscribe(
            OnBpmMultiSelected,
            h => bpmEditPanel.BpmMultiSelected += h,
            h => bpmEditPanel.BpmMultiSelected -= h);
        bpmEditPanel.Disabled = false;

        SetEditPanelVisible(true); // 初始默认显示

        // 设置noteInfoPanel
        noteInfoPanel.OnConfirmed += () => noteInfoPanel.Visible = false;
        Subscribe(
            SetNoteProperty,
            h => noteInfoPanel.OnNotePropertyChanged += h,
            h => noteInfoPanel.OnNotePropertyChanged -= h);

        // 设置eventInfoPanel
        eventInfoPanel.OnConfirmed += () => eventInfoPanel.Visible = false;
        Subscribe(
            SetEventProperty,
            h => eventInfoPanel.PropertyChanged += h,
            h => eventInfoPanel.PropertyChanged -= h);

        //设置bpmInfoPanel
        bpmInfoPanel.OnConfirmed += () => bpmInfoPanel.Visible = false;
        Subscribe(
            SetBpmProperty,
            h => bpmInfoPanel.PropertyChanged += h,
            h => bpmInfoPanel.PropertyChanged -= h);

        //设置弹出菜单
        PopupMenuHelper.SetTheme(theme);

        //设置顶部菜单栏
        //设置“文件”选项
        {
            // 构建菜单项
            var items = new List<PopupMenuItem>
            {
                new PopupMenuItem { Text = "保存", Callback = SaveChart},
                //new PopupMenuItem { Text = "另存为", Callback = null},
                new PopupMenuItem { IsSeparator = true},
                new PopupMenuItem { Text = "保存并退出", Callback = SaveAndQuit},
                new PopupMenuItem { Text = "仅退出", Callback = OnQuitPressed},
            };
            PopupMenuHelper.Instance.SetMenuButton(fileMenuButtion, items);
        }
        //设置“编辑”选项
        {
            // 构建菜单项
            var items = new List<PopupMenuItem>
            {
                new PopupMenuItem { Text = "复制", Callback = null},
                new PopupMenuItem { Text = "粘贴", Callback = null},
                new PopupMenuItem { Text = "剪切", Callback = null},
                new PopupMenuItem { IsSeparator = true},
                new PopupMenuItem { Text = "全局设置", Callback = _settingsPanel.Show},
                new PopupMenuItem { Text = "编辑器设置", Callback = _editorSettingsPanel.Show},
            };
            PopupMenuHelper.Instance.SetMenuButton(editMenuButtion, items);
        }

        // 设置“视图”选项
        {
            // 构建菜单项
            var items = new List<PopupMenuItem>
            {
                new PopupMenuItem { Text = "音符面板", Checkable = true, 
                    Checked = noteEditPanel.Visible,
                    Toggled = (bool value) => noteEditPanel.Visible = value
                },
                new PopupMenuItem { Text = "事件面板", Checkable = true, 
                    Checked = eventEditPanel.Visible,
                    Toggled = (bool value) => eventEditPanel.Visible = value
                },
                new PopupMenuItem { Text = "Bpm面板", Checkable = true, 
                    Checked = bpmEditPanel.Visible,
                    Toggled = (bool value) => bpmEditPanel.Visible = value
                },
            };
            PopupMenuHelper.Instance.SetMenuButton(viewMenuButton, items);
        }

        // 设置左上角“...”按钮
        _othersButton.Pressed += () =>
        {
            // 构建菜单项
            var items = new List<PopupMenuItem>
            {
                new PopupMenuItem { Text = "试玩", Callback = OnTestPlay},
            };

            PopupMenuHelper.Instance.ShowPopupMenu(this, 
                GetViewport().GetMousePosition() + new Vector2(30, 30), 
                items);
        };

        //设置NoteChooser
        Subscribe(
            OnNoteChooserNoteChoosed,
            h => noteChooser.NoteChoosed += h,
            h => noteChooser.NoteChoosed -= h
        );

        Subscribe(
            OnNoteChooserDeselected,
            h => noteChooser.Deselected += h,
            h => noteChooser.Deselected -= h
        );
        // noteChooser.DeleteButtonChoosed += OnNoteChooserDeleteChoosed;

        // 设置_deleteBtn
        _deleteBtn.Visible = false; // 默认不显示
        Subscribe(
            OnDeletePressed,
            h => _deleteBtn.Pressed += h,
            h => _deleteBtn.Pressed -= h
        );

        //设置EditModeManager 初始状态默认为常规模式
        EditModeManager.SetEditMode(EditModeEnum.Normal);

        //设置editModeLabel
        editModeLabel.Text = "模式：常规模式";
        Subscribe(
            OnEditModeChanged,
            h => EditModeManager.OnEditModeChanged += h,
            h => EditModeManager.OnEditModeChanged -= h);

        // 设置PlayModeManager
        Subscribe(
            OnPlayModeChanged,
            h => PlayModeManager.PlayModeChanged += h,
            h => PlayModeManager.PlayModeChanged -= h);
        PlayModeManager.SetPlayMode(PlayModeEnum.Editing);

        GameSettings.Instance.SettingChanged += OnSettingsChanged;

        // 设置撤销重做按钮
        _undoBtn.Pressed += OnUndo;
        _redoBtn.Pressed += OnRedo;

        // 设置复制粘贴按钮
        _copyBtn.Pressed += OnCopyPressed;
        _pasteBtn.Pressed += OnPastePressed;
        _pasteConfirmBtn.Pressed += OnPasteConfirm;
        _pasteCancelBtn.Pressed += OnPasteCancelPressed;

        // 设置多选按钮
        _multiSelectBtn.ToggleMode = true;
        _multiSelectBtn.Toggled += (bool value) =>
        {
            BaseEditPanel.SelectModeEnum mode = value ? 
                BaseEditPanel.SelectModeEnum.Multi : BaseEditPanel.SelectModeEnum.Single;
            
            // 选择模式由 Context 统一持有，三个面板共享
            _context.SelectMode = mode;
        };

        // 设置框选按钮
        _boxSelectBtn.ToggleMode = true;
        _boxSelectBtn.Toggled += (bool value) =>
        {
            _context.IsBoxSelectMode = value;
        };

        // 统一设置面板取消选择的事件
        Subscribe(
            () => {IsSelecting = false;},
            h => noteEditPanel.AllDeselected += h,
            h => noteEditPanel.AllDeselected -= h
        );
        Subscribe(
            () => {IsSelecting = false;},
            h => eventEditPanel.AllDeselected += h,
            h => eventEditPanel.AllDeselected -= h
        );
        Subscribe(
            () => {IsSelecting = false;},
            h => bpmEditPanel.AllDeselected += h,
            h => bpmEditPanel.AllDeselected -= h
        );
        
    }

    public override void _Process(double delta)
    {
        if(!_isReady) return;

        #if TOOLS
        ulong t1 = Time.GetTicksUsec();
        #endif

        if (isPlaying)
        {
            //正在播放时，时间轴由音乐决定
            ChartTime = chartPlayer.ChartTime;
        }
        else
        {
            //否则，时间轴由编辑器面板决定
            chartPlayer.ExternalTime = ChartTime;
        }

        #if TOOLS
        ulong t2 = Time.GetTicksUsec();
        #endif
        
        chartPlayer.UpdateLogic(delta);

        #if TOOLS
        ulong t3 = Time.GetTicksUsec();
        #endif

        if(!chartPlayer.Disabled && !chartRenderer.Disabled)
        {
            (JudgeLineRenderData[] lineData, int lineCount) = chartPlayer.GetLineRenderDatas();
            (NoteRenderData[] noteData, int noteCount) = chartPlayer.GetNoteRenderDatas();

            chartRenderer.Render(lineData, lineCount, noteData, noteCount);
        }

        #if TOOLS
        ulong t4 = Time.GetTicksUsec();
        #endif

        //处理摇杆垂直滚动
		if(slideJoystick.Output != Vector2.Zero)
		{
            if(PlayModeManager.PlayMode == PlayModeEnum.PlayerPause)
            {
                SlideTime(-slideJoystick.Output.Y * verJoystickTimeSens * (float)delta);
            }
            else
            {
                Slide(-slideJoystick.Output.Y * verJoystickSensitivity * (float)delta);
            }
			// horOffset -= ;
			// //限制不能滚动到0以下
			// if(horOffset < 0) horOffset = 0;

			// BeatValue = horOffset / horSeparation;
            // GD.Print($"output:{slideJoystick.Output.Y}");
		}

		//处理摇杆缩放
		if(zoomJoystick.Output != Vector2.Zero)
		{
			Zoom(zoomJoystick.Output.Y * zoomJoystickSensitivity * (float)delta);
		}

		//平滑竖直滚动
        if(Math.Abs(_context.HorOffset - _context.HorOffsetSmoothed) > 0.001f)
		{
			_context.HorOffsetSmoothed += (_context.HorOffset - _context.HorOffsetSmoothed) * (float)delta * 18f;
		}
        else
        {
            _context.HorOffsetSmoothed = _context.HorOffset;
        }

		//平滑竖直缩放
		if(Math.Abs(_context.HorSeparation - _context.HorSeparationSmoothed) > 0.001f)
		{
            _context.HorSeparationSmoothed += (_context.HorSeparation - _context.HorSeparationSmoothed) * (float)delta * 18f;
		}
        else
        {
            _context.HorSeparationSmoothed = _context.HorSeparation;
        }

        #if TOOLS
        ulong t5 = Time.GetTicksUsec();
        #endif

        //同步编辑面板（视图状态统一从 EditorContext 读取，无需逐个字段推送）
        noteEditPanel.UpdateVisuals();
        eventEditPanel.UpdateVisuals();
        bpmEditPanel.UpdateVisuals();

        #if TOOLS
        ulong t6 = Time.GetTicksUsec();

        _setChartTimeTimeUs = t2 - t1;
        _logicTimeUs = t3 - t2;
        _renderTimeUs = t4 - t3;
        _uiTimeUs = t5 - t4;
        _drawEditPanelTimeUs = t6 - t5;
        #endif

    }

    private int fpsRefreshCount = 0;
    public override void _PhysicsProcess(double delta)
    {
        //GD.Print($"ChartTime:{ChartTime}, BeatValue:{BeatValue}, horOffset:{horOffset}");
        fpsRefreshCount++;
        if(fpsRefreshCount > 15)
        {
            fpsRefreshCount = 0;
            fpsLabel.Text = $"FPS:{Performance.GetMonitor(Performance.Monitor.TimeFps)}";
        }
        
        // GD.Print($"BeatValue:{BeatValue}, ChartTime:{ChartTime}, bpm:{editingChart.BpmList[0].Bpm}");
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        // if (_editorSettings != null)
        // {
        //     _editorSettings.SettingChanged -= OnEditorSettingChanged;
        // }

        #if TOOLS
        // 取消注册自定义监视器 小心lambda诡异的生命周期问题
        Performance.RemoveCustomMonitor("EditorScene/SetChartTimeTimeUs");
        Performance.RemoveCustomMonitor("EditorScene/LogicTimeUs");
        Performance.RemoveCustomMonitor("EditorScene/RenderTimeUs");
        Performance.RemoveCustomMonitor("EditorScene/UITimeUs");
        Performance.RemoveCustomMonitor("EditorScene/DrawEditPanelTimeUs");
        #endif

        // 取消订阅所有事件
        UnsubscribeAll();

        GD.Print($"[{Name}] 成功退出EditorScene");
        
    }
    
    private void OnEditModeChanged(EditModeEnum editMode)
    {
        editModeLabel.Text = editMode switch
        {
            EditModeEnum.Normal => "模式：常规模式",
            EditModeEnum.Place => "模式：放置模式",
            // EditModeEnum.Delete => "模式：删除模式",
            _ => "模式：未知",
        };
    }

    private void SaveChart()
    {
        _chartService.SaveChart(editingChartId, editingChart);
        // 谱面和编辑器视图设置一起保存，避免退出后丢失网格状态。
        SaveEditorSettings();
        // TODO 保存成功后弹出Toast提示
    }

    private void Quit()
    {
        SaveEditorSettings();
        var global = GetNode<Global>("/root/Global");
        global.editingChartId = "";
        global.GotoScene("res://Scene/start_menu.tscn");
    }

    #region 编辑器设置
    private void ApplyGridAppearanceSettings()
    {
        if (GameSettings.Instance == null || GameSettings.Instance.Current == null)
        {
            return;
        }

        SettingsData settings = GameSettings.Instance.Current;

        noteEditPanel.HorColor = settings.HorColor;
        noteEditPanel.HorWidth = settings.HorWidth;
        noteEditPanel.HorSubColor = settings.HorSubColor;
        noteEditPanel.HorSubWidth = settings.HorSubWidth;
        noteEditPanel.VerColor = settings.VerColor;
        noteEditPanel.VerWidth = settings.VerWidth;
        noteEditPanel.GroundLineColor = settings.GroundLineColor;
        noteEditPanel.GroundLineWidth = settings.GroundLineWidth;

        eventEditPanel.HorColor = settings.HorColor;
        eventEditPanel.HorWidth = settings.HorWidth;
        eventEditPanel.HorSubColor = settings.HorSubColor;
        eventEditPanel.HorSubWidth = settings.HorSubWidth;
        eventEditPanel.VerColor = settings.VerColor;
        eventEditPanel.VerWidth = settings.VerWidth;
        eventEditPanel.GroundLineColor = settings.GroundLineColor;
        eventEditPanel.GroundLineWidth = settings.GroundLineWidth;

        bpmEditPanel.HorColor = settings.HorColor;
        bpmEditPanel.HorWidth = settings.HorWidth;
        bpmEditPanel.HorSubColor = settings.HorSubColor;
        bpmEditPanel.HorSubWidth = settings.HorSubWidth;
        bpmEditPanel.VerColor = settings.VerColor;
        bpmEditPanel.VerWidth = settings.VerWidth;
        bpmEditPanel.GroundLineColor = settings.GroundLineColor;
        bpmEditPanel.GroundLineWidth = settings.GroundLineWidth;
    }

    private void ApplyEditorSettings()
    {
        if(_editorSettings == null)
        {
            GD.PrintErr($"[{this.Name}] EditorSettings is null");
            return;
        }
        
        int verLineCount = _editorSettings.Current.VerLineCount;
        int subBeatCount = _editorSettings.Current.SubBeatCount;

        noteEditPanel.VerLineCount = verLineCount;
        noteEditPanel.SubBeatCount = subBeatCount;
        
        eventEditPanel.SubBeatCount = subBeatCount;
        
        bpmEditPanel.SubBeatCount = subBeatCount;

        ApplyGridAppearanceSettings();
    }

    private void OnEditorSettingChanged(string key, Variant value)
    {
        if(_editorSettings == null)
        {
            GD.PrintErr($"[{this.Name}] EditorSettings is null");
            return;
        }

        switch (key)
        {
            case nameof(EditorSettingsData.VerLineCount):
                int verLineCount = _editorSettings.Current.VerLineCount;
                noteEditPanel.VerLineCount = verLineCount;
                break;
            
            case nameof(EditorSettingsData.SubBeatCount):
                int subBeatCount = _editorSettings.Current.SubBeatCount;
                noteEditPanel.SubBeatCount = subBeatCount;
                eventEditPanel.SubBeatCount = subBeatCount;
                bpmEditPanel.SubBeatCount = subBeatCount;
                break;
            
            default:
                GD.PrintErr($"[{this.Name}] 未知的EditorSettings设置项:{key}");
                ApplyEditorSettings();
                break;
        }
    }

    private void SaveEditorSettings()
    {
        if (_editorSettings == null || string.IsNullOrEmpty(editingChartId)) return;

        _editorSettings.Save();
    }

    private void OnSettingsChanged(string key, Variant value)
    {
        if (key == nameof(SettingsData.ResourcePackId) || key == nameof(SettingsData.UseDefaultResource))
        {
            LoadResourcePack();
            return;
        }

        if (key == nameof(SettingsData.HorColor)
            || key == nameof(SettingsData.HorWidth)
            || key == nameof(SettingsData.HorSubColor)
            || key == nameof(SettingsData.HorSubWidth)
            || key == nameof(SettingsData.VerColor)
            || key == nameof(SettingsData.VerWidth)
            || key == nameof(SettingsData.GroundLineColor)
            || key == nameof(SettingsData.GroundLineWidth))
        {
            ApplyGridAppearanceSettings();
        }
    }

    #endregion

    private void OnQuitPressed()
    {
        PopupHelper.Instance.ShowConfirm(
            "警告",
            "谱面信息将会丢失，是否仍要退出？",
            Quit,
            null);
    }

    private void SaveAndQuit()
    {
        SaveChart();
        Quit();
    }

    private void LoadResourcePack()
    {
        bool useDefault = GameSettings.Instance.Get<bool>(nameof(SettingsData.UseDefaultResource));
        if (useDefault)
        {
            chartPlayer.UseDefaultResource();
            chartRenderer.UseDefaultResource();
        }
        else
        {
            string id = GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId));
            _resourcePack = ResourcePackLoader.LoadFromLocal(id);
            if(_resourcePack == null)
            {
                GD.PrintErr($"[{Name}] 加载资源包失败, id:{id}");
            }
            chartPlayer.Pack = _resourcePack;
            chartRenderer.Pack = _resourcePack;
        }

        GD.Print($"[{Name}] 成功重新加载资源包!");
    }

    private void OnTestPlay()
    {
        var global = GetNode<Global>("/root/Global");
        global.GotoScene("res://Scene/play_scene.tscn");
    }

    private void OnUndo()
    {
        _chartEditService.Undo();
    }

    private void OnRedo()
    {
        _chartEditService.Redo();
    }

    private void OnCopyPressed()
    {
        switch (_selectFocusPanel)
        {
            case EditPanelType.NoteEdit:

                if(noteEditPanel.SelectedNotes == null || noteEditPanel.SelectedNotes.Count == 0) break;
                
                _editorClipboard.noteClipBoard.SourceLineId = noteEditPanel.EditingLineId;
                _editorClipboard.noteClipBoard.SourceStartBeat = new Beat(noteEditPanel.SelectedNotes.First().StartTime);
                _editorClipboard.noteClipBoard.SourcePosX = noteEditPanel.SelectedNotes.First().PositionX;
                _editorClipboard.noteClipBoard.Notes.Clear();

                foreach(Note note in noteEditPanel.SelectedNotes)
                {
                    _editorClipboard.noteClipBoard.Notes.Add(NoteSnapshot.Capture(note));
                }

                _editorClipboard.LatestClipBoard = EditPanelType.NoteEdit;

                GD.Print($"[{Name}] 成功复制{noteEditPanel.SelectedNotes.Count}个Note");
                break;

            case EditPanelType.LineEventEdit:
                if (eventEditPanel.SelectedEventsWithType == null || eventEditPanel.SelectedEventsWithType.Count == 0)
                    break;

                var eventList = eventEditPanel.SelectedEventsWithType;

                ValueTuple<LineEventEnum, LineEvent> earliestEvent = eventList
                    .OrderBy((ValueTuple<LineEventEnum, LineEvent> kvp) => kvp.Item2.StartTime[0] + kvp.Item2.StartTime[1] * 1f / kvp.Item2.StartTime[2])
                    .First();

                _editorClipboard.lineEventClipBoard = new LineEventClipBoard
                {
                    SourceLineId = EditingLineId,
                    SourceLayer = EditingLayer,
                    SourceStartBeat = new Beat(earliestEvent.Item2.StartTime),
                    Events = new List<LineEventClipBoardItem>()
                };
                
                // 将事件添加到剪切板
                foreach ((LineEventEnum type, LineEvent evt) in eventList)
                {
                    _editorClipboard.lineEventClipBoard.Events.Add(
                        new LineEventClipBoardItem(type, LineEventSnapshot.Capture(evt)));
                }

                _editorClipboard.LatestClipBoard = EditPanelType.LineEventEdit;
                GD.Print($"[{Name}] 成功复制{eventList.Count}个Event");
                break;

            case EditPanelType.BpmEventEdit:
                IReadOnlyCollection<BpmEvent> selectedBpms = bpmEditPanel.SelectedEvents;
                if (selectedBpms == null || selectedBpms.Count == 0)
                    break;

                BpmEvent earliestBpm = selectedBpms
                    .OrderBy(bpm => bpm.StartTime[0] + bpm.StartTime[1] * 1f / bpm.StartTime[2])
                    .First();

                _editorClipboard.bpmEventClipBoard = new BpmEventClipBoard
                {
                    SourceStartBeat = new Beat(earliestBpm.StartTime),
                    Bpms = new List<BpmEventSnapshot>()
                };

                foreach (BpmEvent bpmEvent in selectedBpms)
                {
                    _editorClipboard.bpmEventClipBoard.Bpms.Add(BpmEventSnapshot.Capture(bpmEvent));
                }

                _editorClipboard.LatestClipBoard = EditPanelType.BpmEventEdit;
                GD.Print($"[{Name}] 成功复制{selectedBpms.Count}个BPM事件");
                break;
            default:
                break;
        }
    }

    private void OnPastePressed()
    {
        bool isSuccess = true;
        switch (_editorClipboard.LatestClipBoard)
        {
            case EditPanelType.NoteEdit:
                noteEditPanel.StartPaste(_editorClipboard.noteClipBoard);
                _pasteFocusPanel = EditPanelType.NoteEdit;
                break;
            case EditPanelType.LineEventEdit:
                eventEditPanel.StartPaste(_editorClipboard.lineEventClipBoard);
                _pasteFocusPanel = EditPanelType.LineEventEdit;
                break;
            case EditPanelType.BpmEventEdit:
                if (_editorClipboard.bpmEventClipBoard?.Bpms == null || _editorClipboard.bpmEventClipBoard.Bpms.Count == 0)
                {
                    isSuccess = false;
                    break;
                }

                bpmEditPanel.StartPaste(_editorClipboard.bpmEventClipBoard);
                _pasteFocusPanel = EditPanelType.BpmEventEdit;
                break;
            default:
                isSuccess = false;
                break;
        }

        if (isSuccess)
        {
            SetPasteApplyButtonVisibility(true);
        }
    }

    private void OnPasteConfirm()
    {
        SetPasteApplyButtonVisibility(false);

        // 应用粘贴
        switch (_pasteFocusPanel)
        {
            case EditPanelType.NoteEdit:
                noteEditPanel.ExitPasteMode();
                // 执行粘贴
                _chartEditService.PasteNotes(
                    _editorClipboard.noteClipBoard,
                    EditingLineId,
                    noteEditPanel.PasteTargetBeat,
                    noteEditPanel.PasteTargetPosX
                );
                break;

            case EditPanelType.LineEventEdit:
                eventEditPanel.ExitPasteMode();
                _chartEditService.PasteEvents(
                    _editorClipboard.lineEventClipBoard,
                    EditingLineId,
                    EditingLayer,
                    eventEditPanel.PasteTargetBeat
                );
                break;

            case EditPanelType.BpmEventEdit:
                bpmEditPanel.ExitPasteMode();
                if (_editorClipboard.bpmEventClipBoard != null && _editorClipboard.bpmEventClipBoard.Bpms.Count > 0)
                {
                    _chartEditService.PasteBpmEvents(_editorClipboard.bpmEventClipBoard, bpmEditPanel.PasteTargetBeat);
                }
                break;
            default:
                break;
        }
    }

    private void OnPasteCancelPressed()
    {
        SetPasteApplyButtonVisibility(false);

        noteEditPanel.ExitPasteMode();
        eventEditPanel.ExitPasteMode();
        bpmEditPanel.ExitPasteMode();
    }

    private void SetPasteApplyButtonVisibility(bool value)
    {
        _pasteBtn.Visible = !value;
        _copyBtn.Visible = !value;

        _pasteConfirmBtn.Visible = value;
        _pasteCancelBtn.Visible = value;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            bool ctrl = key.CtrlPressed || key.MetaPressed;

            if (ctrl && key.Keycode == Key.Z)
            {
                if (key.ShiftPressed) _chartEditService.Redo();
                else                  _chartEditService.Undo();
                GetViewport().SetInputAsHandled();
            }
            else if (ctrl && key.Keycode == Key.Y)
            {
                _chartEditService.Redo();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// 当删除按钮被按下时调用
    /// </summary>
    private void OnDeletePressed()
    {
        bool isSuccess = true;

        // 由于删除按钮只有一个，需要判断当前选中的对象位于哪个面板
        switch (_selectFocusPanel)
        {
            case EditPanelType.NoteEdit:
                if(noteEditPanel.SelectedNotes != null && noteEditPanel.SelectedNotes.Count != 0)
                {
                    _chartEditService.DeleteNotes(EditingLineId, noteEditPanel.SelectedNotes);
                }
                break;

            case EditPanelType.LineEventEdit:
                if(eventEditPanel.SelectedEventsWithType != null && 
                    eventEditPanel.SelectedEventsWithType.Count != 0)
                {
                    List<(LineEventEnum Type, LineEvent Evt)> eventsToDelete = eventEditPanel.SelectedEventsWithType
                        .ToList();
                    _chartEditService.DeleteEvents(EditingLineId, EditingLayer, eventsToDelete);
                }
                break;

            case EditPanelType.BpmEventEdit:
                if(bpmEditPanel.SelectedEvents != null && bpmEditPanel.SelectedEvents.Count != 0)
                {
                    _chartEditService.DeleteBpms(bpmEditPanel.SelectedEvents.ToList());
                }
                break;
                
            default:
                GD.PrintErr($"[{this.Name}] 未知的选中面板类型:{_selectFocusPanel}");
                isSuccess = false;
                break;
        }

        if (isSuccess)
        {
            IsSelecting = false;
            noteEditPanel.DeselectAll();
            eventEditPanel.DeselectAll();
            bpmEditPanel.DeselectAll();

        }
    }

    private void OnMultiSelectPressed()
    {
        
    }


    #region 播放控制

    private void SetEditPanelVisible(bool value)
    {
        editPanel.Visible = value;
        noteEditPanel.Disabled = !value;
        eventEditPanel.Disabled = !value;
        bpmEditPanel.Disabled = !value;
    }

    private void SetChartPlayerVisible(bool value)
    {
        chartPlayParent.Visible = value;
        chartPlayer.Disabled = !value;
        chartRenderer.Disabled = !value;
    }

    private void SetIsPlaying(bool value)
    {
        isPlaying = value;
        if(value) chartPlayer.Play((float)ChartTime);
        else chartPlayer.Pause();
    }

    public void OnPlayButtonClicked()
    {
        // 切换播放模式
        PlayModeManager.SetPlayMode(PlayModeEnum.PlayerPlaying);

        // // 开始播放
        // chartPlayer.Play((float)ChartTime);
        // chartPlayer.IsPlaying = true;
        // isPlaying = true;

        // //更新右侧面板
        // rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.AutoPlay);
    }

    public void PlayInEditPanel()
    {
        if(PlayModeManager.PlayMode == PlayModeEnum.EditorPlaying)
        {
            PlayModeManager.SetPlayMode(PlayModeEnum.Editing);
        }
        else
        {
            // 切换播放模式
            PlayModeManager.SetPlayMode(PlayModeEnum.EditorPlaying);
        }
        

        // // 开始播放
        // chartPlayer.Play((float)ChartTime);
        // chartPlayer.IsPlaying = true;
        // isPlaying = true;

        //更新右侧面板
        // rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.AutoPlay);
    }

    public void OnStopButtonClicked()
    {
        // 切换播放模式
        PlayModeManager.SetPlayMode(PlayModeEnum.Editing);

        // // 暂停播放
        // chartPlayer.Pause();
        // chartPlayer.IsPlaying = false;
        // isPlaying = false;

        //更新右侧面板
        // rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Normal);
    }

    public void OnPauseClicked()
    {
        // 切换播放模式
        PlayModeManager.SetPlayMode(PlayModeEnum.PlayerPause);

        // // 暂停播放
        // chartPlayer.IsPlaying = false;
        // chartPlayer.Pause();
        // isPlaying = false;

        //更新右侧面板
        // rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Pause);

    }

    private void OnPlayModeChanged(PlayModeEnum playMode)
    {
        switch (playMode)
        {
            case PlayModeEnum.Editing:
                SetChartPlayerVisible(false);
                SetEditPanelVisible(true);
                SetIsPlaying(false);
                rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Normal);
                break;

            case PlayModeEnum.PlayerPlaying:
                SetChartPlayerVisible(true);
                SetEditPanelVisible(false);
                SetIsPlaying(true);
                rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.AutoPlay);
                break;

            case PlayModeEnum.PlayerPause:
                SetChartPlayerVisible(true);
                SetEditPanelVisible(false);
                SetIsPlaying(false);
                rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Pause);
                break;
            case PlayModeEnum.EditorPlaying:
                SetChartPlayerVisible(false);
                SetEditPanelVisible(true);
                SetIsPlaying(true);
                rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Normal);
                break;
            case PlayModeEnum.EditorAndPlayerPlaying:
                SetChartPlayerVisible(true);
                SetEditPanelVisible(true);
                SetIsPlaying(true);
                // TODO
                break;
        }
    }

    #endregion

    #region JudgeLine相关方法

    private void OnChooseLineClicked()
    {
        if(chooseLinePanel.Visible == false)
        {
            chooseLinePanel.Visible = true;
            _inputManager.IsEnable = false;

            RefreshChooseLinePanel();
            chooseLinePanel.SetEventLayer(EditingLayer);
        }
        else
        {
            chooseLinePanel.Visible = false;
            _inputManager.IsEnable = true;
        }
    }

    private void RefreshChooseLinePanel()
    {
        //准备LineInfo数据
        List<ChooseLinePanel.LineInfo> lineInfos = new();
        for (int i = 0; i < editingChart.JudgeLineList.Count; i++)
        {
            JudgeLine line = editingChart.JudgeLineList[i];

            lineInfos.Add(new ChooseLinePanel.LineInfo
            {
                Id = i, // 判定线的编号从0开始
                NoteCount = line.NumOfNotes,
                //NextEventTime = //TODO 在ChooseLinePanel显示下一个事件的时间
            });
        }

        //设置LineInfo数据
        chooseLinePanel.ShowInfos(lineInfos);
    }

    private void SetEditingLine(int id)
    {
        GD.Print($"[{this.Name}] 用户选择了Line:{id}");

        // 判定线只写入 Context，各面板通过 Context 读取；标签由 EditingLineChanged 统一更新
        EditingLineId = id;

        chooseLinePanel.Visible = false;
        _inputManager.IsEnable = true;
    }

    private void AddLine()
    {
        _chartEditService.AddLine(editingChart.JudgeLineList, -1);
    }

    private void DeleteLine(int id)
    {
        if(editingChart.JudgeLineList.Count <= 1)
        {
            GD.Print($"[{this.Name}] 最少保留一条判定线，删除失败");
            PopupHelper.Instance.ShowAlert("警告", "最少保留一条判定线，删除失败");
            return;
        }
        _chartEditService.DeleteLine(editingChart.JudgeLineList, id);
    }

    #endregion

    #region Note相关方法

    private void SetNoteProperty(int lineId, int noteIndex, NotePropertyEnum property, object value)
    {
        _chartEditService.SetNoteProperty(lineId, noteIndex, property, value);
    }

    private void OnNoteSelected(int lineId, int noteIndex, Vector2 popupViewportPos)
    {
        SelectFocusPanel = EditPanelType.NoteEdit;
        IsSelecting = true;

        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];

        float beatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
        //Vector2 popupPos = noteEditPanel.GetScreenPosition(beatValue, note.PositionX)
        //    + new Vector2(30,30);

        // 构建菜单项（使用闭包捕获当前音符信息）
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnNoteEdit(lineId, noteIndex) },
            new PopupMenuItem { Text = "复制", Callback = () => OnNoteCopy(lineId, noteIndex) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnNoteDelete(lineId, noteIndex) }
        };

        // 弹出菜单
        PopupMenu popupMenu = PopupMenuHelper.Instance.ShowPopupMenu(this, popupViewportPos, items);
        // popupMenu.PopupHide += () =>
        // {
        //     noteEditPanel.DeselectAll();
        //     IsSelecting = false;
        // };
    }

    private void OnNoteEdit(int lineId, int noteIndex)
    {
        noteInfoPanel.Visible = true;
        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];
        noteInfoPanel.ShowInfo(note, lineId, noteIndex);
    }

    private void OnNoteCopy(int lineId, int noteIndex)
    {
        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];

        _editorClipboard.noteClipBoard.Notes = [NoteSnapshot.Capture(note)];
        _editorClipboard.noteClipBoard.SourceLineId = lineId;
        _editorClipboard.noteClipBoard.SourceStartBeat = new Beat(note.StartTime);
        _editorClipboard.noteClipBoard.SourcePosX = note.PositionX;

        _editorClipboard.LatestClipBoard = EditPanelType.NoteEdit;

        GD.Print($"[{Name}] 复制Note: Line{lineId}_{noteIndex} {(NoteType)note.Type}");
    }

    private void OnNoteDelete(int lineId, int noteIndex)
    {
        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];
        _chartEditService.DeleteNote(lineId, note);
    }

    private void OnNoteMultiSelected()
    {
        SelectFocusPanel = EditPanelType.NoteEdit;
        IsSelecting = true;
    }

    private void OnNoteChooserDeselected()
    {
        EditModeManager.SetEditMode(EditModeEnum.Normal);
        // GD.Print($"[{this.Name}] 用户取消选择了note");
    }

    private void OnNoteChooserNoteChoosed(NoteType noteType)
    {
        EditModeManager.SetEditMode(EditModeEnum.Place);
        noteEditPanel.PlacingNote = noteType;
    }

    #endregion

    #region LineEvent相关方法

    private void OnEventSelected(int lineId, int layer, LineEventEnum lineEventEnum, int eventIndex, Vector2 popupViewportPos)
    {
        SelectFocusPanel = EditPanelType.LineEventEdit;
        IsSelecting = true;
        
        EventLayer eventLayer = editingChart.JudgeLineList[EditingLineId].EventLayers[layer];
		LineEvent lineEvent = eventLayer.GetLineEvents(lineEventEnum)[eventIndex];

        // 构建菜单项（使用闭包捕获当前音符信息）
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnEventEdit(lineId, EditingLayer, lineEventEnum, eventIndex) },
            new PopupMenuItem { Text = "复制", Callback = () => OnEventCopy(lineId, lineEventEnum, eventIndex) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnEventDelete(lineId, lineEventEnum, eventIndex) }
        };

        // 弹出菜单
        PopupMenu popupMenu = PopupMenuHelper.Instance.ShowPopupMenu(this, popupViewportPos, items);
        // popupMenu.PopupHide += () =>
        // {
        //     eventEditPanel.DeselectAll();
        //     IsSelecting = false;
        // };
    }

    private void OnEventEdit(int lineId, int layer, LineEventEnum lineEventEnum, int index)
    {
        GD.Print($"[{this.Name}] 编辑事件 line:{lineId}, type:{lineEventEnum}, index:{index}");
        eventInfoPanel.Visible = true;
        eventEditPanel.DeselectAll();
        IsSelecting = false;

        LineEvent lineEvent = editingChart.JudgeLineList[lineId].EventLayers[layer].GetLineEvents(lineEventEnum)[index];

        eventInfoPanel.Edit(lineEvent, lineId, layer, lineEventEnum, index);
    }

    private void SetEventProperty(
        int lineId, int layer, LineEventEnum lineEventEnum, int index,
        LineEventPropertyType propertyType, object value)
    {
        _chartEditService.SetEventProperty(lineId, layer, lineEventEnum, index, propertyType, value);
    }

    private void OnEventCopy(int lineId, LineEventEnum lineEventEnum, int index)
    {
        LineEvent lineEvent = editingChart.JudgeLineList[lineId].EventLayers[EditingLayer].GetLineEvents(lineEventEnum)[index];

        _editorClipboard.lineEventClipBoard = new LineEventClipBoard
        {
            SourceLineId = lineId,
            SourceLayer = EditingLayer,
            SourceStartBeat = new Beat(lineEvent.StartTime),
            Events = [ new LineEventClipBoardItem(lineEventEnum, LineEventSnapshot.Capture(lineEvent)) ]
        };

        _editorClipboard.LatestClipBoard = EditPanelType.LineEventEdit;
        GD.Print($"[{this.Name}] 复制事件 line:{lineId}, type:{lineEventEnum}, index:{index}");
    }

    private void OnEventDelete(int lineId, LineEventEnum lineEventEnum, int index)
    {
        _chartEditService.DeleteEvent(lineId, EditingLayer, lineEventEnum, index);

    }

    private void OnEventMultiSelected()
    {
        SelectFocusPanel = EditPanelType.LineEventEdit;
        IsSelecting = true;
    }

    #endregion

    #region Bpm相关方法

    private void OnBpmSelected(int index, Vector2 popupViewportPos)
    {
        if (editingChart?.BpmList == null || index < 0 || index >= editingChart.BpmList.Count)
        {
            return;
        }

        SelectFocusPanel = EditPanelType.BpmEventEdit;
        IsSelecting = true;

        BpmEvent bpmEvent = editingChart.BpmList[index];
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnBpmEdit(bpmEvent) },
            new PopupMenuItem { Text = "复制", Callback = () => OnBpmCopy(bpmEvent) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnBpmDelete(bpmEvent) }
        };

        PopupMenu popupMenu = PopupMenuHelper.Instance.ShowPopupMenu(this, popupViewportPos, items);
        // popupMenu.PopupHide += () => {
        //     bpmEditPanel.DeselectAll();
        //     IsSelecting = false;
        // };
    }

    private void OnBpmMultiSelected()
    {
        SelectFocusPanel = EditPanelType.BpmEventEdit;
        IsSelecting = true;
    }

    private void OnBpmEdit(BpmEvent bpmEvent)
    {
        if (bpmEvent == null || !editingChart.BpmList.Contains(bpmEvent))
        {
            return;
        }

        bpmEditPanel.DeselectAll();
        IsSelecting = false;

        bpmInfoPanel.Visible = true;
        bpmInfoPanel.Edit(bpmEvent, editingChart.BpmList.IndexOf(bpmEvent));
    }

    private void SetBpmProperty(BpmEvent bpmEvent, string property, object value)
    {
        _chartEditService.SetBpmProperty(bpmEvent, property, value);
    }

    private void OnBpmCopy(BpmEvent bpmEvent)
    {
        _editorClipboard.bpmEventClipBoard = new BpmEventClipBoard
        {
            SourceStartBeat = new Beat(bpmEvent.StartTime),
            Bpms = [ BpmEventSnapshot.Capture(bpmEvent) ]
        };
        _editorClipboard.LatestClipBoard = EditPanelType.BpmEventEdit;
        GD.Print($"[{Name}] 复制 BPM:{bpmEvent?.Bpm}");
    }

    private void OnBpmDelete(BpmEvent bpmEvent)
    {
        _chartEditService.DeleteBpms(new List<BpmEvent> { bpmEvent });
        bpmEditPanel.DeselectAll();
        IsSelecting = false;
    }

    #endregion

}
