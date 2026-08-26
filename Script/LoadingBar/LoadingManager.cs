using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class LoadingManager : Node
{
    public static LoadingManager Instance;

    private PackedScene _loadingBarScene;
    private LoadingBar _loadingBar;

    public override void _Ready()
    {
        base._Ready();

        // ========== 单例保护 ==========
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            GD.PushWarning($"[{Name}] 单例已存在（{Instance.Name}），销毁当前重复实例");
            QueueFree();  // 自杀，保留旧实例
            return;
        }

        Instance = this;
        // =============================

        _loadingBarScene = GD.Load<PackedScene>("res://Scene/loading_bar.tscn");
        _loadingBar = _loadingBarScene.Instantiate<LoadingBar>();
        _loadingBar.Visible = false;
        AddChild(_loadingBar);
        

    }

    public override void _ExitTree()
    {
        // 先清自己的引用，再交给 base
        if (Instance == this)
        {
            Instance = null;
        }
        base._ExitTree();
    }

    /// <summary>
    /// 顺序执行一系列带描述的异步任务，自动更新进度条
    /// </summary>
    public async Task RunTasksAsync(string title, List<(string Description, Func<Task> Work)> steps)
    {
        _loadingBar.Visible = true;

        _loadingBar.Title = title;

        int total = steps.Count;

        for (int i = 0; i < total; i++)
        {
            (string desc, Func<Task> work) = steps[i];
            
            _loadingBar.SetProgress(i, total, desc); // 开始这一步前先更新文字
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            
            await work(); // 执行实际任务
            
            _loadingBar.SetProgress(i + 1, total, desc);
        }

        await ToSignal(GetTree().CreateTimer(0.2), Timer.SignalName.Timeout);
        _loadingBar.Visible = false;
    }

    // public void RunTasks(string title, List<(string Description, Action Work)> steps)
    // {
    //     _loadingBar.Visible = true;

    //     _loadingBar.Title = title;

    //     int total = steps.Count;

    //     for (int i = 0; i < total; i++)
    //     {
    //         (string desc, Action work) = steps[i];
            
    //         _loadingBar.SetProgress(i, total, desc); // 开始这一步前先更新文字
    
    //         work(); // 执行实际任务 阻塞
            
    //         _loadingBar.SetProgress(i + 1, total, desc);
    //     }

    //     //await ToSignal(GetTree().CreateTimer(0.2), Timer.SignalName.Timeout);
    //     _loadingBar.Visible = false;
    // }
}