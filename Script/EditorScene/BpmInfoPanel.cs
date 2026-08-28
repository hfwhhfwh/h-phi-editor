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

	[Signal] public delegate void OnConfirmedEventHandler();
	public event Action<BpmEvent, string, object> PropertyChanged;

	public override void _Ready()
	{
		_confirmButton.ButtonUp += () => EmitSignal(SignalName.OnConfirmed);
	}

	public void Edit(BpmEvent bpmEvent, int index)
	{
		_bpmEvent = bpmEvent;
		ClearEditors();
		_titleLabel.Text = $"正在编辑: BPM事件{index}";

		AddField("Bpm", bpmEvent.Bpm,
			new FloatEditorOptions { MinValue = 0.01f, MaxValue = 1000f, Step = 0.1f });
		AddField("StartTime", new Beat(bpmEvent.StartTime), null);
	}

	private void AddField<T>(string label, T initialValue, FloatEditorOptions floatOptions)
	{
		IPropertyEditor<T> editor = PropertyEditorFactory.Create<T>(floatOptions);
		editor.Setup(label);
		editor.Value = initialValue;
		editor.TypedValueChanged += value => PropertyChanged?.Invoke(_bpmEvent, label, value);

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
