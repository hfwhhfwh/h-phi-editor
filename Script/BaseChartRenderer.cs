using Godot;
using System;
using System.Collections.Generic;

public abstract partial class BaseChartRenderer : Node
{
    public Control Parent { get; set; }
    public bool Disabled { get; set; }

    public abstract void Initialize(Control parent);

    public abstract void Render(
        List<JudgeLineRenderData> lineRenderDatas, List<NoteRenderData> noteRenderDatas);
}
