using System;
using System.Collections.Generic;
using Godot;
using HPhiEditorGame.Editor;
using QuickType;

public static class PropertyEditorFactory
{
    private static readonly Dictionary<Type, Func<IPropertyEditor>> _creators = new()
    {
        [typeof(string)]  = () => new StringEditor(),
        [typeof(int)]     = () => null,//new IntEditor(),
        [typeof(long)]    = () => null,//new IntEditor(),
        [typeof(float)]   = () => new FloatEditor(),
        [typeof(double)]  = () => null,//new DoubleEditor(),
        [typeof(bool)]    = () => new BoolEditor(),
        [typeof(Beat)]    = () => new BeatEditor(),
        [typeof(EasingData)] = () => new EasingEditor(),
    };

    public static IPropertyEditor Create(Type type)
    {
        if (_creators.TryGetValue(type, out var creator))
            return creator();
        throw new NotSupportedException($"不支持的属性类型: {type}");
    }

    public static IPropertyEditor<T> Create<T>(FloatEditorOptions floatOptions = null)
    {
        if (typeof(T) == typeof(float))
            return (IPropertyEditor<T>)(IPropertyEditor)new FloatEditor(floatOptions);

        return (IPropertyEditor<T>)Create(typeof(T));
    }
}