using Godot;
using QuickType;
using System;
using System.IO;

public partial class EditorScene : Node
{
    [ExportGroup("虚拟摇杆引用")]
	[Export] private VirtualJoystick slideJoystick;
	[Export] private VirtualJoystick zoomJoystick;

    [ExportGroup("灵敏度设置")]
	[Export] private float verMouseSensitivity = 100f; // 鼠标滚轮竖直滚动的灵敏度
	[Export] private float verJoystickSensitivity = 10f; // 虚拟摇杆竖直滚动的灵敏度
	[Export] private float zoomJoystickSensitivity = 0.03f; // 虚拟摇杆缩放的灵敏度

    [ExportGroup("")]
    [Export] private NoteEditPanel noteEditPanel;
    [Export] private EventEditPanel eventEditPanel;
    [Export] private ChartPlayer chartPlayer;
    [Export] private Control editPanel;

    private string editingChartId; // 正在编辑的铺面的ID
    private Chart editingChart; // 正在编辑的铺面

    private InputManager inputManager;
    private ChartService _chartService;

    [Export] private float horOffset;
	private float horBeatOffset;
	[Export] private float horSeparation = 100f;
    private float horOffsetSmoothed; // 用于使竖直滚动更平滑
	private float horSeparationSmoothed; // 用于使竖直缩放更平滑

    private bool isPlaying; // 是否正在播放铺面
    private float chartTime; // 谱面当前时间

    public float BeatValue
    {
        get
        {
            return horBeatOffset;
        }
        set
        {
            horBeatOffset = value;
            chartTime = Util.BeatToSecond(horBeatOffset, editingChart.BpmList);
        }
    }

    public float ChartTime
    {
        get
        {
            return chartTime;
        }
        set
        {
            chartTime = value;
            horBeatOffset = Util.SecondToBeat(chartTime, editingChart.BpmList);
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
		horOffset += deltaY * verMouseSensitivity;
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
        
        _chartService = GetNode<ChartService>("/root/ChartService");
        if(_chartService == null)
        {
            GD.PrintErr($"[{this.Name}] ChartService is null");
        }

		//绑定事件
		inputManager.Slide += Slide;
		inputManager.Zoom += Zoom;

        //从global中同步数据
        var global = GetNode<Global>("/root/Global");
        editingChartId = global.editingChartId;

        // 设置正在编辑的铺面
        ChartInfo chartInfo = _chartService.GetChartInfo(editingChartId);
        editingChart = ChartLoader.LoadChart(chartInfo.ChartPath);
        noteEditPanel.editingChart = editingChart;
        eventEditPanel.editingChart = editingChart;

        // ===================初始化谱面播放器
        //1. 设置谱面
        chartPlayer.chart = editingChart;

        //2. 设置背景图片
        Image bgImage = Image.LoadFromFile(chartInfo.PicturePath);
        if (bgImage == null)
        {
            GD.PrintErr($"[{this.Name}] 背景图片导入失败");
            return;
        }
        //TODO 图片模糊效果
        chartPlayer.bgImage = bgImage;

        //3. 设置音乐
        // 因为MP3文件时解压时动态生成的，所以需要使用 AudioStreamMP3.LoadFromFile 加载 MP3
        AudioStream audioStream = AudioStreamMP3.LoadFromFile(chartInfo.SongPath);
        if (audioStream == null)
        {
            GD.PrintErr($"[{this.Name}] 音乐文件加载失败: {chartInfo.SongPath}");
            return;
        }
        chartPlayer.audioStream = audioStream;

        chartPlayer.Initialize();
        chartPlayer.Visible = false;

        GD.Print($"[{this.Name}] 初始化成功 谱面id:{editingChartId}");
    }

    public override void _Process(double delta)
    {
        GD.Print($"ChartTime:{ChartTime}, BeatValue:{BeatValue}, horOffset:{horOffset}");
        //处理摇杆垂直滚动
		if(slideJoystick.Output != Vector2.Zero)
		{
			horOffset -= slideJoystick.Output.Y * verJoystickSensitivity;
			//限制不能滚动到0以下
			if(horOffset < 0) horOffset = 0;

			BeatValue = horOffset / horSeparation;
		}

		//处理摇杆缩放
		if(zoomJoystick.Output != Vector2.Zero)
		{
			Zoom(zoomJoystick.Output.Y * zoomJoystickSensitivity);
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

    public void OnPlayButtonDown()
    {
        if (!isPlaying)
        {
            chartPlayer.Visible = true;
            editPanel.Visible = false;

            //启动chartplayer的播放
            chartPlayer.audioStreamPlayer.Play(ChartTime);
            isPlaying = true;
        }
        else
        {
            chartPlayer.Visible = false;
            editPanel.Visible = true;

            chartPlayer.audioStreamPlayer.Stop();
            isPlaying = false;
        }
    }
    
    public void OnPlayButtonUp()
    {
        // chartPlayer.Visible = false;
        // editPanel.Visible = true;

        // chartPlayer.audioStreamPlayer.Stop();
        // isPlaying = false;
    }

}
