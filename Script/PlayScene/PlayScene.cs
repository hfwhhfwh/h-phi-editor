using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class PlayScene : Node
{
    [Export] private string chartId = "";
    [Export] private PlayTestParent parent;
    [Export] private GameChartPlayer chartPlayer;
    [Export] private BaseChartRenderer chartRenderer;
    [Export] private Label statusLabel;
    [Export] private TouchScreenButton _pauseButtonTouch;
    [Export] private Button _pauseButtonMouse;
    [Export] private CanvasLayer _pauseLayer;
    [Export] private Button _quitButton;
    [Export] private Button _restartButton;
    [Export] private Button _startButton;
    private bool _isPauseActive = false;
    private float _pauseTimer;

    private ChartService _chartService;
    private Chart _chart;
    private ChartJudge _judge;
    private bool _isPlaying = false;
    private double _gameTime;

    public override void _Ready()
    {
        base._Ready();

        // 从global中同步数据
        var global = GetNode<Global>("/root/Global");
        chartId = global.editingChartId;

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

        parent.Clicked += OnClickInput;
        parent.Touched += OnTouchInput;
        parent.Flicked += OnFlickInput;

        _pauseButtonTouch.Pressed += () =>
        {
            if (!_isPauseActive)
            {
                _isPauseActive = true;
                _pauseButtonTouch.Modulate = Colors.White;
                _pauseTimer = 0;
            }
            else
            {
                if(_pauseTimer <= 0.5f)
                {
                    // 触发暂停
                    Pause();

                    // 重置计时器
                    _isPauseActive = false;
                    _pauseButtonTouch.Modulate = new Color(1, 1, 1, 0.6f);
                    _pauseTimer = 0;
                }
            }
        };
        _pauseButtonTouch.Modulate = new Color(1, 1, 1, 0.6f);

        // _pauseButtonMouse
        _pauseButtonMouse.Pressed += () =>
        {
            if (!_isPauseActive)
            {
                _isPauseActive = true;
                _pauseButtonMouse.Modulate = Colors.White;
                _pauseTimer = 0;
            }
            else
            {
                if(_pauseTimer <= 0.5f)
                {
                    // 触发暂停
                    Pause();

                    // 重置计时器
                    _isPauseActive = false;
                    _pauseButtonMouse.Modulate = new Color(1, 1, 1, 0.6f);
                    _pauseTimer = 0;
                }
            }
        };
        _pauseButtonMouse.Modulate = new Color(1, 1, 1, 0.6f);

        if(Engine.IsEditorHint() || OS.HasFeature("Windows") || OS.HasFeature("windows"))
        {
            _pauseButtonMouse.Visible = true;
            _pauseButtonTouch.Visible = false;
        }
        else
        {
            _pauseButtonMouse.Visible = false;
            _pauseButtonTouch.Visible = true;
        }

        _startButton.Pressed += () =>
        {
            Start(); // TODO 倒计时开始
        };

        _quitButton.Pressed += () =>
        {
            // 返回编辑器界面
            var global = GetNode<Global>("/root/Global");
            global.GotoScene("res://Scene/editor_scene.tscn");
        };

        LoadChart();
        if (_chart == null) return;

        var chartInfo = _chartService.GetChartInfo(chartId);
        if (chartInfo == null)
        {
            GD.PrintErr($"[{Name}] 未找到谱面 {chartId}");
            return;
        }

        _judge = new ChartJudge();

        chartPlayer.UseDefaultResource();
        chartRenderer.UseDefaultResource();

        chartPlayer.Initialize(parent, _chart, Image.LoadFromFile(chartInfo.PicturePath), FileUtil.LoadAudioFromFile(chartInfo.SongPath));
        chartRenderer.Initialize(parent);

        
        AddChild(_judge);
        _judge.Initialize(chartPlayer, parent, _chart);
        _judge.OnJudgeResult += OnJudgeResult;
        _judge.OnHoldEndJudgeResult += OnHoldEndJudgeResult;


        chartPlayer.AutoHitEnabled = false;
        
        SetIsPlaying(true);
    }

    public override void _ExitTree()
    {
        _judge.OnJudgeResult -= OnJudgeResult;
        _judge.OnHoldEndJudgeResult -= OnHoldEndJudgeResult;
        _judge = null;

        parent.Clicked -= OnClickInput;
        parent.Touched -= OnTouchInput;
        parent.Flicked -= OnFlickInput;


        base._ExitTree();
    }


    public override void _Process(double delta)
    {
        base._Process(delta);

        if (_chart == null || chartPlayer == null || _judge == null)
            return;

        if (_isPlaying)
        {
            // 处理暂停按钮计时器
            if (_isPauseActive)
            {
                _pauseTimer += (float)delta;
                if(_pauseTimer > 2)
                {
                    // 超时，取消响应
                    _isPauseActive = false;
                    _pauseButtonTouch.Modulate = new Color(1, 1, 1, 0.6f);
                    _pauseTimer = 0;
                }
            }

            chartPlayer.UpdateLogic(delta);
            _gameTime = chartPlayer.ChartTime;

            _judge.Update(chartPlayer.ChartTime, delta);
        }

        var (lineData, lineCount) = chartPlayer.GetLineRenderDatas();
        var (noteData, noteCount) = chartPlayer.GetNoteRenderDatas();
        chartRenderer.Render(lineData, lineCount, noteData, noteCount);
    }

    private void OnClickInput(Vector2 pos)
    {
        if (_chart == null || chartPlayer == null || _judge == null) return;
        if (!chartPlayer.IsPlaying) return;

        // GD.Print($"Click {_gameTime:F2}, {pos}");
        _judge.OnTapInput(pos, _gameTime);
    }

    private void OnTouchInput(Vector2 pos)
    {
        if (_chart == null || chartPlayer == null || _judge == null) return;
        if (!chartPlayer.IsPlaying) return;

        _judge.OnTouchInput(pos, _gameTime);
    }

    private void OnFlickInput(Vector2 pos)
    {
        if (_chart == null || chartPlayer == null || _judge == null) return;
        if (!chartPlayer.IsPlaying) return;

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
        //GD.Print($"{result.Grade} {result.TimeDeltaMs:F0}ms");

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
        //GD.Print($"{result.Grade} {result.TimeDeltaMs:F0}ms");
        
        if (statusLabel != null)
        {
            // if(result.Grade != JudgeGrade.Miss)
            {
                statusLabel.Text = $"{result.Grade} {result.TimeDeltaMs:F0}ms";
            }
            
        }
    }

    private void SetIsPlaying(bool value)
    {
        _isPlaying = value;
        if(value) chartPlayer.Play((float)_gameTime);
        else chartPlayer.Pause();

        Input.EmulateMouseFromTouch = !value;
    }

    private void Pause()
    {
        SetIsPlaying(false);
        _pauseLayer.Visible = true;
    }

    private void Start()
    {
        SetIsPlaying(true);
        _pauseLayer.Visible = false;
    }

    private void Quit()
    {
        
    }

    private void Restart()
    {
        
    }
}
