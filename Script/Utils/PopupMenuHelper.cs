using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class PopupMenuHelper
{
    private static Theme theme;

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
    public static PopupMenu ShowPopupMenu(Node parent, Vector2 position, List<PopupMenuItem> items)
    {
        // 创建 PopupMenu 实例
        PopupMenu menu = new PopupMenu();
        menu.InitialPosition = Window.WindowInitialPosition.Absolute; // 确保位置生效
        parent.AddChild(menu); // 添加到场景树
        menu.Theme = theme; // 设置样式

        // 构建菜单项
        foreach (var item in items)
        {
            if (item.IsSeparator)
                menu.AddSeparator();
            else
                menu.AddItem(item.Text);
        }

        // 绑定点击事件
        menu.IdPressed += (id) =>
        {
            int index = (int)id;
            // 确保索引有效且不是分隔符（分隔符没有回调）
            if (index >= 0 && index < items.Count && items[index]?.Callback != null)
            {
                items[index].Callback.Invoke();
            }
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

    public static void SetMenuButton(MenuButton menuButton, List<PopupMenuItem> items)
    {
        PopupMenu popupMenu = menuButton.GetPopup();

        popupMenu.Clear();

        // 构建菜单项
        foreach (var item in items)
        {
            if (item.IsSeparator)
                popupMenu.AddSeparator();
            else
                popupMenu.AddItem(item.Text);
        }

        // 绑定点击事件
        popupMenu.IdPressed += (id) =>
        {
            int index = (int)id;
            // 确保索引有效且不是分隔符（分隔符没有回调）
            if (index >= 0 && index < items.Count && items[index]?.Callback != null)
            {
                items[index].Callback.Invoke();
            }
            // 菜单自动隐藏，不能需要移除节点
        };
        
    }
}
