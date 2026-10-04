using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 编辑器界面管理器。
///
/// 职责：
/// 1. 构建顶部菜单栏（文件 / 编辑 / 视图）与左上角“...”菜单；
/// 2. 绑定工具栏按钮（撤销重做、复制粘贴、多选、框选）；
/// 3. 更新标签（判定线、编辑模式、FPS）；
/// 4. 面板显隐（视图菜单开关、信息面板确认关闭）。
///
/// 约定：它只负责界面本身。修改谱面数据一律通过 ChartEditService，
/// 复制粘贴交给 EditorClipboardController，判定线增删仍由 EditorScene 协调。
/// </summary>
public partial class EditorUIManager : Node
{
    private EditorViewRefs _view;
    private EditorContext _context;
    private ChartEditService _editService;
    private EditorClipboardController _clipboardController;

    private Action _onSave;
    private Action _onSaveAndQuit;
    private Action _onQuitRequested;
    private Action _onTestPlay;

    private readonly List<Action> _unsubscribes = new();

    private int _fpsRefreshCount;

    public void Initialize(
        EditorViewRefs view,
        EditorContext context,
        ChartEditService editService,
        EditorClipboardController clipboardController,
        Action onSave,
        Action onSaveAndQuit,
        Action onQuitRequested,
        Action onTestPlay)
    {
        _view = view;
        _context = context;
        _editService = editService;
        _clipboardController = clipboardController;

        _onSave = onSave;
        _onSaveAndQuit = onSaveAndQuit;
        _onQuitRequested = onQuitRequested;
        _onTestPlay = onTestPlay;

        BindLabels();
        BindInfoPanels();
        BuildMenuBar();
        BindToolbarButtons();
        BindNoteChooser();
        ApplyInitialPanelState();
    }

    public override void _ExitTree()
    {
        UnsubscribeAll();

        base._ExitTree();
    }

