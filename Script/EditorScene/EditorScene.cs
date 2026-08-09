using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;


public partial class EditorScene : Node
{
    [ExportGroup("虚拟摇杆引用")]
	[Export] private VirtualJoystick slideJoystick;
	[Export] private VirtualJoystick zoomJoystick;

    [ExportGroup("灵敏度设置")]
	[Export] private float verMouseSensitivity = 100f; // 鼠标滚轮竖直滚动的灵敏度
    [Export] private float zoomMouseSensitivity = 1f; // 鼠标滚轮竖直缩放的灵敏度
	[Export] private float verJoystickSensitivity = 1000f; // 虚拟摇杆竖直滚动的灵敏度
	[Export] private float zoomJoystickSensitivity = 1f; // 虚拟摇杆缩放的灵敏度

    [ExportGroup("资源引用")]
    [Export] private Theme theme;

    [ExportGroup("")]
    [Export] private NoteEditPanel noteEditPanel;
    [Export] private EventEditPanel eventEditPanel;
    [Export] private BaseChartPlayer chartPlayer;
    [Export] private BaseChartRenderer chartRenderer;
    [Export] private Control chartPlayParent;
    [Export] private Control editPanel;
    [Export] private RightPanel rightPanel;
    [Export] private ChooseLinePanel chooseLinePanel;
    [Export] private NoteInfoPanel noteInfoPanel;
    [Export] private LineEventInfoPanel eventInfoPanel;
    [Export] private NoteChooser noteChooser;
    [Export] private MenuButton fileMenuButtion;
    [Export] private MenuButton editMenuButtion;
    [Export] private MenuButton helpMenuButtion;

    [Export] private Label editingLineLabel;
    [Export] private Label fpsLabel;
    [Export] private Label editModeLabel;

    private string editingChartId; // 正在编辑的铺面的ID
    private Chart editingChart; // 正在编辑的铺面
    private int editingLineId; // 正在编辑的判定线编号

    private InputManager inputManager;
    private ChartService _chartService;
    private ChartEditService chartEditService;

    [Export] private float horOffset;
	private float horBeatOffset;
	[Export] private float horSeparation = 100f;
    private float horOffsetSmoothed; // 用于使竖直滚动更平滑
	private float horSeparationSmoothed; // 用于使竖直缩放更平滑

    private bool isPlaying; // 是否正在播放铺面
    private double chartTime; // 谱面当前时间

    public float BeatValue
    {
        get
        {
            return horBeatOffset;
        }
        set
        {
            horBeatOffset = value;
            chartTime = TimeUtil.BeatToSecond(horBeatOffset, editingChart.BpmList);
        }
    }

