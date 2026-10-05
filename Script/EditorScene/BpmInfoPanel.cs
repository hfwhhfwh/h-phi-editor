using Godot;
using HPhiEditorGame.Editor;
using QuickType;
using System;
using System.Collections.Generic;

public partial class BpmInfoPanel : PanelContainer
{
	[Export] private VBoxContainer _container;
	[Export] private Label _titleLabel;
	[Export] private Button _confirmButton;

	private BpmEvent _bpmEvent;
	private readonly List<IPropertyEditor> _editors = new();
	private readonly List<Label> _labels = new();

	/// <summary>把数据最新值静默写回各控件，用于撤销/重做后的刷新</summary>
	private readonly List<Action> _refreshers = new();

	/// <summary>场景级状态中心，用于按对象引用重新定位 BPM 事件</summary>
	private EditorContext _context;

	/// <summary>编辑命令入口，由 EditorScene 注入</summary>
	private ChartEditService _editService;

	/// <summary>防止刷新过程中再次触发刷新</summary>
	private bool _refreshing;

	[Signal] public delegate void OnConfirmedEventHandler();

	/// <summary>
	/// 注入依赖。面板不再把属性修改抛给上级转发，直接执行命令；
	/// 同时订阅历史变化，撤销/重做后把数据回填到界面。
	/// </summary>
	public void Initialize(EditorContext context, ChartEditService editService)
	{
		_context = context;
		_editService = editService;

		if (_editService != null)
		{
			_editService.HistoryChanged += OnHistoryChanged;
		}
	}

	public override void _ExitTree()
	{
		if (_editService != null)
		{
			_editService.HistoryChanged -= OnHistoryChanged;
		}

		base._ExitTree();
	}

	/// <summary>
	/// 撤销/重做（或其它来源的命令）之后，把谱面数据的最新值同步到界面
	/// </summary>
	private void OnHistoryChanged()
	{
		if (_refreshing || !Visible || _bpmEvent == null) return;

		List<BpmEvent> bpmList = _context?.EditingChart?.BpmList;
		int index = bpmList?.IndexOf(_bpmEvent) ?? -1;
		if (index < 0)
		{
			// BPM 事件已被删除，面板没有可编辑对象了
			Visible = false;
			return;
		}

		_titleLabel.Text = $"正在编辑: BPM事件{index}";

		_refreshing = true;
		try
		{
			foreach (Action refresh in _refreshers) refresh();
		}
		finally
		{
			_refreshing = false;
		}
	}

	public override void _Ready()
	{
		_confirmButton.ButtonUp += () => EmitSignal(SignalName.OnConfirmed);
	}

	public void Edit(BpmEvent bpmEvent, int index)
	{
		_bpmEvent = bpmEvent;
		ClearEditors();
		_titleLabel.Text = $"正在编辑: BPM事件{index}";

		// 传入「从数据读当前值」的委托，撤销/重做后据此静默回填控件
		AddField("Bpm", () => _bpmEvent.Bpm,
			new FloatEditorOptions { MinValue = 0.01f, MaxValue = 1000f, Step = 0.1f });
		AddField("StartTime", () => new Beat(_bpmEvent.StartTime), null);
	}

	private void AddField<T>(string label, Func<T> readValue, FloatEditorOptions floatOptions)
	{
		IPropertyEditor<T> editor = PropertyEditorFactory.Create<T>(floatOptions);
		editor.Setup(label);
		editor.Value = readValue();
		editor.TypedValueChanged += value => _editService?.SetBpmProperty(_bpmEvent, label, value);

		// 撤销/重做后静默回填：IPropertyEditor.Value 的 setter 不会触发 TypedValueChanged
		_refreshers.Add(() => editor.Value = readValue());

		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		Label fieldLabel = new Label { Text = label, CustomMinimumSize = new Vector2(100, 0) };
		row.AddChild(fieldLabel);

		Control control = editor.Control;
		control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(control);
		_container.AddChild(row);

		_editors.Add(editor);
		_labels.Add(fieldLabel);
	}

	private void ClearEditors()
	{
		_refreshers.Clear();

		foreach (IPropertyEditor editor in _editors)
		{
			Node parent = editor.Control.GetParent();
			parent?.RemoveChild(editor.Control);
			editor.Control.QueueFree();
		}
		_editors.Clear();

		foreach (Label label in _labels)
		{
			Node parent = label.GetParent();
			parent?.RemoveChild(label);
			label.QueueFree();
		}
		_labels.Clear();

		foreach (Node child in _container.GetChildren())
		{
			_container.RemoveChild(child);
			child.QueueFree();
		}
	}
}
