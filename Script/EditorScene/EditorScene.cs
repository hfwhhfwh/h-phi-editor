using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

    // ---- 场景级控制器（EditorScene 只负责组装与生命周期）----
    private EditorInputController _inputController;
    private EditorPlaybackController _playbackController;
    private EditorClipboardController _clipboardController;
    private EditorSelectionController _selectionController;

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

    private readonly List<Action> _unsubscribes = new();

    private const float BlurRadius = 150;

    #if TOOLS
    // ---- 性能分析 ----
    private double _panelSyncTimeUs = 0;

    #endif

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
        Performance.AddCustomMonitor("EditorScene/PanelSyncUs", Callable.From(() => _panelSyncTimeUs));
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

        // 创建场景级状态中心(EditorContext)，并把 Context / ChartEditService 注入各面板
        InitContext();

        // 创建各场景级控制器（输入、播放、剪贴板、选择）
        InitControllers();

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

                _playbackController.SetPlayerVisible(false); // 初始不显示
                
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
    /// 创建场景级状态中心(EditorContext)，并把依赖注入各面板。
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

    /// <summary>
    /// 创建场景级控制器并把节点引用注入进去。
    /// EditorScene 只做组装，具体行为由各控制器负责；
    /// 控制器作为子节点存在，便于在远程场景树里观察，生命周期随场景一起结束。
    /// </summary>
    private void InitControllers()
    {
        BaseEditPanel[] editPanels = [noteEditPanel, eventEditPanel, bpmEditPanel];

        // ---- 输入：摇杆、滚轮、快捷键 ----
        _inputController = new EditorInputController { Name = "EditorInputController" };
        AddChild(_inputController);
        _inputController.Initialize(
            _context, _inputManager, _chartEditService,
            slideJoystick, zoomJoystick,
            verMouseSensitivity, zoomMouseSensitivity,
            verJoystickSensitivity, zoomJoystickSensitivity, verJoystickTimeSens);

        // ---- 播放：播放/暂停/停止、模式切换、每帧逻辑与渲染 ----
        _playbackController = new EditorPlaybackController { Name = "EditorPlaybackController" };
        AddChild(_playbackController);
        _playbackController.Initialize(
            _context, chartPlayer, chartRenderer, chartPlayParent,
            editPanel, editPanels, rightPanel);

        // ---- 剪贴板：复制/粘贴/粘贴确认 ----
        _clipboardController = new EditorClipboardController { Name = "EditorClipboardController" };
        AddChild(_clipboardController);
        _clipboardController.Initialize(
            _context, _chartEditService,
            noteEditPanel, eventEditPanel, bpmEditPanel,
            _copyBtn, _pasteBtn, _pasteConfirmBtn, _pasteCancelBtn);

        // ---- 选择：选择焦点、右键菜单、删除按钮 ----
        _selectionController = new EditorSelectionController { Name = "EditorSelectionController" };
        AddChild(_selectionController);
        _selectionController.Initialize(
            _context, _chartEditService, _clipboardController,
            noteEditPanel, eventEditPanel, bpmEditPanel,
            noteInfoPanel, eventInfoPanel, bpmInfoPanel,
            _deleteBtn);
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

        // 设置NoteEditPanel / EventEditPanel / BpmEditPanel 的初始可用状态
        // 添加/删除/拖动等编辑操作由面板直接调用 ChartEditService；
        // 选择焦点、右键菜单、删除按钮由 EditorSelectionController 负责。
        noteEditPanel.Disabled = false;
        eventEditPanel.Disabled = false;
        bpmEditPanel.Disabled = false;

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

        //设置EditModeManager 初始状态默认为常规模式
        EditModeManager.SetEditMode(EditModeEnum.Normal);

        //设置editModeLabel
        editModeLabel.Text = "模式：常规模式";
        Subscribe(
            OnEditModeChanged,
            h => EditModeManager.OnEditModeChanged += h,
            h => EditModeManager.OnEditModeChanged -= h);

        // 谱面播放器与编辑面板此时都已经初始化完成，可以安全进入编辑模式
        _playbackController.EnterEditingMode();

        GameSettings.Instance.SettingChanged += OnSettingsChanged;

        // 设置撤销重做按钮
        _undoBtn.Pressed += OnUndo;
        _redoBtn.Pressed += OnRedo;

        // 设置复制粘贴按钮
        _copyBtn.Pressed += _clipboardController.CopySelection;
        _pasteBtn.Pressed += _clipboardController.StartPaste;
        _pasteConfirmBtn.Pressed += _clipboardController.ConfirmPaste;
        _pasteCancelBtn.Pressed += _clipboardController.CancelPaste;

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
    }

    public override void _Process(double delta)
    {
        if(!_isReady) return;

        // 播放器：时间轴同步 + 逻辑更新 + 渲染
        _playbackController.ProcessPlayback(delta);

        // 输入：摇杆与视图平滑（更新 EditorContext 上的视图状态）
        _inputController.ProcessInput(delta);

        #if TOOLS
        ulong t1 = Time.GetTicksUsec();
        #endif

        //同步编辑面板（视图状态统一从 EditorContext 读取，无需逐个字段推送）
        noteEditPanel.UpdateVisuals();
        eventEditPanel.UpdateVisuals();
        bpmEditPanel.UpdateVisuals();

        #if TOOLS
        _panelSyncTimeUs = Time.GetTicksUsec() - t1;
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
        Performance.RemoveCustomMonitor("EditorScene/PanelSyncUs");
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
            _playbackController.UseDefaultResource();
        }
        else
        {
            string id = GameSettings.Instance.Get<string>(nameof(SettingsData.ResourcePackId));
            _resourcePack = ResourcePackLoader.LoadFromLocal(id);
            if(_resourcePack == null)
            {
                GD.PrintErr($"[{Name}] 加载资源包失败, id:{id}");
            }
            _playbackController.SetResourcePack(_resourcePack);
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

    #region 播放控制（场景按钮入口）

    // editor_scene.tscn 的信号连接直接指向 EditorScene 上的这些方法，因此保留为转发入口。
    public void OnPlayButtonClicked()
    {
        _playbackController.PlayWithPlayer();
    }

    public void PlayInEditPanel()
    {
        _playbackController.TogglePlayInEditPanel();
    }

    public void OnStopButtonClicked()
    {
        _playbackController.Stop();
    }

    public void OnPauseClicked()
    {
        _playbackController.PausePlayer();
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

    private void SetEventProperty(
        int lineId, int layer, LineEventEnum lineEventEnum, int index,
        LineEventPropertyType propertyType, object value)
    {
        _chartEditService.SetEventProperty(lineId, layer, lineEventEnum, index, propertyType, value);
    }

    #endregion

    #region Bpm相关方法

    private void SetBpmProperty(BpmEvent bpmEvent, string property, object value)
    {
        _chartEditService.SetBpmProperty(bpmEvent, property, value);
    }

    #endregion

}
