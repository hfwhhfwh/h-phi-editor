using System;
using System.Collections.Generic;
using Godot;
using HPhiEditorGame.Editor;
using QuickType;

public static class PropertyEditorFactory
{
    private static readonly Dictionary<Type, Func<IPropertyEditor>> _creators = new()
    {
        [typeof(string)]     = () => new StringEditor(),
        [typeof(int)]        = () => new IntEditor(),
        [typeof(long)]       = () => null,   // 需要时再补 LongEditor
        [typeof(float)]      = () => new FloatEditor(),
        [typeof(double)]     = () => null,   // 需要时再补 DoubleEditor
        [typeof(bool)]       = () => new BoolEditor(),
        [typeof(Beat)]       = () => new BeatEditor(),
        [typeof(EasingData)] = () => new EasingEditor(),
        [typeof(NoteType)]   = () => new NoteTypeEditor(),
    };

    public static IPropertyEditor Create(Type type)
    {
        if (_creators.TryGetValue(type, out var creator))
        {
            var editor = creator();
            if (editor == null) throw new NotSupportedException($"未实现的属性类型: {type}");
            return editor;
        }
        throw new NotSupportedException($"不支持的属性类型: {type}");
    }

    public static IPropertyEditor<T> Create<T>(FloatEditorOptions floatOptions = null)
    {
        if (typeof(T) == typeof(float))
            return (IPropertyEditor<T>)(IPropertyEditor)new FloatEditor(floatOptions);

        return (IPropertyEditor<T>)Create(typeof(T));
    }
}