using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public sealed class EditorClipboard
{
    public NoteClipBoard noteClipBoard;
    public LineEventClipBoard lineEventClipBoard;
    public BpmEventClipBoard bpmEventClipBoard;

    public EditPanelType LatestClipBoard { get; set; }

    public EditorClipboard()
    {
        noteClipBoard = new NoteClipBoard();
        lineEventClipBoard = new LineEventClipBoard();
        bpmEventClipBoard = new BpmEventClipBoard();
    }
}

public sealed class NoteClipBoard
{
    public Beat SourceStartBeat { get; set; }
    public float SourcePosX { get; set; }

    public int SourceLineId { get; set; }
    public List<NoteSnapshot> Notes { get; set; }

    public NoteClipBoard()
    {
        Notes = new List<NoteSnapshot>();
    }
}

public sealed class LineEventClipBoard
{
    public Beat SourceStartBeat { get; set; }

    public int SourceLineId { get; set; }
    public int SourceLayer { get; set; }

    public List<LineEventClipBoardItem> Events { get; set; }

    public LineEventClipBoard()
    {
        Events = new List<LineEventClipBoardItem>();
    }
}

public sealed class LineEventClipBoardItem
{
    public LineEventEnum Type { get; }
    public LineEventSnapshot Snapshot { get; }

    public LineEventClipBoardItem(LineEventEnum type, LineEventSnapshot snapshot)
    {
        Type = type;
        Snapshot = snapshot;
    }
}

public sealed class BpmEventClipBoard
{
    public Beat SourceStartBeat { get; set; }

    public List<BpmEventSnapshot> Bpms { get; set; }

    public BpmEventClipBoard()
    {
        Bpms = new List<BpmEventSnapshot>();
    }
}
