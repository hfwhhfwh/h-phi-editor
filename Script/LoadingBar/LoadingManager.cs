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
            FileLogger.LogWarning($"[{Name}] 单例已存在（{Instance.Name}），销毁当前重复实例");
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
    public async Task RunTasksAsync(string title, List<(string Description, Func<Task> Work)> steps, Action onFailed)
    {
        if(steps == null || steps.Count == 0)
        {
            throw new ArgumentException("steps 不能为空或空列表", nameof(steps));
        }
        
        _loadingBar.Visible = true;

        _loadingBar.Title = title;

        int total = steps.Count;

        try
        {
            for (int i = 0; i < total; i++)
            {
                (string desc, Func<Task> work) = steps[i];
                
                _loadingBar.SetProgress(i, total, desc); // 开始这一步前先更新文字
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                
                if(work == null)
                {
                    FileLogger.LogWarning($"[{Name}] 任务步骤 {i} 的 work 为 null: {desc}");
                    continue;
                }
                await work(); // 执行实际任务
                
                _loadingBar.SetProgress(i + 1, total, desc);
            }
        }
        catch (Exception e)
        {
            FileLogger.LogErr($"[{Name}] 任务执行失败, 错误: {e.Message}");

            // onFailed 自身抛异常时不要覆盖原始异常
            try
            {
                onFailed?.Invoke();
            }
            catch (Exception cbEx)
            {
                FileLogger.LogErr($"[{Name}] onFailed 回调执行失败: {cbEx.Message}");
            }

            throw; // 重新抛出，保留原始堆栈
        }
        finally
        {
            // 无论成功还是失败，都隐藏进度条
            _loadingBar.Visible = false;
        }
        

        // 等待一段时间，让用户看到加载结束
        await ToSignal(GetTree().CreateTimer(0.2), Timer.SignalName.Timeout);
        _loadingBar.Visible = false;
    }

    public void SetTitle(string title)
    {
        _loadingBar.Title = title;
    }

}