using Godot;
using System;

public partial class PopupMenuItem
{
    public string Text { get; set; }
    public Action Callback { get; set; }
    public bool Disabled { get; set; } = false;
    public bool IsSeparator { get; set; } = false; // 用于分隔符
}
