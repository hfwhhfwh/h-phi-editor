using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class PopupMenuHelper : Node
{
    public static PopupMenuHelper Instance;
    private static Theme theme;

    public override void _Ready()
    {
        // ========== 单例保护 ==========
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            GD.PushWarning($"[{Name}] 单例已存在（{Instance.Name}），销毁当前重复实例");
            QueueFree();  // 自杀，保留旧实例
            return;
        }

        Instance = this;
        // =============================

        theme = GD.Load<Theme>("res://theme_gray.tres");
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


    public static void SetTheme(Theme theme)
    {
        PopupMenuHelper.theme = theme;
    }

    /// <summary>
    /// 在指定位置弹出上下文菜单
    /// </summary>
    /// <param name="parent">需要将菜单添加到的父节点（通常是当前场景的根节点）</param>
    /// <param name="position">屏幕坐标（全局鼠标位置）</param>
    /// <param name="items">菜单项列表</param>
    public PopupMenu ShowPopupMenu(Node parent, Vector2 position, List<PopupMenuItem> items)
    {
        // 创建 PopupMenu 实例
        PopupMenu menu = new PopupMenu();
        menu.InitialPosition = Window.WindowInitialPosition.Absolute; // 确保位置生效
        parent.AddChild(menu); // 添加到场景树
        menu.Theme = theme; // 设置样式

        SetPopupMenu(menu, items);

        // 手动绑定点击事件
        menu.IdPressed += (id) =>
        {
            // 菜单自动隐藏，但我们需要移除并释放节点
            menu.QueueFree();
        };

        // 处理菜单关闭（点击外部）时也要清理
        menu.PopupHide += () => {
            menu.QueueFree();
        };
        
        // 弹出菜单
        menu.Popup(new Rect2I((int)position.X, (int)position.Y, 0, 0));

        return menu;
    }

    public void SetMenuButton(MenuButton menuButton, List<PopupMenuItem> items)
    {
        PopupMenu popupMenu = menuButton.GetPopup();

        SetPopupMenu(popupMenu, items);
        
    }

    /// <summary>
    /// 设置PopupMenu的选项列表
    /// 注意：此方法不会调用PopupMenu的QueueFree()
    /// </summary>
    /// <param name="popupMenu">弹出菜单</param>
    /// <param name="items">需要显示的菜单项</param>
    public void SetPopupMenu(PopupMenu popupMenu, List<PopupMenuItem> items)
    {
        popupMenu.Clear();

        // 构建菜单项
        for (int i = 0; i < items.Count; i++)
        {
            PopupMenuItem item = items[i];

            if (item.IsSeparator)
                popupMenu.AddSeparator();
            else if (item.Checkable)
            {
                popupMenu.AddCheckItem(item.Text);
                popupMenu.SetItemChecked(i, item.Checked);
            }
            else
                popupMenu.AddItem(item.Text);
        }

        // TODO 为避免重复绑定，先断开旧连接（如果你复用同一个 PopupMenu）
        // popupMenu.IdPressed -= YourHandler;   // 建议用成员变量保存handler再移除
        // 这里简单演示直接绑定（每次调用会新增，可能累积，可根据实际情况管理）
        popupMenu.IdPressed += (id) =>
        {
            int index = (int)id;
            if (index < 0 || index >= items.Count)
                return;

            var item = items[index];
            if (item == null)
                return;

            // 如果是检查项，调用 Toggled 并传入当前状态
            if (item.Checkable)
            {
                bool currentState = popupMenu.IsItemChecked(index);
                popupMenu.SetItemChecked(index, !currentState);
                item.Toggled?.Invoke(!currentState);

                //GD.Print($"index:{index}, currentState:{currentState}");
            }
            else // 如果是普通按钮，调用无参数的回调
            {
                item.Callback?.Invoke();
            }
        };

    }
}
