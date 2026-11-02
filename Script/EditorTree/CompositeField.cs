using Godot;
using HPhiEditorGame.Editor;
using System;
using System.Collections.Generic;

public class CompositeField : ICompositeField
{
    private readonly VBoxContainer _view = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private readonly List<IEditorField> _children = new();
    private readonly Dictionary<IEditorField, Control> _rows = new();
    private readonly Dictionary<IEditorField, Action<EditorChange>> _handlers = new();

    public Control View => _view;
    public IReadOnlyList<IEditorField> Children => _children;
    public event Action<EditorChange> ValueChanged;

    public void AddField(IEditorField field)
    {
        string path = field is IPropertyEditor p ? p.Label : string.Empty;
        AddChild(path, field);
    }

    public void AddChild(string path, IEditorField child)
    {
        if (child == null) return;
        _children.Add(child);

        // 事件向上冒泡
        Action<EditorChange> handler = change =>
        {
            var full = new List<string>(change.Path.Count + (string.IsNullOrEmpty(path) ? 0 : 1));
            if (!string.IsNullOrEmpty(path)) full.Add(path);
            full.AddRange(change.Path);
            ValueChanged?.Invoke(new EditorChange(full, change.Value));
        };
        child.ValueChanged += handler;
        _handlers[child] = handler;

        // 配置UI
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(new Label { Text = path, CustomMinimumSize = new Vector2(80, 0) });
        child.View.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(child.View);
        _view.AddChild(row);
        _rows[child] = row;
    }

    public void RemoveField(IEditorField field)
    {
        if (field == null) return;
        if (_handlers.TryGetValue(field, out Action<EditorChange> handler))
        {
            field.ValueChanged -= handler;
            _handlers.Remove(field);
        }
        if (_rows.TryGetValue(field, out Control row))
        {
            _view.RemoveChild(row);
            row.QueueFree();
            _rows.Remove(field);
        }
        _children.Remove(field);
    }

    public void ApplyToUI()
    {
        foreach (var child in _children)
        {
            if (child is ICompositeField composite)
            {
                composite.SetValueSilently(composite.ReadValue());
                composite.ApplyToUI();
            }
            else
            {
                child.SetValueSilently(child.ReadValue());
            }
        }
    }

    public void SetValueSilently(object value) { }
    public object ReadValue() => null;
}