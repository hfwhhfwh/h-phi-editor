using Godot;
using System;

public partial class PopupHelper : Node
{
    public static PopupHelper Instance;

    private Theme _theme;

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

        _theme = GD.Load<Theme>("res://theme_gray.tres");
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

    // 显示一个确认弹窗
    public void ShowAlert(string title, string message)
    {
        var dialog = new AcceptDialog();
        AddChild(dialog);
        
        dialog.Title = title;
        dialog.DialogText = message;
        dialog.Theme = _theme;

        dialog.Confirmed += () => dialog.QueueFree(); // 点击确定后销毁
        
        dialog.PopupCentered();
    }

    // 显示带"是/否"的确认弹窗
    public void ShowConfirm(string title, string message, Action onConfirm, Action onCancel)
    {
        var dialog = new ConfirmationDialog();
        AddChild(dialog);
        
        dialog.Title = title;
        dialog.DialogText = message;
        dialog.Theme = _theme;
        
        dialog.Confirmed += () =>
        {
            GD.Print("用户点击了 确定");
            onConfirm?.Invoke();
            dialog.QueueFree();
        };
        
        dialog.Canceled += () =>
        {
            GD.Print("用户点击了 取消");
            onCancel?.Invoke();
            dialog.QueueFree();
        };
        
        dialog.PopupCentered();
    }
}
