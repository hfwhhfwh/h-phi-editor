using Godot;
using QuickType;
using System;

public static class TimeUtil
{
    /// <summary>
    /// 时间转换：将Beat（int[]）转换为秒
    /// </summary>
    /// <param name="beat">当前节拍数</param>
    /// <param name="bpmList">谱面的所有bpm事件</param>
    /// <returns></returns>
    public static float BeatToSecond(int[] beat, BpmEvent[] bpmList)
    {
        if (bpmList == null || bpmList.Length == 0) return 0;

        // 将Beat转为以拍为单位的总拍数： beat[0] + beat[1]/beat[2]
        float totalBeats = beat[0] + (float)beat[1] / beat[2];

        return BeatToSecond(totalBeats, bpmList);
    }

    /// <summary>
    /// 时间转换：将Beat（int[]）转换为秒
    /// </summary>
    /// <param name="beatValue">当前节拍数</param>
    /// <param name="bpmList">谱面的所有bpm事件</param>
    /// <returns></returns>
    public static float BeatToSecond(float beatValue, BpmEvent[] bpmList)
    {
        if (bpmList == null || bpmList.Length == 0) return 0;
        
        // 找到当前Beat所在的BPM段并累积时间
        float elapsedSeconds = 0;
        float lastBpmBeat = 0; // 上一个BPM事件的总拍数
        float currentBpm = bpmList[0].Bpm; // 默认第一个BPM

        for (int i = 0; i < bpmList.Length; i++)
        {
            var bpmEvent = bpmList[i];
            float eventBeat = bpmEvent.StartTime[0] + (float)bpmEvent.StartTime[1] / bpmEvent.StartTime[2];

            if (beatValue >= eventBeat)
            {
                // 累加从上一个BPM点到这个BPM点的时间
                if (i > 0)
                {
                    float beatDiff = eventBeat - lastBpmBeat;
                    elapsedSeconds += beatDiff * 60f / (float)currentBpm;
                }
                lastBpmBeat = eventBeat;
                currentBpm = (float)bpmEvent.Bpm;
            }
            else
            {
                break;
            }
        }

        // 加上从最后一个BPM点到目标Beat的时间
        float remainingBeats = beatValue - lastBpmBeat;
        elapsedSeconds += remainingBeats * 60f / currentBpm;

        return elapsedSeconds;
    }

    /// <summary>
    /// 时间转换：将秒转换为Beat
    /// </summary>
    /// <param name="secondValue">当前秒数</param>
    /// <param name="BpmList">谱面的所有bpm事件</param>
    /// <returns>对应的节拍数</returns>
    public static float SecondToBeat(float secondValue, BpmEvent[] BpmList)
    {
        // 处理空列表或无效输入
        if (BpmList == null || BpmList.Length == 0 || secondValue < 0)
        {
            GD.PrintErr($"[Util] SecondToBeat() 输入不合法");
            return 0f;
        }

        // 辅助函数：计算事件的绝对节拍（与BeatToSecond中的计算方式一致）
        float GetEventBeat(BpmEvent e) => e.StartTime[0] + (float)e.StartTime[1] / e.StartTime[2];

        // 累积已处理的时间（秒）
        float elapsedSeconds = 0f;
        // 当前BPM段的起始节拍
        float currentBeat = GetEventBeat(BpmList[0]);

        // 遍历BPM段（除最后一个事件外，每个段由当前事件到下一个事件构成）
        for (int i = 0; i < BpmList.Length - 1; i++)
        {
            BpmEvent curEvent = BpmList[i];
            BpmEvent nextEvent = BpmList[i + 1];

            float startBeat = GetEventBeat(curEvent);
            float endBeat = GetEventBeat(nextEvent);
            float bpm = (float)curEvent.Bpm;

            // 当前段的节拍跨度
            float beatDiff = endBeat - startBeat;
            // 当前段的持续时间（秒）
            float segmentSeconds = beatDiff * 60f / bpm;

            if (secondValue <= elapsedSeconds + segmentSeconds)
            {
                // 目标秒数落在当前段内
                float offsetSeconds = secondValue - elapsedSeconds;
                float offsetBeats = offsetSeconds * bpm / 60f;
                return startBeat + offsetBeats;
            }

            // 否则，累加时间并移动到下一段的起始节拍
            elapsedSeconds += segmentSeconds;
            currentBeat = endBeat;
        }

        // 处理最后一个BPM段（从最后一个事件到无限远）
        BpmEvent lastEvent = BpmList[BpmList.Length - 1];
        float lastBeat = GetEventBeat(lastEvent);
        float lastBpm = (float)lastEvent.Bpm;
        float remainingSeconds = secondValue - elapsedSeconds;
        float remainingBeats = remainingSeconds * lastBpm / 60f;
        return lastBeat + remainingBeats;
    }
}