    /// <summary>
    /// FPS 显示（放这里是为了让 EditorScene 不再需要 _PhysicsProcess）
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        _fpsRefreshCount++;
        if (_fpsRefreshCount > 15)
        {
            _fpsRefreshCount = 0;

            if (_view?.FpsLabel != null)
            {
                _view.FpsLabel.Text = $"FPS:{Performance.GetMonitor(Performance.Monitor.TimeFps)}";
            }
        }
    }

    // ==================== 订阅辅助 ====================

    private void Subscribe<THandler>(THandler handler, Action<THandler> add, Action<THandler> remove)
        where THandler : Delegate
    {
        add(handler);
        _unsubscribes.Add(() => remove(handler));
    }

    private void UnsubscribeAll()
    {
        foreach (Action u in _unsubscribes) u();
        _unsubscribes.Clear();
    }

    // ==================== 标签 ====================

    private void BindLabels()
    {
        Subscribe(
            OnEditingLineChanged,
            h => _context.EditingLineChanged += h,
            h => _context.EditingLineChanged -= h);

        Subscribe(
            OnEditModeChanged,
            h => EditModeManager.OnEditModeChanged += h,
            h => EditModeManager.OnEditModeChanged -= h);

        _view.EditingLineLabel.Text = $"线{_context.EditingLineId}";
        UpdateEditModeLabel(EditModeManager.EditMode);

        // 初始状态默认为常规模式（上一次会话可能停在放置模式）
        EditModeManager.SetEditMode(EditModeEnum.Normal);
        UpdateEditModeLabel(EditModeManager.EditMode);
    }

    private void OnEditingLineChanged(int lineId)
    {
        _view.EditingLineLabel.Text = $"线{lineId}";
    }

    private void OnEditModeChanged(EditModeEnum editMode)
    {
        UpdateEditModeLabel(editMode);
    }

    private void UpdateEditModeLabel(EditModeEnum editMode)
    {
        _view.EditModeLabel.Text = editMode switch
        {
            EditModeEnum.Normal => "模式：常规模式",
            EditModeEnum.Place => "模式：放置模式",
            // EditModeEnum.Delete => "模式：删除模式",
            _ => "模式：未知",
        };
    }

    // ==================== 信息面板 ====================

    private void BindInfoPanels()
    {
        _view.NoteInfoPanel.OnConfirmed += OnNoteInfoConfirmed;
        _view.EventInfoPanel.OnConfirmed += OnEventInfoConfirmed;
        _view.BpmInfoPanel.OnConfirmed += OnBpmInfoConfirmed;
    }

    private void OnNoteInfoConfirmed()
    {
        _view.NoteInfoPanel.Visible = false;
    }

    private void OnEventInfoConfirmed()
    {
        _view.EventInfoPanel.Visible = false;
    }

    private void OnBpmInfoConfirmed()
    {
        _view.BpmInfoPanel.Visible = false;
    }

    // ==================== 菜单栏 ====================

    private void BuildMenuBar()
    {
        PopupMenuHelper.SetTheme(_view.Theme);

        // “文件”
        PopupMenuHelper.Instance.SetMenuButton(_view.FileMenu, new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "保存", Callback = _onSave },
            //new PopupMenuItem { Text = "另存为", Callback = null},
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "保存并退出", Callback = _onSaveAndQuit },
            new PopupMenuItem { Text = "仅退出", Callback = _onQuitRequested },
        });

        // “编辑”
        PopupMenuHelper.Instance.SetMenuButton(_view.EditMenu, new List<PopupMenuItem>
        {
            new PopupMenuItem { Text = "复制", Callback = null },
            new PopupMenuItem { Text = "粘贴", Callback = null },
            new PopupMenuItem { Text = "剪切", Callback = null },
            new PopupMenuItem { IsSeparator = true },
            new PopupMenuItem { Text = "全局设置", Callback = _view.SettingsPanel.Show },
            new PopupMenuItem { Text = "编辑器设置", Callback = _view.EditorSettingsPanel.Show },
        });

        // “视图”
        PopupMenuHelper.Instance.SetMenuButton(_view.ViewMenu, new List<PopupMenuItem>
        {
            new PopupMenuItem
            {
                Text = "音符面板", Checkable = true,
                Checked = _view.NoteEditPanel.Visible,
                Toggled = (bool value) => _view.NoteEditPanel.Visible = value
            },
            new PopupMenuItem
            {
                Text = "事件面板", Checkable = true,
                Checked = _view.EventEditPanel.Visible,
                Toggled = (bool value) => _view.EventEditPanel.Visible = value
            },
            new PopupMenuItem
            {
                Text = "Bpm面板", Checkable = true,
                Checked = _view.BpmEditPanel.Visible,
                Toggled = (bool value) => _view.BpmEditPanel.Visible = value
            },
        });

        // 左上角“...”按钮
        _view.OthersButton.Pressed += OnOthersButtonPressed;
    }

    private void OnOthersButtonPressed()
    {
        List<PopupMenuItem> items =
        [
            new PopupMenuItem { Text = "试玩", Callback = _onTestPlay },
        ];

        PopupMenuHelper.Instance.ShowPopupMenu(
            this,
            GetViewport().GetMousePosition() + new Vector2(30, 30),
            items);
    }

    // ==================== 工具栏按钮 ====================

    private void BindToolbarButtons()
    {
        // 撤销 / 重做
        _view.UndoButton.Pressed += OnUndoPressed;
        _view.RedoButton.Pressed += OnRedoPressed;

        // 撤销/重做按钮的可用状态跟随历史
        Subscribe(
            RefreshHistoryButtons,
            h => _editService.HistoryChanged += h,
            h => _editService.HistoryChanged -= h);
        RefreshHistoryButtons();

        // 复制 / 粘贴
        _view.CopyButton.Pressed += _clipboardController.CopySelection;
        _view.PasteButton.Pressed += _clipboardController.StartPaste;
        _view.PasteConfirmButton.Pressed += _clipboardController.ConfirmPaste;
        _view.PasteCancelButton.Pressed += _clipboardController.CancelPaste;

        // 多选
        _view.MultiSelectButton.ToggleMode = true;
        _view.MultiSelectButton.Toggled += OnMultiSelectToggled;

        // 框选
        _view.BoxSelectButton.ToggleMode = true;
        _view.BoxSelectButton.Toggled += OnBoxSelectToggled;
    }

    private void OnUndoPressed()
    {
        _editService.Undo();
    }

    private void OnRedoPressed()
    {
        _editService.Redo();
    }

    private void RefreshHistoryButtons()
    {
        _view.UndoButton.Disabled = !_editService.CanUndo;
        _view.RedoButton.Disabled = !_editService.CanRedo;
    }

    private void OnMultiSelectToggled(bool value)
    {
        // 选择模式由 Context 统一持有，三个面板共享
        _context.SelectMode = value
            ? BaseEditPanel.SelectModeEnum.Multi
            : BaseEditPanel.SelectModeEnum.Single;
    }

    private void OnBoxSelectToggled(bool value)
    {
        _context.IsBoxSelectMode = value;
    }

    // ==================== NoteChooser ====================

    private void BindNoteChooser()
    {
        Subscribe(
            OnNoteChooserNoteChoosed,
            h => _view.NoteChooser.NoteChoosed += h,
            h => _view.NoteChooser.NoteChoosed -= h);

        Subscribe(
            OnNoteChooserDeselected,
            h => _view.NoteChooser.Deselected += h,
            h => _view.NoteChooser.Deselected -= h);
        // _view.NoteChooser.DeleteButtonChoosed += OnNoteChooserDeleteChoosed;
    }

    private void OnNoteChooserNoteChoosed(NoteType noteType)
    {
        EditModeManager.SetEditMode(EditModeEnum.Place);
        _view.NoteEditPanel.PlacingNote = noteType;
    }

    private void OnNoteChooserDeselected()
    {
        EditModeManager.SetEditMode(EditModeEnum.Normal);
    }

    // ==================== 初始状态 ====================

    private void ApplyInitialPanelState()
    {
        // 编辑面板初始可用；播放模式切换时 EditorPlaybackController 也会重新设置
        _view.NoteEditPanel.Disabled = false;
        _view.EventEditPanel.Disabled = false;
        _view.BpmEditPanel.Disabled = false;
    }
}
