using Godot;
using QuickType;
using System;

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

    private string editingChartId; // 正在编辑的铺面的ID
    private Chart editingChart; // 正在编辑的铺面

    private InputManager inputManager;
    private ChartService _chartService;

    [Export] private float horOffset;
	private float horBeatOffset;
	[Export] private float horSeparation = 100f;
    private float horOffsetSmoothed; // 用于使竖直滚动更平滑
	private float horSeparationSmoothed; // 用于使竖直缩放更平滑

    /// <summary>
	/// 用于缩放
	/// </summary>
	/// <param name="zoomDelta">缩放比例</param>
	public void Zoom(float zoomDelta)
	{
		horSeparation *= 1f + zoomDelta;
		//确保当前处于的beat不变
		horOffset = horBeatOffset * horSeparation;
	}

    public void Slide(float deltaY)
	{
		horOffset += deltaY * verMouseSensitivity;
		//限制不能滚动到0以下
		if(horOffset < 0) horOffset = 0;

		horBeatOffset = horOffset / horSeparation;
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
        
        //绑定noteEditPanel
        noteEditPanel.slideJoystick = slideJoystick;
        noteEditPanel.zoomJoystick = zoomJoystick;

        //从global中同步数据
        var global = GetNode<Global>("/root/Global");
        editingChartId = global.editingChartId;

        // 设置正在编辑的铺面
        ChartInfo chartInfo = _chartService.GetChartInfo(editingChartId);
        editingChart = ChartLoader.LoadChart(chartInfo.ChartPath);
        noteEditPanel.editingChart = editingChart;

        GD.Print($"[{this.Name}] 初始化成功 谱面id:{editingChartId}");
    }

    public override void _Process(double delta)
    {
        //处理摇杆垂直滚动
		if(slideJoystick.Output != Vector2.Zero)
		{
			horOffset -= slideJoystick.Output.Y * verJoystickSensitivity;
			//限制不能滚动到0以下
			if(horOffset < 0) horOffset = 0;

			horBeatOffset = horOffset / horSeparation;
		}

		//处理摇杆缩放
		if(zoomJoystick.Output != Vector2.Zero)
		{
			Zoom(zoomJoystick.Output.Y * zoomJoystickSensitivity);
		}

		//平滑竖直滚动
        horOffsetSmoothed += (horOffset - horOffsetSmoothed) * 0.3f;
		if(Math.Abs(horOffset - horOffsetSmoothed) <= 0.05f)
		{
			horOffsetSmoothed = horOffset;
		}

		//平滑竖直缩放
		horSeparationSmoothed += (horSeparation - horSeparationSmoothed) * 0.3f;
		if(Math.Abs(horSeparation - horSeparationSmoothed) <= 0.05f)
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

}
