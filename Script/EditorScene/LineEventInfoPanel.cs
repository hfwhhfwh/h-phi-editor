using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using HPhiEditorGame.Editor;

public partial class LineEventInfoPanel : Panel
{
    [Export] private InfoEditPanel _infoEditPanel;

    private int _lineId;
    private int _eventLayer;
    private LineEventEnum _eventType;
    private int _eventIndex;
    private LineEvent _lineEvent;
    private EasingData _lastEasing;

    private EditorContext _context;
    private ChartEditService _editService;
    private bool _refreshing;

    [Signal] public delegate void OnConfirmedEventHandler();

    public void Initialize(EditorContext context, ChartEditService editService)
    {
        _context = context;
        _editService = editService;
        if (_editService != null) _editService.HistoryChanged += OnHistoryChanged;
    }

    public override void _Ready()
    {
        _infoEditPanel.OnConfirmed += () => EmitSignal(SignalName.OnConfirmed);
    }

    public override void _ExitTree()
    {
        if (_editService != null) _editService.HistoryChanged -= OnHistoryChanged;
        base._ExitTree();
    }

    public void Edit(LineEvent lineEvent, int lineId, int layer, LineEventEnum type, int index)
    {
        _lineEvent = lineEvent;
        _lineId = lineId;
        _eventLayer = layer;
        _eventType = type;
        _eventIndex = index;

        (float minValue, float maxValue, float step) = GetRange(type);

        _infoEditPanel.BeginEdit($"事件{type}_{index}");

        _infoEditPanel.AddField("StartTime",
            () => new Beat(_lineEvent.StartTime),
            v => NotifyPropertyChanged(LineEventPropertyType.StartTime, v));

        _infoEditPanel.AddField("EndTime",
            () => new Beat(_lineEvent.EndTime),
            v => NotifyPropertyChanged(LineEventPropertyType.EndTime, v));

        var range = new NumericFieldOptions { MinValue = minValue, MaxValue = maxValue, Step = step };

        _infoEditPanel.AddField("Start",
            () => _lineEvent.Start,
            v => NotifyPropertyChanged(LineEventPropertyType.Start, v),
            range);

        _infoEditPanel.AddField("End",
            () => _lineEvent.End,
            v => NotifyPropertyChanged(LineEventPropertyType.End, v),
            range);

        _lastEasing = BuildEasingData().Duplicate();

        _infoEditPanel.AddField("Easing",
            () => BuildEasingData(),
            v => HandleEasingChanged((EasingData)v));
    }

    private static (float min, float max, float step) GetRange(LineEventEnum type) => type switch
    {
        LineEventEnum.MoveX   => (-675f, 675f, 0.1f),
        LineEventEnum.MoveY   => (-450f, 450f, 0.1f),
        LineEventEnum.Rotate  => (0f,   360f, 0.1f),
        LineEventEnum.Alpha   => (0f,   255f, 0.1f),
        LineEventEnum.Speed   => (0f,    20f, 0.1f),
        _                     => (0f,     0f, 0.1f)
    };

    private void NotifyPropertyChanged(LineEventPropertyType prop, object value)
    {
        _editService?.SetEventProperty(_lineId, _eventLayer, _eventType, _eventIndex, prop, value);
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

    private void HandleEasingChanged(EasingData neo)
    {
        bool funcOrIO = neo.EasingFunc != _lastEasing.EasingFunc
                     || neo.EasingIO != _lastEasing.EasingIO;

        if (funcOrIO)
        {
            int type = EasingHelper.Convert.EasingToNumber(neo.EasingFunc, neo.EasingIO);
            int validType = type == -1 ? 1 : type;
            NotifyPropertyChanged(LineEventPropertyType.EasingType, validType);
        }
        if (neo.EasingLeft != _lastEasing.EasingLeft)
            NotifyPropertyChanged(LineEventPropertyType.EasingLeft, neo.EasingLeft);
        if (neo.EasingRight != _lastEasing.EasingRight)
            NotifyPropertyChanged(LineEventPropertyType.EasingRight, neo.EasingRight);

        _lastEasing = neo.Duplicate();
    }

    private void OnHistoryChanged()
    {
        if (_refreshing || !Visible || _lineEvent == null) return;

        int index = IndexOfEditedEvent();
        if (index < 0) { Visible = false; return; }

        _eventIndex = index;
        _infoEditPanel.UpdateTitle($"事件{_eventType}_{index}");

        _refreshing = true;
        try
        {
            _infoEditPanel.ApplyToUI();
            _lastEasing = BuildEasingData().Duplicate();
        }
        finally
        {
            _refreshing = false;
        }
    }

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
}