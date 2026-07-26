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
    [Export] private Label editingLineLabel;
    [Export] private NoteInfoPanel noteInfoPanel;

    [Export] private Label fpsLabel;

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
            return;
        }
        //TODO 图片模糊效果
        // chartPlayer2.bgImage = bgImage;

        // 音乐
        // 因为MP3文件时解压时动态生成的，所以需要使用 AudioStreamMP3.LoadFromFile 加载 MP3
        AudioStream audioStream = FileUtil.LoadAudioFromFile(chartInfo.SongPath);
        if (audioStream == null)
        {
            GD.PrintErr($"[{this.Name}] 音乐文件加载失败: {chartInfo.SongPath}");
            return;
        }
        // chartPlayer2.audioStream = audioStream;
        // AudioStream audioStream = AudioStreamMP3.LoadFromFile(chartInfo.SongPath);
        // if (audioStream == null)
        // {
        //     GD.PrintErr($"[{this.Name}] 音乐文件加载失败: {chartInfo.SongPath}");
        //     return;
        // }
        // chartPlayer.audioStream = audioStream;

        // chartPlayer2.Initialize();
        // chartPlayer2.Visible = false;

        chartPlayer.Initialize(chartPlayParent, editingChart, bgImage, audioStream);
        chartRenderer.Initialize(chartPlayParent);

        chartPlayParent.ClipContents = true;

        chooseLinePanel.Visible = false;
        chooseLinePanel.LineSelected += SetEditingLine;

        editingLineLabel.Text = $"正在编辑:线{1}";

        noteEditPanel.OnNoteSelected += OnNoteSelected;

        noteInfoPanel.OnConfirmed += () =>
        {
            noteInfoPanel.Visible = false;
        };

        noteInfoPanel.OnNotePropertyChanged += 
        (int lineId, int noteIndex, NotePropertyType property, object value) => {
            chartEditService.SetNoteProperty(lineId, noteIndex, property, value);
        };

        //设置弹出菜单
        PopupMenuHelper.SetTheme(theme);

        //设置ChartEditService
        chartEditService.EditingChart = editingChart;

        GD.Print($"[{this.Name}] 初始化成功 谱面id:{editingChartId}");
    }

    public override void _Process(double delta)
    {
        
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
        chartPlayer.UpdateLogic();

        List<JudgeLineRenderData> judgeLineRenderDatas = chartPlayer.GetLineRenderDatas();
        List<NoteRenderData> noteRenderDatas = chartPlayer.GetNoteRenderDatas();

        chartRenderer.Render(judgeLineRenderDatas, noteRenderDatas);
        

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

        //同步编辑面板
        noteEditPanel.horOffsetSmoothed = horOffsetSmoothed;
        noteEditPanel.horSeparationSmoothed = horSeparationSmoothed;
        noteEditPanel.QueueRedraw();

        eventEditPanel.horOffsetSmoothed = horOffsetSmoothed;
        eventEditPanel.horSeparationSmoothed = horSeparationSmoothed;
        eventEditPanel.QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        //GD.Print($"ChartTime:{ChartTime}, BeatValue:{BeatValue}, horOffset:{horOffset}");
        fpsLabel.Text = $"FPS:{Performance.GetMonitor(Performance.Monitor.TimeFps)}";
    }


    
    public void OnPlayButtonClicked()
    {
        if (!isPlaying)
        {
            chartPlayParent.Visible = true;
            editPanel.Visible = false;

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
        chartPlayParent.Visible = false;
        editPanel.Visible = true;

        chartPlayer.Pause();
        chartPlayer.IsPlaying = false;

        //更新右侧面板
        rightPanel.SwitchToTab(RightPanel.RightPanelTabPage.Normal);

        isPlaying = false;
    }

    public void OnChooseLineClicked()
    {
        if(chooseLinePanel.Visible == false)
        {
            chooseLinePanel.Visible = true;
            inputManager.IsEnable = false;

            //准备LineInfo数据
            List<ChooseLinePanel.LineInfo> lineInfos = new();
            for (int i = 0; i < editingChart.JudgeLineList.Length; i++)
            {
                JudgeLine line = editingChart.JudgeLineList[i];

                lineInfos.Add(new ChooseLinePanel.LineInfo
                {
                    Id = i, // 判定线的编号从0开始
                    NoteCount = line.NumOfNotes,
                    //NextEventTime = //TODO
                });
            }

            //设置LineInfo数据
            chooseLinePanel.ShowInfos(lineInfos);
        }
        else
        {
            chooseLinePanel.Visible = false;
            inputManager.IsEnable = true;
        }
    }

    private void SetEditingLine(int id)
    {
        GD.Print($"[{this.Name}] 用户选择了Line:{id}");
        editingLineId = id-1;

        noteEditPanel.EditingLineId = id-1;
        eventEditPanel.EditingLineId = id-1;

        editingLineLabel.Text = $"正在编辑:线{id}";

        chooseLinePanel.Visible = false;
        inputManager.IsEnable = true;
    }

    private void OnNoteSelected(int lineId, int noteIndex)
    {
        Note note = editingChart.JudgeLineList[lineId].Notes[noteIndex];

        float beatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
        Vector2 popupPos = noteEditPanel.GetGlobalPosition(beatValue, note.PositionX)
            + new Vector2(30,30);

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
        PopupMenu popupMenu = PopupMenuHelper.ShowPopupMenu(this, popupPos, items);
        popupMenu.PopupHide += () =>
        {
            noteEditPanel.DeselectAll();
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
        chartEditService.DeleteNote(lineId, noteIndex);
    }
}
