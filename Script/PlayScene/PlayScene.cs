using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class PlayScene : Node
{
    [Export] private string chartId = "45201552814680";
    [Export] private Control parent;
    [Export] private GameChartPlayer chartPlayer;
    [Export] private BaseChartRenderer chartRenderer;
    [Export] private Label statusLabel;

    private ChartService _chartService;
    private Chart _chart;
    private ChartJudge _judge;
    private bool _isPlaying = true;
    private bool _mousePressed;
    private Vector2 _pressedMousePos;
    private Dictionary<int, Vector2> _pressedTouch = new();

    public float FlickSpeedThreshold { get; set; } = 500f;
    private double _gameTime;

    public override void _Ready()
    {
        base._Ready();

        _chartService = GetNode<ChartService>("/root/ChartService");
        if (_chartService == null)
        {
            GD.PrintErr($"[{Name}] ChartService is null");
            return;
        }

        if (chartPlayer == null)
        {
            GD.PrintErr($"[{Name}] chartPlayer is null");
            return;
        }

        if (chartRenderer == null)
        {
            GD.PrintErr($"[{Name}] chartRenderer is null");
            return;
        }

        if (parent == null)
        {
            var root = GetNode<Control>("Control/ChartPlayerContainer/ControlParent");
            if (root != null)
                parent = root;
        }

        LoadChart();
        if (_chart == null) return;

        var chartInfo = _chartService.GetChartInfo(chartId);
        if (chartInfo == null)
        {
            GD.PrintErr($"[{Name}] 未找到谱面 {chartId}");
            return;
        }

        chartPlayer.UseDefaultResource();
        chartRenderer.UseDefaultResource();

        chartPlayer.Initialize(parent, _chart, Image.LoadFromFile(chartInfo.PicturePath), FileUtil.LoadAudioFromFile(chartInfo.SongPath));
        chartRenderer.Initialize(parent);

        _judge = new ChartJudge();
        AddChild(_judge);
        _judge.Initialize(chartPlayer, parent, _chart);
        _judge.OnJudgeResult += OnJudgeResult;
        _judge.OnHoldEndJudgeResult += OnHoldEndJudgeResult;
        

        chartPlayer.AutoHitEnabled = false;
        chartPlayer.Play(0f);
    }

    public override void _ExitTree()
    {
        _judge.OnJudgeResult -= OnJudgeResult;
        _judge.OnHoldEndJudgeResult -= OnHoldEndJudgeResult;
        _judge = null;


        base._ExitTree();
    }


    public override void _Process(double delta)
    {
        base._Process(delta);

        if (_chart == null || chartPlayer == null || _judge == null)
            return;

        if (_isPlaying)
        {
            chartPlayer.UpdateLogic(delta);
            _gameTime = chartPlayer.ChartTime;

            // 处理触摸事件
            if (_mousePressed)
            {
                OnTouchInput(_pressedMousePos);
            }
            foreach(Vector2 pos in _pressedTouch.Values)
            {
                OnTouchInput(pos);
            }

            _judge.Update(chartPlayer.ChartTime, delta);
        }

        var (lineData, lineCount) = chartPlayer.GetLineRenderDatas();
        var (noteData, noteCount) = chartPlayer.GetNoteRenderDatas();
        chartRenderer.Render(lineData, lineCount, noteData, noteCount);
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        // 不接受模拟输入
        if(@event.Device == -1) return;

        if (_chart == null || chartPlayer == null || _judge == null) return;
        if (!chartPlayer.IsPlaying) return;

        // ---- 1. 鼠标模拟输入 ----
        if(@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if(mouseBtn.Pressed)
            {
                OnClickInput(mouseBtn.Position);
                _mousePressed = true;
                _pressedMousePos = mouseBtn.Position;
            }
            else
            {
                _mousePressed = false;
            }
        }
        else if(@event is InputEventMouseMotion mouseMotion)
        {
            if(_mousePressed == true)
            {
                _pressedMousePos = mouseMotion.Position;

                if(mouseMotion.Velocity.Length() >= FlickSpeedThreshold)
                {
                    OnFlickInput(mouseMotion.Position);
                }
            }
        }

        // ---- 2. 屏幕输入 ----
        else if(@event is InputEventScreenTouch screenTouch)
        {
            if(screenTouch.Pressed)
            {
                OnClickInput(screenTouch.Position);

                _pressedTouch[screenTouch.Index] = screenTouch.Position;
            }
            else
            {
                _pressedTouch.Remove(screenTouch.Index);
            }
        }
        else if(@event is InputEventScreenDrag screenDrag)
        {
            if(_pressedTouch.ContainsKey(screenDrag.Index))
            {
                _pressedTouch[screenDrag.Index] = screenDrag.Position;
            }

            if(screenDrag.Velocity.Length() >= FlickSpeedThreshold)
            {
                OnFlickInput(screenDrag.Position);
            }
        }
    }

    private void OnClickInput(Vector2 pos)
    {
        GD.Print($"Click {_gameTime:F2}, {pos}");
        _judge.OnTapInput(pos, _gameTime);
    }

    private void OnTouchInput(Vector2 pos)
    {
        _judge.OnTouchInput(pos, _gameTime);
    }

    private void OnFlickInput(Vector2 pos)
    {
        _judge.OnFlickInput(pos, _gameTime);
    }

    private void LoadChart()
    {
        var info = _chartService.GetChartInfo(chartId);
        if (info == null)
        {
            GD.PrintErr($"[{Name}] 未找到谱面 {chartId}");
            return;
        }

        _chart = ChartLoader.LoadChart(info.ChartPath);
        if (_chart == null)
        {
            GD.PrintErr($"[{Name}] 谱面加载失败: {info.ChartPath}");
            return;
        }
    }

    private void OnJudgeResult(JudgeResult result)
    {
        GD.Print($"{result.Grade} {result.TimeDeltaMs:F0}ms");

        if (statusLabel != null)
        {
            //if(result.Grade != JudgeGrade.Miss)
            {
                statusLabel.Text = $"{result.Grade} {result.TimeDeltaMs:F0}ms";
            }
        }

        if (result.Grade == JudgeGrade.Perfect || result.Grade == JudgeGrade.Good)
        {
            chartPlayer.TriggerHit(result);
        }
        else if (result.Grade == JudgeGrade.Bad)
        {
            chartPlayer.CreateBadEffect(result, 
                new Vector2(chartRenderer.NoteScale, chartRenderer.NoteScale));
        }
        else
        {
            // GD.Print($"Miss at {result.TimeDeltaMs}ms");
        }
    }

    private void OnHoldEndJudgeResult(JudgeResult result)
    {
        GD.Print($"{result.Grade} {result.TimeDeltaMs:F0}ms");
        
        if (statusLabel != null)
        {
            // if(result.Grade != JudgeGrade.Miss)
            {
                statusLabel.Text = $"{result.Grade} {result.TimeDeltaMs:F0}ms";
            }
            
        }
    }
}
