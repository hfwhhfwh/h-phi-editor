using System;

/// <summary>
/// 字段编辑器配置基类。由最上层业务面板在 AddField 时传入，
/// 把"业务约束"（范围、步长等）与"字段类型"解耦。
/// </summary>
public abstract class FieldOptions { }

/// <summary>
/// 数值类字段的配置：范围与步长。
/// 适用于 int / long / short / byte / float / double / decimal / Beat 等。
/// </summary>
public class NumericFieldOptions : FieldOptions
{
    public double MinValue { get; set; } = double.NegativeInfinity;
    public double MaxValue { get; set; } = double.PositiveInfinity;
    public double Step { get; set; } = 0.1;
}

/// <summary>兼容旧代码。请改用 NumericFieldOptions。</summary>
[Obsolete("请使用 NumericFieldOptions")]
public class FloatEditorOptions : NumericFieldOptions { }