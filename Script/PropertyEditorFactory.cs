using System;
using Godot;
using HPhiEditorGame.Editor;
using QuickType;

public static class PropertyEditorFactory
{
    public static IPropertyEditor Create(Type type) => Create(type, null);

    public static IPropertyEditor Create(Type type, FieldOptions options)
    {
        NumericFieldOptions num = options as NumericFieldOptions;

        if (type == typeof(string))   return new StringEditor();
        if (type == typeof(int))      return new IntEditor(num);
        if (type == typeof(long))     return new LongEditor(num);
        if (type == typeof(short))    return new LongEditor(num);
        if (type == typeof(byte))     return new LongEditor(num);
        if (type == typeof(float))    return new FloatEditor(num);
        if (type == typeof(double))   return new DoubleEditor(num);
        if (type == typeof(decimal))  return new DoubleEditor(num);
        if (type == typeof(bool))     return new BoolEditor();
        if (type == typeof(Beat))     return new BeatEditor();
        if (type == typeof(EasingData)) return new EasingEditor();
        if (type == typeof(NoteType)) return new NoteTypeEditor();

        throw new NotSupportedException($"不支持的属性类型: {type}");
    }

    public static IPropertyEditor<T> Create<T>(FieldOptions options = null)
        => (IPropertyEditor<T>)Create(typeof(T), options);
}