using Godot;

/// <summary>
/// 编辑面板的 GUI 输入过滤器。
///
/// 从 BaseEditPanel 中抽出的职责：把 <c>_GuiInput</c> 收到的事件筛成
/// 「左键 / 拖拽 / 触摸」，转交给 InputController，并告诉调用方是否需要
/// 调用 AcceptEvent 阻止事件继续冒泡。
///
/// 说明：普通 C# 类，不占场景树节点。
/// </summary>
public sealed class PanelInputHandler
{
    private readonly InputController _inputController;

    public PanelInputHandler(InputController inputController)
    {
        _inputController = inputController;
    }

    /// <summary>
    /// 过滤并转发一个 GUI 事件
    /// </summary>
    /// <returns>是否已处理该事件（true 时调用方应 AcceptEvent）</returns>
    public bool ProcessGuiInput(InputEvent @event)
    {
        if (@event.Device == -1) return false; // 拦截模拟输入

        // 只处理左键和触摸，其余事件（滚轮、中键）忽略
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            _inputController.ProcessEvent(@event);
            return true;
        }

        if (@event is InputEventMouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _inputController.ProcessEvent(@event);
            return true;
        }

        if (@event is InputEventScreenTouch)
        {
            _inputController.ProcessEvent(@event);
            return true;
        }

        if (@event is InputEventScreenDrag)
        {
            _inputController.ProcessEvent(@event);
            return true;
        }

        return false;
    }
}
