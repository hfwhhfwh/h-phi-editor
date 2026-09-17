using System;
using System.Collections.Generic;
using Godot;

public class CommandHistory
{
    private readonly List<IEditCommand> _undoStack = new();
    private readonly List<IEditCommand> _redoStack = new();

    public int MaxSteps { get; set; } = 200;
    public event Action Changed;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Execute(IEditCommand cmd, ChartEditService service)
    {
        cmd.Execute(service);
        _undoStack.Add(cmd);
        _redoStack.Clear();          // 新操作让重做栈失效
        TrimUndoStack();
        GD.Print($"执行命令：{cmd.Name}");
        Changed?.Invoke();
    }

    public void Undo(ChartEditService service)
    {
        if (_undoStack.Count == 0) return;
        IEditCommand cmd = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        cmd.Undo(service);
        _redoStack.Add(cmd);
        GD.Print($"撤销命令：{cmd.Name}");
        Changed?.Invoke();
    }

    public void Redo(ChartEditService service)
    {
        if (_redoStack.Count == 0) return;
        var cmd = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        cmd.Execute(service);
        _undoStack.Add(cmd);
        GD.Print($"重做命令：{cmd.Name}");
        Changed?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Changed?.Invoke();
    }

    private void TrimUndoStack()
    {
        if (MaxSteps > 0 && _undoStack.Count > MaxSteps)
        {
            _undoStack.RemoveRange(0, _undoStack.Count - MaxSteps);
        }
    }
}