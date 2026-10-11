using Godot;
using QuickType;
using System;

public partial class TestSceneChartEditService : Node
{
    private Chart chart = new Chart();

    private ChartEditService chartEditService;

    public override void _Ready()
    {
        base._Ready();

        chart = ChartLoader.LoadChart("res://9091515374590503.json");
        if(chart == null)
        {
            FileLogger.LogErr($"[{this.Name}] chart is null!");
            return;
        }

        chartEditService = GetNode<ChartEditService>("/root/ChartEditService");
        if(chartEditService == null)
        {
            FileLogger.LogErr($"[{this.Name}] ChartEditService is null");
            return;
        }
        chartEditService.EditingChart = chart;

        // 为了方便，固定测试第0条判定线的第1个音符（确保索引有效）
    int lineId = 0;
    int noteIdx = 1;
    var note = chart.JudgeLineList[lineId].Notes[noteIdx];

    FileLogger.Log($"==============PosX 修改测试==============");
    FileLogger.Log($"原值: {note.PositionX}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.PosX, 123f);
    FileLogger.Log($"修改后: {note.PositionX}");

    FileLogger.Log($"==============Type 修改测试==============");
    FileLogger.Log($"原值: {note.Type}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.Type, NoteType.Flick);
    FileLogger.Log($"修改后: {note.Type}");

    FileLogger.Log($"==============StartTime 修改测试==============");
    FileLogger.Log($"原值: [{note.StartTime[0]}, {note.StartTime[1]}, {note.StartTime[2]}]");
    int[] newStartTime = new int[] { 123, 456, 789 };
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.StartTime, newStartTime);
    FileLogger.Log($"修改后: [{note.StartTime[0]}, {note.StartTime[1]}, {note.StartTime[2]}]");

    FileLogger.Log($"==============Above 修改测试==============");
    FileLogger.Log($"原值: {note.Above}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.Above, 0);
    FileLogger.Log($"修改后: {note.Above}");

    FileLogger.Log($"==============Alpha 修改测试==============");
    FileLogger.Log($"原值: {note.Alpha}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.Alpha, 100);
    FileLogger.Log($"修改后: {note.Alpha}");

    FileLogger.Log($"==============EndTime 修改测试==============");
    FileLogger.Log($"原值: [{note.EndTime[0]}, {note.EndTime[1]}, {note.EndTime[2]}]");
    int[] newEndTime = new int[] { 100, 200, 300 };
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.EndTime, newEndTime);
    FileLogger.Log($"修改后: [{note.EndTime[0]}, {note.EndTime[1]}, {note.EndTime[2]}]");

    FileLogger.Log($"==============IsFake 修改测试==============");
    FileLogger.Log($"原值: {note.IsFake}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.IsFake, true);
    FileLogger.Log($"修改后: {note.IsFake}");

    FileLogger.Log($"==============Size 修改测试==============");
    FileLogger.Log($"原值: {note.Size}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.Size, 3.0f);
    FileLogger.Log($"修改后: {note.Size}");

    FileLogger.Log($"==============VisibleTime 修改测试==============");
    FileLogger.Log($"原值: {note.VisibleTime}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.VisibleTime, 2.5f);
    FileLogger.Log($"修改后: {note.VisibleTime}");

    FileLogger.Log($"==============YOffset 修改测试==============");
    FileLogger.Log($"原值: {note.YOffset}");
    chartEditService.SetNoteProperty(lineId, noteIdx, NotePropertyEnum.YOffset, 123f);
    FileLogger.Log($"修改后: {note.YOffset}");
    }

}
