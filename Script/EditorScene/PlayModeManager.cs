using Godot;
using System;

/// <summary>
/// 播放器模式
/// </summary>
public enum PlayModeEnum
{
    Editing, // 播放器隐藏
    PlayerPlaying, // 播放器正常播放
    PlayerPause, // 播放器下暂停
    EditorPlaying, // 在编辑器下滚动播放
    EditorAndPlayerPlaying, // 编辑器和播放器同时显示
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
