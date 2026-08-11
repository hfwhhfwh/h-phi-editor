using Godot;
using System;

/// <summary>
/// 播放器模式
/// </summary>
public enum PlayModeEnum
{
    Editor, // 播放器隐藏
    Player, // 播放器正常播放
    EditorAndPlayer, // 编辑器和播放器同时显示
}
public static class PlayModeManager
{
    public static PlayModeEnum PlayMode { get; private set; }

    /// <summary>
    /// 当播放模式变化时发出信号
    /// </summary>
    public static event Action<PlayModeEnum> PlayModeChanged;

    public static void SetPlayMode(PlayModeEnum playModeEnum)
    {
        PlayMode = playModeEnum;
        PlayModeChanged?.Invoke(playModeEnum);
    }
}