    public double ChartTime
    {
        get
        {
            return chartTime;
        }
        set
        {
            chartTime = value;
            horBeatOffset = TimeUtil.SecondToBeat((float)chartTime, editingChart.BpmList);
            horOffset = horBeatOffset * horSeparation;
        }
    }

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
		horSeparation *= 1f + zoomDelta;
		//确保当前处于的beat不变
		horOffset = BeatValue * horSeparation;
	}

    public void Slide(float deltaY)
	{
		horOffset += deltaY;
		//限制不能滚动到0以下
		if(horOffset < 0) horOffset = 0;

		BeatValue = horOffset / horSeparation;
	}


    public override void _Ready()
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
        inputManager = GetNode<InputManager>("/root/InputManager");
        if(inputManager == null)
        {
            GD.PrintErr($"[{this.Name}] inputManager is null");
        }
        inputManager.IsEnable = true;
        
        _chartService = GetNode<ChartService>("/root/ChartService");
        if(_chartService == null)
        {
            GD.PrintErr($"[{this.Name}] ChartService is null");
        }

        chartEditService = GetNode<ChartEditService>("/root/ChartEditService");
        if(chartEditService == null)
        {
            GD.PrintErr($"[{this.Name}] ChartEditService is null");
        }

		//绑定事件
		inputManager.Slide += (float x) =>
        {
            Slide(x * verMouseSensitivity);
        };
		inputManager.Zoom += (float x) =>
        {
            Zoom(x * zoomMouseSensitivity);
        };

        //从global中同步数据
        var global = GetNode<Global>("/root/Global");
        editingChartId = global.editingChartId;

        // 设置正在编辑的铺面
        ChartInfo chartInfo = _chartService.GetChartInfo(editingChartId);
        editingChart = ChartLoader.LoadChart(chartInfo.ChartPath);
        noteEditPanel.editingChart = editingChart;
        eventEditPanel.editingChart = editingChart;

        // ===================初始化谱面播放器=================
        // 背景图片
        Image bgImage = Image.LoadFromFile(chartInfo.PicturePath);
        if (bgImage == null)
        {
            GD.PrintErr($"[{this.Name}] 背景图片导入失败");
            //return;
        }
        //TODO 图片模糊效果
        // chartPlayer2.bgImage = bgImage;

        // 音乐
        // 因为MP3文件时解压时动态生成的，所以需要使用 AudioStreamMP3.LoadFromFile 加载 MP3
        AudioStream audioStream = FileUtil.LoadAudioFromFile(chartInfo.SongPath);
        if (audioStream == null)
        {
            GD.PrintErr($"[{this.Name}] 音乐文件加载失败: {chartInfo.SongPath}");
            //return;
        }

        chartPlayer.Initialize(chartPlayParent, editingChart, bgImage, audioStream);
        chartRenderer.Initialize(chartPlayParent);

        chartPlayParent.ClipContents = true;

        SetChartPlayerVisible(false); // 初始不显示


        // 设置chooseLinePanel
        chooseLinePanel.Visible = false;
        chooseLinePanel.LineSelected += SetEditingLine;
        chooseLinePanel.AddLineRequested += AddLine;
        chooseLinePanel.DeleteLineRequested += DeleteLine;
        chooseLinePanel.RefreshRequested += RefreshChooseLinePanel;

        editingLineLabel.Text = $"正在编辑:线{0}";

        // 设置NoteEditPanel
        noteEditPanel.OnNoteSelected += OnNoteSelected;
        noteEditPanel.NoteAddRequested += AddNote;
        noteEditPanel.NoteDeleteRequested += OnNotesDelete;
        noteEditPanel.Disabled = false;

        // 设置eventEditPanel
        eventEditPanel.EventSelected += OnEventSelected;
        eventEditPanel.EventsDeleteRequested += DeleteEvents;
        eventEditPanel.AddEventRequested += AddEvent;
        eventEditPanel.Disabled = false;

        SetEditPanelVisible(true); // 初始默认显示

        // 设置noteInfoPanel
        noteInfoPanel.OnConfirmed += () => noteInfoPanel.Visible = false;
        noteInfoPanel.OnNotePropertyChanged += SetNoteProperty;

        // 设置eventInfoPanel
        eventInfoPanel.OnConfirmed += () => eventInfoPanel.Visible = false;
        eventInfoPanel.PropertyChanged += SetEventProperty;

        //设置弹出菜单
        PopupMenuHelper.SetTheme(theme);

        //设置ChartEditService
        chartEditService.EditingChart = editingChart;

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
                new PopupMenuItem { Text = "仅退出", Callback = Quit},
            };
            PopupMenuHelper.SetMenuButton(fileMenuButtion, items);
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
                // new PopupMenuItem { Text = "放置Note", Checkable = true, 
                //     Toggled = (bool isChecked) =>
                //     {
                //         GD.Print($"放置Note 被设置为:{isChecked}");
                //         // 切换编辑模式
                //         if(isChecked) editMode = EditMode.PlacingNote;
                //         else editMode = EditMode.Normal;
                //     }
                // },
                // new PopupMenuItem { IsSeparator = true},
                new PopupMenuItem { Text = "偏好设置", Callback = null},
            };
            PopupMenuHelper.SetMenuButton(editMenuButtion, items);
        }

        //设置NoteChooser
        noteChooser.NoteChoosed += OnNoteChooserNoteChoosed;
        noteChooser.Deselected += OnNoteChooserDeselected;
        noteChooser.DeleteButtonChoosed += OnNoteChooserDeleteChoosed;

        //设置EditModeManager 初始状态默认为常规模式
        EditModeManager.SetEditMode(EditModeEnum.Normal);

        //设置editModeLabel
        editModeLabel.Text = "编辑模式：常规模式";
        EditModeManager.OnEditModeChanged += OnEditModeChanged;
        

        GD.Print($"[{this.Name}] 初始化成功 谱面id:{editingChartId}");

        
    }

    public override void _Process(double delta)
    {
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
            chartPlayer.ExternalTime = chartTime;
        }

        #if TOOLS
        ulong t2 = Time.GetTicksUsec();
        #endif
        
        chartPlayer.UpdateLogic();

        #if TOOLS
        ulong t3 = Time.GetTicksUsec();
        #endif

        (JudgeLineRenderData[] lineData, int lineCount) = chartPlayer.GetLineRenderDatas();
        (NoteRenderData[] noteData, int noteCount) = chartPlayer.GetNoteRenderDatas();

        chartRenderer.Render(lineData, lineCount, noteData, noteCount);

        #if TOOLS
        ulong t4 = Time.GetTicksUsec();
        #endif

        //处理摇杆垂直滚动
		if(slideJoystick.Output != Vector2.Zero)
		{
			horOffset -= slideJoystick.Output.Y * verJoystickSensitivity * (float)delta;
			//限制不能滚动到0以下
			if(horOffset < 0) horOffset = 0;

			BeatValue = horOffset / horSeparation;
		}

		//处理摇杆缩放
		if(zoomJoystick.Output != Vector2.Zero)
		{
			Zoom(zoomJoystick.Output.Y * zoomJoystickSensitivity * (float)delta);
		}

		//平滑竖直滚动
        if(Math.Abs(horOffset - horOffsetSmoothed) > 0.001f)
		{
			horOffsetSmoothed += (horOffset - horOffsetSmoothed) * (float)delta * 18f;
		}
        else
        {
            horOffsetSmoothed = horOffset;
        }

		//平滑竖直缩放
		if(Math.Abs(horSeparation - horSeparationSmoothed) > 0.001f)
		{
            horSeparationSmoothed += (horSeparation - horSeparationSmoothed) * (float)delta * 18f;
		}
        else
        {
            horSeparationSmoothed = horSeparation;
        }

        #if TOOLS
        ulong t5 = Time.GetTicksUsec();
        #endif

        //同步编辑面板
        noteEditPanel.horOffsetSmoothed = horOffsetSmoothed;
        noteEditPanel.horSeparationSmoothed = horSeparationSmoothed;
        noteEditPanel.UpdateVisuals();

        eventEditPanel.horOffsetSmoothed = horOffsetSmoothed;
        eventEditPanel.horSeparationSmoothed = horSeparationSmoothed;
        eventEditPanel.UpdateVisuals();

        #if TOOLS
        ulong t6 = Time.GetTicksUsec();

        _setChartTimeTimeUs = t2 - t1;
        _logicTimeUs = t3 - t2;
        _renderTimeUs = t4 - t3;
        _uiTimeUs = t5 - t4;
        _drawEditPanelTimeUs = t6 - t5;
        #endif

    }

    public override void _PhysicsProcess(double delta)
    {
        //GD.Print($"ChartTime:{ChartTime}, BeatValue:{BeatValue}, horOffset:{horOffset}");
        fpsLabel.Text = $"FPS:{Performance.GetMonitor(Performance.Monitor.TimeFps)}";
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        #if TOOLS
        // 取消注册自定义监视器 小心lambda诡异的生命周期问题
        Performance.RemoveCustomMonitor("EditorScene/SetChartTimeTimeUs");
        Performance.RemoveCustomMonitor("EditorScene/LogicTimeUs");
        Performance.RemoveCustomMonitor("EditorScene/RenderTimeUs");
        Performance.RemoveCustomMonitor("EditorScene/UITimeUs");
        Performance.RemoveCustomMonitor("EditorScene/DrawEditPanelTimeUs");
        #endif

        // 设置NoteEditPanel
        noteEditPanel.NoteAddRequested -= AddNote;
        noteEditPanel.NoteDeleteRequested -= OnNotesDelete;

        // 设置eventEditPanel
        eventEditPanel.EventSelected -= OnEventSelected;
        eventEditPanel.EventsDeleteRequested -= DeleteEvents;
        eventEditPanel.AddEventRequested -= AddEvent;

        //设置eventInfoPanel
        eventInfoPanel.PropertyChanged -= SetEventProperty;

        EditModeManager.OnEditModeChanged -= OnEditModeChanged;

        GD.Print($"[{Name}] 成功退出EditorScene");
        
    }

    private void SetEditPanelVisible(bool value)
    {
        editPanel.Visible = value;
        noteEditPanel.Disabled = !value;
        eventEditPanel.Disabled = !value;
    }

    private void SetChartPlayerVisible(bool value)
    {
        chartPlayParent.Visible = value;
        chartPlayer.Disabled = !value;
        chartRenderer.Disabled = !value;
    }

    public void OnPlayButtonClicked()
    {
        if (!isPlaying)
        {
            SetChartPlayerVisible(true);

            SetEditPanelVisible(false);

            //启动chartplayer的播放
            chartPlayer.Play((float)ChartTime);
            chartPlayer.IsPlaying = true;

            //更新右侧面板
            rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Playing);

            isPlaying = true;
            
        }
        else
        {
            OnStopButtonClicked();
        }
    }

    public void OnStopButtonClicked()
    {
        SetChartPlayerVisible(false);

        SetEditPanelVisible(true);

        chartPlayer.Pause();
        chartPlayer.IsPlaying = false;

        //更新右侧面板
        rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Normal);

        isPlaying = false;
    }

    private void OnChooseLineClicked()
    {
        if(chooseLinePanel.Visible == false)
        {
            chooseLinePanel.Visible = true;
            inputManager.IsEnable = false;

            RefreshChooseLinePanel();
        }
        else
        {
            chooseLinePanel.Visible = false;
            inputManager.IsEnable = true;
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
        editingLineId = id;

        noteEditPanel.EditingLineId = id;
        eventEditPanel.EditingLineId = id;

        editingLineLabel.Text = $"正在编辑:线{id}";

        chooseLinePanel.Visible = false;
        inputManager.IsEnable = true;
    }

    private void SetNoteProperty(int lineId, int noteIndex, NotePropertyEnum property, object value)
    {
        chartEditService.SetNoteProperty(lineId, noteIndex, property, value);
    }

    private void OnNoteSelected(int lineId, int noteIndex, Vector2 popupViewportPos)
    {
        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];

        float beatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
        //Vector2 popupPos = noteEditPanel.GetScreenPosition(beatValue, note.PositionX)
        //    + new Vector2(30,30);

        // 构建菜单项（使用闭包捕获当前音符信息）
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnNoteEdit(lineId, noteIndex) },
            new PopupMenuItem { Text = "复制", Callback = () => OnNoteCopy(lineId, noteIndex) },
            new PopupMenuItem { Text = "粘贴", Callback = () => OnNotePaste(lineId, noteIndex) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnNoteDelete(lineId, noteIndex) }
        };

        // 弹出菜单
        PopupMenu popupMenu = PopupMenuHelper.ShowPopupMenu(this, popupViewportPos, items);
        popupMenu.PopupHide += () =>
        {
            noteEditPanel.DeselectAll();
        };
    }

    private void OnNoteChooserDeselected()
    {
        EditModeManager.SetEditMode(EditModeEnum.Normal);
        // GD.Print($"[{this.Name}] 用户取消选择了note");
    }

    private void OnEditModeChanged(EditModeEnum editMode)
    {
        editModeLabel.Text = editMode switch
        {
            EditModeEnum.Normal => "编辑模式：常规模式",
            EditModeEnum.Place => "编辑模式：放置模式",
            EditModeEnum.Delete => "编辑模式：删除模式",
            _ => "编辑模式：未知",
        };
    }

    private void OnNoteEdit(int lineId, int noteIndex)
    {
        noteInfoPanel.Visible = true;
        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];
        noteInfoPanel.ShowInfo(note, lineId, noteIndex);
    }

    private void OnNoteCopy(int lineId, int noteIndex)
    {
        throw new NotImplementedException();
    }

    private void OnNotePaste(int lineId, int noteIndex)
    {
        throw new NotImplementedException();
    }

    private void OnNoteDelete(int lineId, int noteIndex)
    {
        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];
        chartEditService.DeleteNote(lineId, note);
    }

    private void OnNotesDelete(int lineId, List<Note> notes)
    {
        chartEditService.DeleteNotes(lineId, notes);
    }

    private void SaveChart()
    {
        _chartService.SaveChart(editingChartId, editingChart);
    }

    private void Quit()
    {
        var global = GetNode<Global>("/root/Global");
        global.editingChartId = "";
        global.GotoScene("res://Scene/start_menu.tscn");
    }

    private void SaveAndQuit()
    {
        SaveChart();
        Quit();
    }

    private void OnNoteChooserNoteChoosed(NoteType noteType)
    {
        EditModeManager.SetEditMode(EditModeEnum.Place);
        noteEditPanel.PlacingNote = noteType;
    }

    private void OnNoteChooserDeleteChoosed()
    {
        EditModeManager.SetEditMode(EditModeEnum.Delete);
    }

    private void AddNote(NoteType noteType, Beat startBeatValue, Beat EndBeatValue, float posX)
    {
        chartEditService.AddNote(editingLineId, noteType, startBeatValue, EndBeatValue, posX);
        //通知谱面数据产生了变化
        ChartEventBus.NotifyDataChanged();
    }

    private void AddLine()
    {
        chartEditService.AddLine(editingChart.JudgeLineList, -1);
    }

    private void DeleteLine(int id)
    {
        if(editingChart.JudgeLineList.Count <= 1)
        {
            GD.Print($"[{this.Name}] 最少保留一条判定线，删除失败");
            // TODO 最少保留一条判定线，删除失败时显示弹窗提示
            return;
        }
        chartEditService.DeleteLine(editingChart.JudgeLineList, id);
    }

    private void OnEventSelected(int lineId, LineEventEnum lineEventEnum, int eventIndex, Vector2 popupViewportPos)
    {
        EventLayer eventLayer = editingChart.JudgeLineList[editingLineId].EventLayers[0];
		LineEvent lineEvent = eventLayer.GetLineEvents(lineEventEnum)[eventIndex];

        // 构建菜单项（使用闭包捕获当前音符信息）
        var items = new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "编辑", Callback = () => OnEventEdit(lineId, lineEventEnum, eventIndex) },
            new PopupMenuItem { Text = "复制", Callback = () => OnEventCopy(lineId, lineEventEnum, eventIndex) },
            new PopupMenuItem { Text = "粘贴", Callback = () => OnEventPaste(lineId, lineEventEnum, eventIndex) },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "删除", Callback = () => OnEventDelete(lineId, lineEventEnum, eventIndex) }
        };

        // 弹出菜单
        PopupMenu popupMenu = PopupMenuHelper.ShowPopupMenu(this, popupViewportPos, items);
        popupMenu.PopupHide += () =>
        {
            noteEditPanel.DeselectAll();
        };
    }

    private void OnEventEdit(int lineId, LineEventEnum lineEventEnum, int index)
    {
        GD.Print($"[{this.Name}] 编辑事件 line:{lineId}, type:{lineEventEnum}, index:{index}");
        eventInfoPanel.Visible = true;
        eventEditPanel.DeselectAll();

        LineEvent lineEvent = editingChart.JudgeLineList[lineId].EventLayers[0].GetLineEvents(lineEventEnum)[index];

        eventInfoPanel.Edit(lineEvent, lineId, lineEventEnum, index);
    }

    private void SetEventProperty(
        int lineId, LineEventEnum lineEventEnum, int index,
        LineEventPropertyType propertyType, object value)
    {
        chartEditService.SetEventProperty(lineId, lineEventEnum, index, propertyType, value);
    }

    private void OnEventCopy(int lineId, LineEventEnum lineEventEnum, int index)
    {
        GD.Print($"[{this.Name}] 复制事件 line:{lineId}, type:{lineEventEnum}, index:{index}");
        //throw new NotImplementedException();
    }
    private void OnEventPaste(int lineId, LineEventEnum lineEventEnum, int index)
    {
        GD.Print($"[{this.Name}] 粘贴事件 line:{lineId}, type:{lineEventEnum}, index:{index}");
        //throw new NotImplementedException();
    }
    private void OnEventDelete(int lineId, LineEventEnum lineEventEnum, int index)
    {
        chartEditService.DeleteEvent(lineId, lineEventEnum, index);

    }

    private void DeleteEvents(int lineId, List<LineEvent> eventsToDelete)
    {
        // lineEvents可能包含不同种类的事件，需要分别删除，构建一张表格
        LineEventEnum[] allEventTypes = (LineEventEnum[])Enum.GetValues(typeof(LineEventEnum));
        Dictionary<LineEventEnum, List<int>> table = new();

        foreach(LineEvent lineEvent in eventsToDelete)
        {
            foreach(LineEventEnum lineEventEnum in allEventTypes)
            {
                List<LineEvent> lineEvents = editingChart.JudgeLineList[lineId].EventLayers[0].GetLineEvents(lineEventEnum);
                if (lineEvents.Contains(lineEvent))
                {
                    //添加到表格
                    if (!table.TryGetValue(lineEventEnum, out var indices))
                    {
                        indices = new List<int>();
                        table[lineEventEnum] = indices;
                    }
                    indices.Add(lineEvents.IndexOf(lineEvent));
                    break;
                }
            }
        }

        //分别删除
        foreach(LineEventEnum lineEventEnum in table.Keys)
        {
            chartEditService.DeleteEvents(lineId, lineEventEnum, table[lineEventEnum]);
        }
    }

    private void AddEvent(int lineId, LineEventEnum lineEventEnum, Beat startBeat, Beat endBeat)
    {
        chartEditService.AddEvent(lineId, lineEventEnum, startBeat, endBeat);
    }
}
