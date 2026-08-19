using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using HPhiEditorGame.Editor;

public partial class LineEventInfoPanel : Panel
{
    [Export] private VBoxContainer _container;
    [Export] private Label _titleLabel;
    [Export] private Button _confirmButton;

    private int _lineId;
    private int _eventLayer;
    private LineEventEnum _eventType;
    private int _eventIndex;
    private LineEvent _lineEvent;
    private readonly List<IPropertyEditor> _editors = new();
    private readonly List<Label> _labels = new();
    private EasingData _lastEasing;

    [Signal] public delegate void OnConfirmedEventHandler();
    /// <summary>
    /// LineEvent属性修改时触发 参数:(判定线编号，事件层，事件类型，事件索引，属性(LineEventPropertyType)，值(object))
    /// </summary>
    public event Action<int, int, LineEventEnum, int, LineEventPropertyType, object> PropertyChanged;

    public override void _Ready()
    {
        _confirmButton.ButtonUp += () => EmitSignal(SignalName.OnConfirmed);
    }

    public void Edit(LineEvent lineEvent, int lineId, int layer, LineEventEnum type, int index)
    {
        _lineEvent = lineEvent;
        _lineId = lineId;
        _eventLayer = layer;
        _eventType = type;
        _eventIndex = index;

        // 选择滑动条的最大、最小值
        float minValue = 0, maxValue = 0;
        float step = 0.1f;
        switch(type)
        {
            case LineEventEnum.MoveX:
                minValue = -675f;
                maxValue = 675f;
                break;
            case LineEventEnum.MoveY:
                minValue = -450f;
                maxValue = 450f;
                break;
            case LineEventEnum.Rotate:
                minValue = 0f;
                maxValue = 360f;
                break;
            case LineEventEnum.Alpha:
                minValue = 0f;
                maxValue = 255f;
                break;
            case LineEventEnum.Speed:
                minValue = 0f;
                maxValue = 20f;
                break;
        }

        ClearEditors();
        _titleLabel.Text = $"正在编辑: 事件{type}_{index}";

        // ---------- 声明字段 ----------
        AddField("StartTime", new Beat(lineEvent.StartTime),
            null, // 应该只触发事件，交给上级修改谱面数据
            LineEventPropertyType.StartTime);

        AddField("EndTime", new Beat(lineEvent.EndTime),
            null, 
            LineEventPropertyType.EndTime);

        AddField("Start", lineEvent.Start,
            null, 
            LineEventPropertyType.Start,
            floatOptions: new FloatEditorOptions{MinValue = minValue, MaxValue = maxValue, Step = step}
        );

        AddField("End", lineEvent.End,
            null, 
            LineEventPropertyType.End,
            floatOptions: new FloatEditorOptions{MinValue = minValue, MaxValue = maxValue, Step = step}
        );

        // 缓动：先拆包，编辑完再比较差异并分发事件
        (EasingFunc func, EasingIO io) = EasingHelper.Convert.NumberToEasing(lineEvent.EasingType);
        EasingData easing = new EasingData
        {
            EasingFunc = func,
            EasingIO = io,
            EasingLeft = lineEvent.EasingLeft,
            EasingRight = lineEvent.EasingRight
        };
        _lastEasing = easing.Duplicate();

        AddField("Easing", easing,
            v => HandleEasingChanged((EasingData)v),
            null); // 缓动内部自行分发子事件
    }

    /// <summary>
    /// 添加一个字段。setter 直接操作 LineEvent，保证数据流最短。
    /// </summary>
    /// <param name="label">左侧显示的名称，如 "StartTime"</param>
    /// <param name="initialValue">初始值，用于初始化编辑器显示</param>
    /// <param name="setter">一个 Action，定义"值变了之后怎么写回 LineEvent"</param>
    /// <param name="propType">对应的枚举，用于向外通知"哪个属性变了"；null 表示内部自行处理（如 Easing）</param>
    /// <param name="floatOptions">Float 字段的滑块范围配置</param>
    /// <typeparam name="T"></typeparam>
    private void AddField<T>(string label, T initialValue, Action<object> setter, LineEventPropertyType? propType, FloatEditorOptions floatOptions = null)
    {
        IPropertyEditor<T> editor = PropertyEditorFactory.Create<T>(floatOptions);
        editor.Setup(label);
        editor.Value = initialValue;

        editor.TypedValueChanged += (value) =>
        {
            setter?.Invoke(value);
            if (propType.HasValue)
                PropertyChanged?.Invoke(_lineId, _eventLayer, _eventType, _eventIndex, propType.Value, value);
        };

        // UI 行布局
        HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

        Label lbl = new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) };
        row.AddChild(lbl);

        Control ctrl = editor.Control;
        ctrl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(ctrl);

        _container.AddChild(row);
        _editors.Add(editor);
        _labels.Add(lbl);
    }

    private void HandleEasingChanged(EasingData neo)
    {
        bool funcOrIO = neo.EasingFunc != _lastEasing.EasingFunc || neo.EasingIO != _lastEasing.EasingIO;

        if (funcOrIO)
        {
            int type = EasingHelper.Convert.EasingToNumber(neo.EasingFunc, neo.EasingIO);
            int validType = type == -1 ? 1 : type;
            
            _lineEvent.EasingType = validType;
            PropertyChanged?.Invoke(_lineId, _eventLayer, _eventType, _eventIndex, LineEventPropertyType.EasingType, validType);
            
        }
        if (neo.EasingLeft != _lastEasing.EasingLeft)
        {
            _lineEvent.EasingLeft = neo.EasingLeft;
            PropertyChanged?.Invoke(_lineId, _eventLayer, _eventType, _eventIndex, LineEventPropertyType.EasingLeft, neo.EasingLeft);
        }
        if (neo.EasingRight != _lastEasing.EasingRight)
        {
            _lineEvent.EasingRight = neo.EasingRight;
            PropertyChanged?.Invoke(_lineId, _eventLayer, _eventType, _eventIndex, LineEventPropertyType.EasingRight, neo.EasingRight);
        }

        _lastEasing = neo.Duplicate();
    }

    private void ClearEditors()
    {
        foreach (IPropertyEditor e in _editors)
        {
            Node parent = e.Control.GetParent();
            parent?.RemoveChild(e.Control);
            e.Control.QueueFree();
        }
        _editors.Clear();

        foreach (Label label in _labels)
        {
            Node parent = label.GetParent();
            parent?.RemoveChild(label);
            label.QueueFree();
        }
        _labels.Clear();

        foreach (Node node in _container.GetChildren())
        {
            Node parent = node.GetParent();
            parent?.RemoveChild(node);
            node.QueueFree();
        }
    }
}