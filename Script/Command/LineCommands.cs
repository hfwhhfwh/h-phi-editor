// File: Editor/Commands/LineCommands.cs
using System.Collections.Generic;
using QuickType;

public class AddLineCommand : IEditCommand
{
    private readonly int _insertAt;
    private JudgeLine _created;
    private int _actualIndex = -1;

    public string Name => "添加判定线";

    public AddLineCommand(int insertAt) => _insertAt = insertAt;

    private static JudgeLine CreateDefaultLine()
    {
        var layers = new List<EventLayer>();
        for (int i = 0; i < 4; i++)
        {
            layers.Add(new EventLayer
            {
                MoveXEvents = new List<LineEvent>(),
                MoveYEvents = new List<LineEvent>(),
                RotateEvents = new List<LineEvent>(),
                AlphaEvents = new List<LineEvent>(),
                SpeedEvents = new List<LineEvent>(),
            });
        }
        return new JudgeLine
        {
            Texture = "line.png",
            EventLayers = layers,
            Father = -1,
            Notes = new List<Note>(),
        };
    }

    public void Execute(ChartEditService service)
    {
        var list = service.EditingChart.JudgeLineList;
        if (_created == null) _created = CreateDefaultLine();

        if (_insertAt < 0 || _insertAt > list.Count)
        {
            list.Add(_created);
            _actualIndex = list.Count - 1;
        }
        else
        {
            list.Insert(_insertAt, _created);
            _actualIndex = _insertAt;
        }

        ChartEventBus.NotifyLineCountChanged();
    }

    public void Undo(ChartEditService service)
    {
        service.EditingChart.JudgeLineList.Remove(_created);
        ChartEventBus.NotifyLineCountChanged();
    }
}

public class DeleteLinesCommand : IEditCommand
{
    private readonly List<int> _sortedIndices = new();
    private readonly List<JudgeLine> _removed = new();

    public string Name => "删除判定线";

    public DeleteLinesCommand(IEnumerable<int> indices)
    {
        foreach (var i in indices) _sortedIndices.Add(i);
        _sortedIndices.Sort();
    }

    public void Execute(ChartEditService service)
    {
        var list = service.EditingChart.JudgeLineList;
        _removed.Clear();

        // 从高到低删，避免索引位移
        for (int i = _sortedIndices.Count - 1; i >= 0; i--)
        {
            int idx = _sortedIndices[i];
            if (idx < 0 || idx >= list.Count) continue;
            _removed.Insert(0, list[idx]);
            list.RemoveAt(idx);
        }

        service.RefreshNoteMultiHold();
        ChartEventBus.NotifyLineCountChanged();
    }

    public void Undo(ChartEditService service)
    {
        var list = service.EditingChart.JudgeLineList;
        // 从低到高插回原索引
        for (int i = 0; i < _sortedIndices.Count && i < _removed.Count; i++)
        {
            int idx = _sortedIndices[i];
            if (idx < 0 || idx > list.Count) idx = list.Count;
            list.Insert(idx, _removed[i]);
        }
        service.RefreshNoteMultiHold();
        ChartEventBus.NotifyLineCountChanged();
    }
}