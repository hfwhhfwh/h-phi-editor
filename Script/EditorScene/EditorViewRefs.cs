using Godot;

/// <summary>
/// EditorScene 从场景文件中导出的界面节点引用集合。
///
/// 为什么需要这个类：Godot 的 <c>node_paths</c> 只能写在挂了脚本的那个节点上，
/// 所以引用必须留在 EditorScene 上；EditorScene 把它们打包成这个集合注入 EditorUIManager，
/// 避免在初始化方法上堆二十多个参数。
/// </summary>
public sealed class EditorViewRefs
{
    public Theme Theme;

    // ---- 顶部菜单栏 ----
    public MenuButton FileMenu;
    public MenuButton EditMenu;
    public MenuButton ViewMenu;
    public MenuButton HelpMenu;
    public Button OthersButton;

    // ---- 工具栏按钮 ----
    public Button UndoButton;
    public Button RedoButton;
    public Button CopyButton;
    public Button PasteButton;
    public Button MultiSelectButton;
    public Button BoxSelectButton;
    public Button PasteConfirmButton;
    public Button PasteCancelButton;

    // ---- 标签 ----
    public Label EditingLineLabel;
    public Label EditModeLabel;
    public Label FpsLabel;

    // ---- 面板 ----
    public NoteEditPanel NoteEditPanel;
    public EventEditPanel EventEditPanel;
    public BpmEditPanel BpmEditPanel;

    public NoteInfoPanel NoteInfoPanel;
    public LineEventInfoPanel EventInfoPanel;
    public BpmInfoPanel BpmInfoPanel;

    public SettingsPanel SettingsPanel;
    public EditorSettingsPanel EditorSettingsPanel;

    public NoteChooser NoteChooser;
}
