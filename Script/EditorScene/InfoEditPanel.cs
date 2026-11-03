using Godot;
using HPhiEditorGame.Editor;
using System;
using System.Collections.Generic;

public partial class InfoEditPanel : Control
{
    [Export] private Theme _theme;
    [Export] private VBoxContainer _vBoxContainer;
    [Export] private Label _nameLabel;
    [Export] private Button _confirmButton;

    public Label TitleLabel => _nameLabel;
    public Button ConfirmButton => _confirmButton;

    [Signal] public delegate void OnConfirmedEventHandler();

    private CompositeField _root;

    public override void _Ready()
    {
        base._Ready();
        _confirmButton.ButtonUp += () => EmitSignal(SignalName.OnConfirmed);
    }

    /// <summary>打开面板：设置标题并清空所有字段。</summary>
    public void BeginEdit(string content)
    {
        _nameLabel.Text = $"正在编辑:{content}";
        ClearFields();
    }

    /// <summary>更新标题（不清空字段）。撤销重做后重排索引时使用。</summary>
    public void UpdateTitle(string content)
    {
        _nameLabel.Text = $"正在编辑:{content}";
    }

    /// <summary>
    /// 添加一个字段。<paramref name="options"/> 由业务层决定，把范围/步长传给编辑器；
    /// <paramref name="onChanged"/> 是字段变更回调（不做转发分发，直接绑定）。
    /// </summary>
    public void AddField<T>(
        string label,
        Func<T> readValue,
        Action<object> onChanged = null,
        FieldOptions options = null)
    {
        EnsureRoot();

        IPropertyEditor<T> editor = PropertyEditorFactory.Create<T>(options);
        editor.Setup(label, () => readValue());
        editor.SetValueSilently(readValue());

        if (onChanged != null)
            editor.ValueChanged += change => onChanged(change.Value);

        _root.AddField(editor);
    }

    /// <summary>批量添加：每个构建器依次向面板添加字段。</summary>
    public void AddFields(params Action<InfoEditPanel>[] builders)
    {
        if (builders == null) return;
        foreach (var build in builders) build?.Invoke(this);
    }

    /// <summary>清空所有字段。</summary>
    public void ClearFields()
    {
        _root = null;

        foreach (Node child in _vBoxContainer.GetChildren())
        {
            _vBoxContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>静默刷新：所有字段回填为绑定数据的当前值，不触发变更回调。</summary>
    public void ApplyToUI()
    {
        _root?.ApplyToUI();
    }

    private void EnsureRoot()
    {
        if (_root != null) return;
        _root = new CompositeField();
        _root.View.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _vBoxContainer.AddChild(_root.View);
    }
}