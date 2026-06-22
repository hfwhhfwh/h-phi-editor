using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class ChartDataHelper
{
    /// <summary>
    /// 设置事件的开始时间
    /// </summary>
    /// <param name="ev">事件</param>
    /// <param name="newStartTime">新的开始时间</param>
    /// <param name="bpmList">BPM事件列表</param>
    public static void SetEventStartTime(LineEvent ev, int[] newStartTime, BpmEvent[] bpmList)
    {
        ev.StartTime = newStartTime;
        ev.startSec = TimeUtil.BeatToSecond(newStartTime, bpmList);
    }

    /// <summary>
    /// 设置事件的结束时间
    /// </summary>
    /// <param name="ev">事件</param>
    /// <param name="newEndTime">新的结束时间</param>
    /// <param name="bpmList">BPM事件列表</param>
    public static void SetEventEndTime(LineEvent ev, int[] newEndTime, BpmEvent[] bpmList)
    {
        ev.EndTime = newEndTime;
        ev.endSec = TimeUtil.BeatToSecond(newEndTime, bpmList);
    }

    public static void SetNoteStartTime(Note note, int[] newStartTime, BpmEvent[] bpmList)
    {
        note.StartTime = newStartTime;
        note.startSec = TimeUtil.BeatToSecond(newStartTime, bpmList);
        //防止StartTime在EndTime后面
        if(note.startSec > note.endSec)
        {
            note.EndTime = note.StartTime;
            note.endSec = note.startSec;
        }
        //如果是tap flick drag, StartTime和EndTime必须相同
        if(note.Type == 1 || note.Type == 3 || note.Type == 4)
        {
            note.EndTime = note.StartTime;
            note.endSec = note.startSec;
        }
    }

    public static void SetNoteEndTime(Note note, int[] newEndTime, BpmEvent[] bpmList)
    {
        note.EndTime = newEndTime;
        note.endSec = TimeUtil.BeatToSecond(newEndTime, bpmList);
        //防止StartTime在EndTime后面
        if(note.startSec > note.endSec)
        {
            note.StartTime = note.EndTime;
            note.startSec = note.endSec;
        }
        //如果是tap flick drag, StartTime和EndTime必须相同
        if(note.Type == 1 || note.Type == 3 || note.Type == 4)
        {
            note.StartTime = note.EndTime;
            note.startSec = note.endSec;
        }
    }

    /// <summary>
    /// 根据事件的StartTime和EndTime，重新计算所有事件的StartSec和EndSec
    /// </summary>
    public static void RefreshEventSec(Chart chart)
    {
        BpmEvent[] bpmList = chart.BpmList;

        foreach(JudgeLine line in chart.JudgeLineList)
        {
            if(line.EventLayers == null || line.EventLayers.Length == 0) continue;
            foreach(EventLayer layer in line.EventLayers)
            {
                if(layer == null) continue;
                //1. MoveXEvent
                if(layer.MoveXEvents != null && layer.MoveXEvents.Length > 0)
                {
                    foreach(LineEvent lineEvent in layer.MoveXEvents)
                    {
                        lineEvent.startSec = TimeUtil.BeatToSecond(lineEvent.StartTime, bpmList);
                        lineEvent.endSec = TimeUtil.BeatToSecond(lineEvent.EndTime, bpmList);
                    }
                }
                
                //2. MoveYEvents
                if(layer.MoveYEvents != null && layer.MoveYEvents.Length > 0)
                {
                    foreach(LineEvent lineEvent in layer.MoveYEvents)
                    {
                        lineEvent.startSec = TimeUtil.BeatToSecond(lineEvent.StartTime, bpmList);
                        lineEvent.endSec = TimeUtil.BeatToSecond(lineEvent.EndTime, bpmList);
                    }
                }
                
                //3. RotateEvents
                if(layer.RotateEvents != null && layer.RotateEvents.Length > 0)
                {
                    foreach(LineEvent lineEvent in layer.RotateEvents)
                    {
                        lineEvent.startSec = TimeUtil.BeatToSecond(lineEvent.StartTime, bpmList);
                        lineEvent.endSec = TimeUtil.BeatToSecond(lineEvent.EndTime, bpmList);
                    }
                }
                
                //4. AlphaEvents
                if(layer.AlphaEvents != null && layer.AlphaEvents.Length > 0)
                {
                    foreach(LineEvent lineEvent in layer.AlphaEvents)
                    {
                        lineEvent.startSec = TimeUtil.BeatToSecond(lineEvent.StartTime, bpmList);
                        lineEvent.endSec = TimeUtil.BeatToSecond(lineEvent.EndTime, bpmList);
                    }
                }
                
                //5. SpeedEvents
                if(layer.SpeedEvents != null && layer.SpeedEvents.Length > 0)
                {
                    foreach(LineEvent lineEvent in layer.SpeedEvents)
                    {
                        lineEvent.startSec = TimeUtil.BeatToSecond(lineEvent.StartTime, bpmList);
                        lineEvent.endSec = TimeUtil.BeatToSecond(lineEvent.EndTime, bpmList);
                    }
                }
                
            }
        }
    }

    public static void RefreshEventPrefix(LineEvent[] events)
    {
        if (events == null || events.Length == 0) return;

        float totalX = 0; // 总位移
        //遍历所有速度事件
        for (int i = 0; i < events.Length; i++)
        {
            LineEvent ev = events[i];

            //每一个事件都需要处理上一个事件的位移和间隙的位移
            if(i == 0)
            {
                //第一个事件起始时间与0的间隙，默认速度为0
                ev.prefixX = 0;
            }
            else
            {
                //上个事件的位移
                float lastX = (events[i-1].Start + events[i-1].End)*120f * (events[i-1].endSec - events[i-1].startSec) / 2f;
                //事件间隙的位移
                float gapX = events[i-1].End*120f * (ev.startSec - events[i-1].endSec);

                totalX += lastX + gapX;

                //GD.Print($"lastX:{lastX}, gapX:{gapX}");

                //赋值给前缀和
                ev.prefixX = totalX;
            }

        }
    }

    public static void RefreshAllEventPrefix(Chart chart)
    {
        BpmEvent[] bpmList = chart.BpmList;

        foreach(JudgeLine line in chart.JudgeLineList)
        {
            foreach(EventLayer layer in line.EventLayers)
            {
                if(layer == null) continue;
                RefreshEventPrefix(layer.SpeedEvents);
                
            }
        }
    }

    public static void RefreshNoteSec(Chart chart)
    {
        BpmEvent[] bpmList = chart.BpmList;

        foreach(JudgeLine line in chart.JudgeLineList)
        {
            if(line.Notes == null || line.Notes.Length == 0) continue;
            foreach(Note note in line.Notes)
            {
                if(note.Type == 2)
                {
                    note.startSec = TimeUtil.BeatToSecond(note.StartTime, bpmList);
                    note.endSec = TimeUtil.BeatToSecond(note.EndTime, bpmList);
                }
                else
                {
                    note.startSec = TimeUtil.BeatToSecond(note.StartTime, bpmList);
                    note.endSec = note.startSec;
                }
            }
        }
    }

    /// <summary>
    /// 更新所有note的累积位移
    /// 调用此方法前必须先调用RefreshEventSec, RefreshEventPrefix, RefreshNoteSec
    /// </summary>
    /// <param name="chart">需要更新的铺面</param>
    public static void RefreshAllNoteAllDisplacement(Chart chart)
    {
        BpmEvent[] bpmList = chart.BpmList;

        if(chart.JudgeLineList == null || chart.JudgeLineList.Length == 0) return;
        foreach(JudgeLine line in chart.JudgeLineList)
        {
            if(line.Notes == null || line.Notes.Length == 0) continue;
            foreach(Note note in line.Notes)
            {
                note.allDisplacement = GetDisplacementAtTime(line.EventLayers[0].SpeedEvents, note.startSec);
            }
        }

    }


    /// <summary>
    /// 二分查找最后一个 startSec <= time 的事件
    /// </summary>
    /// <param name="events">事件列表</param>
    /// <param name="time">游戏时间</param>
    /// <returns>事件的索引，若time小于第一个事件的开始时间，则返回-1</returns>
    public static int BinarySearchLatestEvent(LineEvent[] events, double time)
    {
        if (events == null || events.Length == 0)
        {
            throw new Exception("events列表为空");
        }

        // 二分查找 time 所在的段（或最后一个 startSec <= time 的段）
        int idx = Array.BinarySearch(events, new LineEvent { startSec = (float)time }, 
            Comparer<LineEvent>.Create((a, b) => a.startSec.CompareTo(b.startSec)));
        if (idx < 0) idx = ~idx - 1;

        return idx;
    }

    /// <summary>
    /// 通用事件插值（如moveX, alpha等）
    /// </summary>
    /// <param name="events">事件列表</param>
    /// <param name="time">游戏运行时间</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>当前时间下的事件值</returns>
    public static float InterpolateEvent(LineEvent[] events, double time, float defaultValue)
    {
        if (events == null || events.Length == 0) return defaultValue;

        // 二分查找 time 所在的段（或最后一个 startSec <= time 的段）
        int idx = BinarySearchLatestEvent(events, time);

        // time 在第一个事件之前（按约定，为0）
        if (idx < 0) return 0f; 

        LineEvent ev = events[idx];
        float startSec = ev.startSec;
        float endSec = ev.endSec;

        if (time >= startSec && time <= endSec)
        {
            // 插值，需要考虑事件切割
            float t = (float)((time - startSec) / (endSec - startSec));
            float leftCut = ev.EasingLeft;
            float rightCut = ev.EasingRight;
            return EasingHelper.CutInterpolateValue(ev.Start, ev.End, t, ev.EasingType, leftCut, rightCut);
            
        }
        else if (time > endSec)
        {
            return ev.End;
        }
        else
        {
            GD.PrintErr($"[ChartDataHelper] InterpolateEvent() Error! time:{time}");
            return 0f;
        }
    }

    /// <summary>
    /// 获取某一时刻的判定线速度（是谱面文件中写的数值，每个单位代表120px/s）
    /// </summary>
    /// <param name="events">速度事件</param>
    /// <param name="time">游戏时间</param>
    /// <returns></returns>
    public static float GetSpeedAtTime(LineEvent[] events, float time)
    {
        // //遍历所有速度事件
        // for (int i = 0; i < events.Length; i++)
        // {
        //     LineEvent ev = events[i];

        //     float start = ev.Start;
        //     float end = ev.End;
        //     float startSec = ev.startSec;
        //     float endSec = ev.endSec;

        //     // 如果time正在这个事件中
        //     if(time >= startSec && time < endSec)
        //     {
        //         float a = (end - start) / (endSec - startSec); // 加速度 a = △v/△t
        //         float t = (float)(time - startSec); // 时间
        //         return start + a * t;
        //     }
            
        //     //同时也要处理与下一个速度事件之间的部分
        //     if(i < events.Length - 1) // 如果这不是最后一个事件
        //     {
        //         float nextStartSec = events[i+1].startSec;
        //         //如果time正在这个间隔中
        //         if(time >= endSec && time < nextStartSec)
        //         {
        //             return end;
        //         }
        //         //如果time在这个间隔之后
        //         else if(time >= nextStartSec)
        //         {
        //             continue;//继续到下一个速度事件
        //         }
        //     }
        //     else// 如果这是最后一个事件之后的间隔
        //     {
        //         return end;
        //     }
        // }
        // return events[^1].End;//理论上不会执行到这里

        if (events == null || events.Length == 0) return 0f;

        // 二分查找 time 所在的段（或最后一个 startSec <= time 的段）
        int idx = BinarySearchLatestEvent(events, time);

        // time 在第一个事件之前（按约定，速度为0）
        if (idx < 0) return 0f; 

        LineEvent ev = events[idx];
        float start = ev.Start;
        float end = ev.End;
        float startSec = ev.startSec;
        float endSec = ev.endSec;

        if (time >= startSec && time <= endSec)
        {
            float a = (end - start) / (endSec - startSec); // 加速度 a = △v/△t
            float t = (float)(time - startSec); // 时间
            return start + a * t;
            
        }
        else if (time > endSec)
        {
            return ev.End;
        }
        else
        {
            GD.PrintErr($"[ChartDataHelper] GetSpeedAtTime() Error!");
            return 0f;
        }

    }

    /// <summary>
    /// 根据速度事件，计算note的位移（开销较大，已被前缀和替代）
    /// </summary>
    /// <param name="events">速度事件</param>
    /// <param name="time">游戏时间</param>
    /// <returns></returns>
    public static float IntegralSpeedEvent(LineEvent[] events, float time)
    {
        float totalX = 0; // Y轴上的总位移
        //遍历所有速度事件
        for (int i = 0; i < events.Length; i++)
        {
            LineEvent ev = events[i];

            float start = ev.Start;
            float end = ev.End;
            float startSec = ev.startSec;
            float endSec = ev.endSec;

            // 如果time已经在这个事件之后
            if(time >= endSec)
            {
                totalX += 120f * (start + end) * (endSec - startSec) / 2f;

            }
            // 如果time正在这个事件中
            else if(time >= startSec && time < endSec)
            {
                float a = 120f * (end - start) / (endSec - startSec); // 加速度 a = △v/△t
                float t = (float)(time - startSec); // 时间
                float x = (start * 120f) * t + 0.5f * a * t * t; // 位移x = v0t + 0.5at^2
                totalX += x;
                break;
            }
            //如果time在这个事件之前
            else
            {
                break;
            }
            
            //同时也要处理与下一个速度事件之间的部分
            if(i < events.Length - 1) // 如果这不是最后一个事件
            {
                float nextStartSec = events[i+1].startSec;
                //如果time正在这个间隔中
                if(time >= endSec && time < nextStartSec)
                {
                    totalX += 120f * (float)(end * (time - endSec));
                    break;
                }
                //如果time在这个间隔之后
                else if(time >= nextStartSec)
                {
                    totalX += 120f * (float)(end * (nextStartSec - endSec));
                }
            }
            else// 如果这是最后一个事件之后的间隔
            {
                totalX += 120f * (float)(end * (time - endSec));
            }
        }
        return totalX;

    }

    public static float GetDisplacementAtTime(LineEvent[] events, float time)
    {
        if (events == null || events.Length == 0) return 0f;

        // 二分查找 time 所在的段（或最后一个 startSec <= time 的段）
        int idx = BinarySearchLatestEvent(events, time);

        // time 在第一个事件之前（按约定，速度为0）
        if (idx < 0) return 0f; 

        LineEvent ev = events[idx];
        if (time < ev.startSec) return ev.prefixX; // 防御
        // if (idx < events.Length - 1 && time > events[idx+1].startSec)
        // {
        //     // 实际上二分查找应保证 time 在 ev.startSec 和 ev.endSec 之间或后续区间
        //     // 为简化，这里只处理落在当前事件内或之后的情况
        // }
        if (time < ev.endSec)
        {
            // 事件内部积分
            float dt = time - ev.startSec;
            float v0 = ev.Start;
            float a = (ev.End - ev.Start) / (ev.endSec - ev.startSec);
            float dispInside = 120f * (v0 * dt + 0.5f * a * dt * dt);
            return ev.prefixX + dispInside;
        }
        else
        {
            // 这个事件已经结束，正在与下一个事件之间的间隙中
            float dispInside = (ev.Start + ev.End)*120f * (ev.endSec - ev.startSec) / 2f;
            float dispGap = ev.End*120f * (time - ev.endSec);
            return ev.prefixX + dispInside + dispGap;
        }
    }
}
