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
    private EasingData _lastEasing;

    /// <summary>根字段容器：只负责 View 布局、事件转发、ApplyToUI。</summary>
    private CompositeField _root;

    /// <summary>label → 该字段的变更处理器，由根事件按 Path[0] 分发。</summary>
    private readonly Dictionary<string, Action<EditorChange>> _fieldHandlers = new();

    /// <summary>场景级状态中心，用于按对象引用重新定位事件</summary>
    private EditorContext _context;

    /// <summary>编辑命令入口，由 EditorScene 注入</summary>
    private ChartEditService _editService;

    /// <summary>防止刷新过程中再次触发刷新（缓动控件在数据非法时会回写数据）</summary>
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
        if (_editService != null) _editService.HistoryChanged += OnHistoryChanged;
    }

    public override void _ExitTree()
    {
        if (_editService != null) _editService.HistoryChanged -= OnHistoryChanged;
        base._ExitTree();
    }
    /// <summary>
    /// 把属性修改提交给 ChartEditService
    /// </summary>
    private void NotifyPropertyChanged(LineEventPropertyType propertyType, object value)
    {
        _editService?.SetEventProperty(_lineId, _eventLayer, _eventType, _eventIndex, propertyType, value);
    }

    /// <summary>
    /// 撤销/重做（或其它来源的命令）之后，把谱面数据的最新值同步到界面。
    /// 时间类属性会让事件在列表中重排，因此这里按对象引用重新定位索引。
    /// </summary>
    private void OnHistoryChanged()
    {
        if (_refreshing || !Visible || _lineEvent == null) return;

        int index = IndexOfEditedEvent();
        if (index < 0)
        {
            // 事件已被删除（或重做了一次删除），面板没有可编辑对象了
            Visible = false;
            return;
        }

        _eventIndex = index;
        _titleLabel.Text = $"正在编辑: 事件{_eventType}_{_eventIndex}";

        _refreshing = true;
        try
        {
            RefreshEditors();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// 按对象引用查找当前事件在列表中的索引，找不到返回 -1
    /// </summary>
    private int IndexOfEditedEvent()
    {
        Chart chart = _context?.EditingChart;
        if (chart?.JudgeLineList == null) return -1;
        if (_lineId < 0 || _lineId >= chart.JudgeLineList.Count) return -1;

        List<EventLayer> layers = chart.JudgeLineList[_lineId].EventLayers;
        if (layers == null || _eventLayer < 0 || _eventLayer >= layers.Count) return -1;

        List<LineEvent> list = layers[_eventLayer].GetLineEvents(_eventType);
        return list?.IndexOf(_lineEvent) ?? -1;
    }

    /// <summary>
    /// 把所有控件刷新为数据当前值（静默，不会产生新的命令）
    /// </summary>
    private void RefreshEditors()
    {
        _root?.ApplyToUI();

        // 缓动控件内部持有数据副本，回填后要同步比较基准
        _lastEasing = BuildEasingData().Duplicate();
    }

    private EasingData BuildEasingData()
    {
        (EasingFunc func, EasingIO io) = EasingHelper.Convert.NumberToEasing(_lineEvent.EasingType);
        return new EasingData
        {
            EasingFunc = func,
            EasingIO = io,
            EasingLeft = _lineEvent.EasingLeft,
            EasingRight = _lineEvent.EasingRight
        };
    }

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

        float minValue = 0, maxValue = 0;
        float step = 0.1f;
        switch (type)
        {
            case LineEventEnum.MoveX: minValue = -675f; maxValue = 675f; break;
            case LineEventEnum.MoveY: minValue = -450f; maxValue = 450f; break;
            case LineEventEnum.Rotate: minValue = 0f; maxValue = 360f; break;
            case LineEventEnum.Alpha: minValue = 0f; maxValue = 255f; break;
            case LineEventEnum.Speed: minValue = 0f; maxValue = 20f; break;
        }

        ClearEditors();
        _titleLabel.Text = $"正在编辑: 事件{type}_{index}";

        _root = new CompositeField();
        _root.View.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _root.ValueChanged += OnRootValueChanged;
        _container.AddChild(_root.View);

        AddField("StartTime", () => new Beat(_lineEvent.StartTime),
            null, LineEventPropertyType.StartTime);

        AddField("EndTime", () => new Beat(_lineEvent.EndTime),
            null, LineEventPropertyType.EndTime);

        AddField("Start", () => _lineEvent.Start,
            null, LineEventPropertyType.Start,
            floatOptions: new FloatEditorOptions { MinValue = minValue, MaxValue = maxValue, Step = step });

        AddField("End", () => _lineEvent.End,
            null, LineEventPropertyType.End,
            floatOptions: new FloatEditorOptions { MinValue = minValue, MaxValue = maxValue, Step = step });

        _lastEasing = BuildEasingData().Duplicate();

        AddField("Easing", BuildEasingData,
            v => HandleEasingChanged((EasingData)v),
            null);
    }

    /// <summary>根字段事件分发：Path[0] 是字段 label。</summary>
    private void OnRootValueChanged(EditorChange change)
    {
        if (change.Path == null || change.Path.Count == 0) return;
        if (_fieldHandlers.TryGetValue(change.Path[0], out var handler))
            handler(change);
    }

    /// <summary>
    /// 添加一个字段。setter 直接操作 LineEvent，保证数据流最短。
    /// </summary>
    /// <param name="label">左侧显示的名称，如 "StartTime"</param>
    /// <param name="readValue">从数据读出当前值；既用于初始化，也用于撤销/重做后的回填</param>
    /// <param name="setter">一个 Action，定义"值变了之后怎么写回 LineEvent"</param>
    /// <param name="propType">对应的枚举，用于向外通知"哪个属性变了"；null 表示内部自行处理（如 Easing）</param>
    /// <param name="floatOptions">Float 字段的滑块范围配置</param>
    /// <typeparam name="T"></typeparam>
    private void AddField<T>(string label, Func<T> readValue, Action<object> setter,
        LineEventPropertyType? propType, FloatEditorOptions floatOptions = null)
    {
        IPropertyEditor<T> editor = PropertyEditorFactory.Create<T>(floatOptions);
        editor.Setup(label, () => readValue());
        editor.SetValueSilently(readValue());
        _root.AddField(editor);

        _fieldHandlers[label] = (EditorChange change) =>
        {
            setter?.Invoke(change.Value);
            if (propType.HasValue)
                NotifyPropertyChanged(propType.Value, change.Value);
        };
    }

    private void HandleEasingChanged(EasingData neo)
    {
        bool funcOrIO = neo.EasingFunc != _lastEasing.EasingFunc || neo.EasingIO != _lastEasing.EasingIO;

        if (funcOrIO)
        {
            int type = EasingHelper.Convert.EasingToNumber(neo.EasingFunc, neo.EasingIO);
            int validType = type == -1 ? 1 : type;
            NotifyPropertyChanged(LineEventPropertyType.EasingType, validType);
        }
        if (neo.EasingLeft != _lastEasing.EasingLeft)
        {
            NotifyPropertyChanged(LineEventPropertyType.EasingLeft, neo.EasingLeft);
        }
        if (neo.EasingRight != _lastEasing.EasingRight)
        {
            NotifyPropertyChanged(LineEventPropertyType.EasingRight, neo.EasingRight);
        }

        _lastEasing = neo.Duplicate();
    }

    private void ClearEditors()
    {
        _fieldHandlers.Clear();
        _lastEasing = null;
        _root = null;

        foreach (Node node in _container.GetChildren())
        {
            _container.RemoveChild(node);
            node.QueueFree();
        }
    }
}