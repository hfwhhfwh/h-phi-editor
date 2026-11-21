using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

// 关键：给 Expression 起别名，指向 Linq
using Expr = System.Linq.Expressions.Expression;

public static class ChartInfoFieldBuilder
{
    // 不自动生成字段的属性（内部管理 / 只读 / 计算属性）
    private static readonly HashSet<string> Excluded = new()
    {
        nameof(ChartInfo.Id),
        nameof(ChartInfo.SongFileName),
        nameof(ChartInfo.PictureFileName),
        nameof(ChartInfo.ChartFileName),
        nameof(ChartInfo.Duration), // 由音频时长推导
    };

    /// <summary>把 ChartInfo 的所有可编辑属性添加到 panel 上，读写都绑定到 target。</summary>
    public static void Build(InfoEditPanel panel, ChartInfo target)
    {
        List<PropertyInfo> props = GetEditableProperties().ToList();

        foreach(var prop in props)
        {
            
        }
    }

    public static IEnumerable<PropertyInfo> GetEditableProperties()
    {
        return typeof(ChartInfo)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            // 只取自己声明的属性，排除 GodotObject 上的 NativeInstance 等
            .Where(p => p.DeclaringType == typeof(ChartInfo))
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => !Excluded.Contains(p.Name))
            .Where(p => p.GetCustomAttribute<ChartInfoFieldAttribute>()?.ReadOnly != true)
            .OrderBy(p => p.MetadataToken); // 按源码声明顺序
    }

    private static string GetLabel(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<ChartInfoFieldAttribute>();
        return attr?.Label ?? prop.Name;
    }

    // 生成 Func<T>：读取属性当前值（可空类型用 ?? default 兜底）
    private static Delegate BuildReader(object target, PropertyInfo prop, Type editorType)
    {
        var instance = Expr.Constant(target, prop.DeclaringType);
        Expr access = Expr.Property(instance, prop);

        if (prop.PropertyType != editorType) // Nullable<T> → T
            access = Expr.Coalesce(access, Expr.Default(editorType));

        Type funcType = typeof(Func<>).MakeGenericType(editorType);
        return Expr.Lambda(funcType, access).Compile();
    }

    // 生成 Action<object>：把值写回属性（含 Nullable<T> 装箱）
    private static Action<object> BuildWriter(object target, PropertyInfo prop, Type editorType)
    {
        var instance = Expr.Constant(target, prop.DeclaringType);
        var value    = Expr.Parameter(typeof(object), "value");

        Expr converted = Expr.Convert(value, editorType);
        if (prop.PropertyType != editorType) // T → Nullable<T>
            converted = Expr.Convert(converted, prop.PropertyType);

        Expr assign = Expr.Assign(Expr.Property(instance, prop), converted);
        Expr body   = Expr.Block(assign, Expr.Empty()); // 包成 void 以匹配 Action<object>

        return Expr.Lambda<Action<object>>(body, value).Compile();
    }

    private static FieldOptions BuildOptions(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<ChartInfoFieldAttribute>();
        if (attr == null) return null;

        // ⚠️ 按你的 FieldOptions 实际 API 填写，例如：
        // return new FieldOptions { Min = (float)attr.Min, Max = (float)attr.Max, Step = (float)attr.Step };
        return null;
    }
}