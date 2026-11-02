using Godot;
using System;
using System.Collections.Generic;

public readonly struct EditorChange
{
    /// <summary>相对根字段的路径片段，如 ["Easing","Left"]。叶子发出时为空。</summary>
    public IReadOnlyList<string> Path { get; }
    public object Value { get; }

    public EditorChange(IReadOnlyList<string> path, object value)
    {
        Path = path ?? Array.Empty<string>();
        Value = value;
    }
}

public interface IEditorField
{
    Control View { get; }
    event Action<EditorChange> ValueChanged;

    /// <summary>静默回填：不触发 ValueChanged。用于撤销/重做刷新。</summary>
    void SetValueSilently(object value);

    /// <summary>从绑定读取当前值，供 ApplyToUI 递归使用。</summary>
    object ReadValue();
}

public interface ICompositeField : IEditorField
{
    IReadOnlyList<IEditorField> Children { get; }
    void AddChild(string path, IEditorField child);
    void AddField(IEditorField field);
    void RemoveField(IEditorField field);

    /// <summary>递归调用子字段的 SetValueSilently(ReadValue())。</summary>
    void ApplyToUI();
}