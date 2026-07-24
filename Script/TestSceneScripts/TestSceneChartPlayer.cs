using Godot;
using QuickType;
using System;

public partial class TestSceneChartPlayer : Node
{
    [Export] private string editingChartId;
    private Chart editingChart;
    private bool isPlaying = true;
    private double chartTime;

    [Export] private Control parent;
    [Export] private BaseChartPlayer chartPlayer;

    private ChartService chartService;
    public override void _Ready()
    {
        base._Ready();

        chartService = GetNode<ChartService>("/root/ChartService");
        if(chartService == null)
        {
            GD.PrintErr($"[{this.Name}] ChartService is null");
        }

        // 设置正在编辑的铺面
        ChartInfo chartInfo = chartService.GetChartInfo(editingChartId);
        editingChart = ChartLoader.LoadChart(chartInfo.ChartPath);

        // ===================初始化谱面播放器=================
        // 背景图片
        Image bgImage = Image.LoadFromFile(chartInfo.PicturePath);
        if (bgImage == null)
        {
            GD.PrintErr($"[{this.Name}] 背景图片导入失败");
            return;
        }

        // 音乐
        // 因为MP3文件时解压时动态生成的，所以需要使用 AudioStreamMP3.LoadFromFile 加载 MP3
        AudioStream audioStream = FileUtil.LoadAudioFromFile(chartInfo.SongPath);
        if (audioStream == null)
        {
            GD.PrintErr($"[{this.Name}] 音乐文件加载失败: {chartInfo.SongPath}");
            return;
        }

        chartPlayer.Initialize(parent, editingChart, bgImage, audioStream);

        //开始播放
        chartPlayer.Play((float)chartTime);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (isPlaying)
        {
            //正在播放时，时间轴由音乐决定
            chartTime = chartPlayer.ChartTime;
        }
        else
        {
            //否则，时间轴由编辑器面板决定
            chartPlayer.ExternalTime = chartTime;
        }

        chartPlayer.UpdateLogic();

        chartPlayer.Render();

    }


}
