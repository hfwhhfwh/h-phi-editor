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
    private EditorUIManager _uiManager;
    private SettingsController _settingsController;
    private EditorJudgeLineController _judgeLineController;

    // ---- 读取 EditorContext 的便捷访问器（数据实际存放在 Context 中）----
    private Chart editingChart => _context?.EditingChart;
    private string editingChartId => _context?.EditingChartId;

    // 皮肤资源包（由 SettingsController 持有与应用）
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

        // 创建场景级状态中心(EditorContext)，并把 Context / ChartEditService 注入各面板
        InitContext();

        // 创建各场景级控制器（输入、播放、剪贴板、选择、设置）
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
                _settingsController.LoadForChart(chartId);

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
                _settingsController.ApplyEditorSettings();
                
            }),
            ("正在加载资源包...", async () => {
                // ================ 加载资源包 ================
                await Task.Run(() => _settingsController.LoadResourcePack());
                
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

                // 订阅谱面结构变化（判定线/音符增删），让播放器重建拓扑与渲染缓冲
                chartPlayer.SetEventBus(_chartEditService.Events);

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

        // 面板直接依赖 Context（数据）与 ChartEditService（命令）
        foreach (BaseEditPanel panel in new BaseEditPanel[] { noteEditPanel, eventEditPanel, bpmEditPanel })
        {
            panel.Initialize(_context, _chartEditService);
        }

        // 信息面板同样直接持有命令入口，属性修改不再经 EditorScene 转发；
        // 同时注入 Context，让它们在撤销/重做后能按对象引用重新定位并回填数值。
        noteInfoPanel.Initialize(_context, _chartEditService);
        eventInfoPanel.Initialize(_context, _chartEditService);
        bpmInfoPanel.Initialize(_context, _chartEditService);
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

        // ---- 判定线与事件层 ----
        _judgeLineController = new EditorJudgeLineController { Name = "EditorJudgeLineController" };
        AddChild(_judgeLineController);
        _judgeLineController.Initialize(_context, _chartEditService, _inputManager, chooseLinePanel);

        // 判定线面板订阅谱面结构变化（总线实例由 ChartEditService 持有）
        chooseLinePanel.Initialize(_chartEditService.Events);

        // ---- 设置：编辑器设置 / 全局设置 / 资源包 ----
        // 必须在加载任务开始前就绪：读取谱面时会先载入该谱面的编辑器设置。
        _settingsController = new SettingsController { Name = "SettingsController" };
        AddChild(_settingsController);
        _settingsController.Initialize(
            _editorSettings, _context,
            noteEditPanel, eventEditPanel, bpmEditPanel,
            _playbackController);

        // ---- 界面：菜单、按钮、标签、面板显隐（Initialize 在 InitEditor 中调用）----
        _uiManager = new EditorUIManager { Name = "EditorUIManager" };
        AddChild(_uiManager);
    }

    /// <summary>
    /// 把 EditorScene 上从场景导出的界面节点引用打包交给 EditorUIManager。
    /// </summary>
    private EditorViewRefs BuildViewRefs()
    {
        return new EditorViewRefs
        {
            Theme = theme,

            FileMenu = fileMenuButtion,
            EditMenu = editMenuButtion,
            ViewMenu = viewMenuButton,
            HelpMenu = helpMenuButtion,
            OthersButton = _othersButton,

            UndoButton = _undoBtn,
            RedoButton = _redoBtn,
            CopyButton = _copyBtn,
            PasteButton = _pasteBtn,
            MultiSelectButton = _multiSelectBtn,
            BoxSelectButton = _boxSelectBtn,
            PasteConfirmButton = _pasteConfirmBtn,
            PasteCancelButton = _pasteCancelBtn,

            EditingLineLabel = editingLineLabel,
            EditModeLabel = editModeLabel,
            FpsLabel = fpsLabel,

            NoteEditPanel = noteEditPanel,
            EventEditPanel = eventEditPanel,
            BpmEditPanel = bpmEditPanel,

            NoteInfoPanel = noteInfoPanel,
            EventInfoPanel = eventInfoPanel,
            BpmInfoPanel = bpmInfoPanel,

            SettingsPanel = _settingsPanel,
            EditorSettingsPanel = _editorSettingsPanel,

            NoteChooser = noteChooser,
        };
    }

    private void InitEditor()
    {
        // ---- 界面：菜单栏、工具栏按钮、标签、面板显隐 ----
        _uiManager.Initialize(
            BuildViewRefs(),
            _context,
            _chartEditService,
            _clipboardController,
            SaveChart,
            SaveAndQuit,
            OnQuitPressed,
            OnTestPlay);

        // ---- 谱面播放器与编辑面板此时都已经初始化完成，可以安全进入编辑模式 ----
        _playbackController.EnterEditingMode();
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

    public override void _ExitTree()
    {
        base._ExitTree();

        #if TOOLS
        // 取消注册自定义监视器 小心lambda诡异的生命周期问题
        Performance.RemoveCustomMonitor("EditorScene/PanelSyncUs");
        #endif

        // 取消订阅所有事件
        UnsubscribeAll();

        GD.Print($"[{Name}] 成功退出EditorScene");
        
    }
    
    private void SaveChart()
    {
        _chartService.SaveChart(editingChartId, editingChart);
        // 谱面和编辑器视图设置一起保存，避免退出后丢失网格状态。
        _settingsController.SaveEditorSettings();
        // TODO 保存成功后弹出Toast提示
    }

    private void Quit()
    {
        _settingsController.SaveEditorSettings();
        var global = GetNode<Global>("/root/Global");
        global.editingChartId = "";
        global.GotoScene("res://Scene/start_menu.tscn");
    }

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

    private void OnTestPlay()
    {
        var global = GetNode<Global>("/root/Global");
        global.GotoScene("res://Scene/play_scene.tscn");
    }

    #region 场景按钮入口（editor_scene.tscn 的信号连接直接指向这些方法）

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

    public void OnChooseLineClicked()
    {
        _judgeLineController.ToggleChooseLinePanel();
    }

    #endregion

}
