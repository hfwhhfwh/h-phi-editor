using Godot;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuickType
{
    public static class AllTypes
    {
        public readonly static LineEventEnum[] allLineEventTypes = (LineEventEnum[])Enum.GetValues(typeof(LineEventEnum));
    }
    public partial class JudgeLine
    {
        /// <summary>
        /// 获取某一时刻的判定线速度（是谱面文件中写的数值，每个单位代表120px/s）
        /// </summary>
        /// <param name="events">速度事件</param>
        /// <param name="time">游戏时间</param>
        /// <returns></returns>
        public float GetSpeedAtTime(float time)
        {
            float totalSpeed = 0; // 当前速度是所有事件层速度的累加
            foreach(EventLayer layer in EventLayers)
            {
                if(layer == null) continue;

                List<LineEvent> events = layer.SpeedEvents;
                if (events == null || events.Count == 0) continue;

                // 二分查找 time 所在的段（或最后一个 startSec <= time 的段）
                int idx = ChartDataHelper.BinarySearchLatestEvent(events, time);

                // time 在第一个事件之前（按约定，速度为0）
                if (idx < 0) continue; 

                LineEvent ev = events[idx];
                float start = ev.Start;
                float end = ev.End;
                float startSec = ev.startSec;
                float endSec = ev.endSec;

                if (time >= startSec && time <= endSec)
                {
                    float a = (end - start) / (endSec - startSec); // 加速度 a = △v/△t
                    float t = (float)(time - startSec); // 时间
                    totalSpeed += start + a * t;
                    
                }
                else if (time > endSec)
                {
                    totalSpeed += ev.End;
                }
                else
                {
                    //GD.PrintErr($"");
                    throw new Exception("GetSpeedAtTime() Error!");
                    //return 0f;
                }
            }
            
            return totalSpeed;
        }

        /// <summary>
        /// 获取指定时刻的总位移
        /// </summary>
        /// <param name="time">游戏时刻</param>
        /// <returns>从0时刻到指定时刻的总位移</returns>
        public float GetDisplacementAtTime(float time)
        {
            List<LineEvent> events = EventLayers[0].SpeedEvents; // TODO 这里假设只有一个事件层
            if (events == null || events.Count == 0) return 0f;

            // 二分查找 time 所在的段（或最后一个 startSec <= time 的段）
            int idx = ChartDataHelper.BinarySearchLatestEvent(events, time);

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

        public void RefreshAllNoteDisplacement()
        {
            if(Notes == null) return;
            foreach(Note note in Notes)
            {
                note.RefreshDisplacement(this);
            }
        }

        public void AddNote()
        {
            
        }
    }


    public partial class LineEvent
    {
        [JsonIgnore] public float startSec;
        [JsonIgnore] public float endSec;

        // 仅在速度事件中有效，在此事件的StartTime之前的所有位移，前缀和优化
        [JsonIgnore] public float prefixX; 

        /// <summary>
        /// 设置事件的开始时间
        /// </summary>
        /// <param name="time">新的开始时间</param>
        /// <param name="bpmList">BPM事件列表</param>
        public void SetStartTime(int[] time, BpmEvent[] bpmList)
        {
            StartTime = time;
            startSec = TimeUtil.BeatToSecond(time, bpmList);
        }

        /// <summary>
        /// 设置事件的结束时间
        /// </summary>
        /// <param name="time">新的结束时间</param>
        /// <param name="bpmList">BPM事件列表</param>
        public void SetEndTime(int[] time, BpmEvent[] bpmList)
        {
            EndTime = time;
            endSec = TimeUtil.BeatToSecond(time, bpmList);
        }

        public void RefreshSec(BpmEvent[] bpmList)
        {
            startSec = TimeUtil.BeatToSecond(StartTime, bpmList);
            endSec = TimeUtil.BeatToSecond(EndTime, bpmList);
        }
    }

    public partial class Note
    {
        [JsonIgnore] public float startSec;
        [JsonIgnore] public float endSec;

        /// <summary>
        /// note所在时刻累积的所有位移，用于优化性能
        /// </summary>
        [JsonIgnore] public float allDisplacement;

        /// <summary>
        /// （仅限Hold）Hold末尾所在时刻累积的所有位移，用于优化性能
        /// </summary>
        [JsonIgnore] public float endAllDisplacement;

        public void SetStartTime(int[] newStartTime, BpmEvent[] bpmList, JudgeLine line)
        {
            StartTime = newStartTime;
            startSec = TimeUtil.BeatToSecond(newStartTime, bpmList);

            //防止StartTime在EndTime后面
            if(startSec > endSec)
            {
                EndTime = StartTime;
                endSec = startSec;
            }
            //如果是tap flick drag, StartTime和EndTime必须相同
            if(Type == 1 || Type == 3 || Type == 4)
            {
                EndTime = StartTime;
                endSec = startSec;
            }

            // 刷新总位移
            allDisplacement = line.GetDisplacementAtTime(startSec);
        }

        public void SetEndTime(int[] newEndTime, BpmEvent[] bpmList, JudgeLine line)
        {
            EndTime = newEndTime;
            endSec = TimeUtil.BeatToSecond(newEndTime, bpmList);

            bool startTimeChanged = false;

            //防止StartTime在EndTime后面
            if(startSec > endSec)
            {
                StartTime = EndTime;
                startSec = endSec;
                startTimeChanged = true;
            }
            //如果是tap flick drag, StartTime和EndTime必须相同
            if(Type == 1 || Type == 3 || Type == 4)
            {
                StartTime = EndTime;
                startSec = endSec;
                startTimeChanged = true;
            }

            // 如果StartTime被修改了，必须刷新总位移
            if(startTimeChanged)
            {
                allDisplacement = line.GetDisplacementAtTime(startSec);
            }

            if(Type == 2) endAllDisplacement = line.GetDisplacementAtTime(endSec);
        }

        public void RefreshDisplacement(JudgeLine line)
        {
            allDisplacement = line.GetDisplacementAtTime(startSec);

            if(Type == 2) 
                endAllDisplacement = line.GetDisplacementAtTime(endSec);
            
        }
    }

    public partial class EventLayer
    {
        public List<LineEvent> GetLineEvents(LineEventEnum lineEventEnum)
        {
            List<LineEvent> lineEvents = lineEventEnum switch
            {
                LineEventEnum.MoveX => MoveXEvents,
                LineEventEnum.MoveY => MoveYEvents,
                LineEventEnum.Rotate => RotateEvents,
                LineEventEnum.Alpha => AlphaEvents,
                LineEventEnum.Speed => SpeedEvents,
                _ => MoveXEvents,
            };

            // 懒加载：如果为 null，创建新列表并写回对应属性
            if (lineEvents == null)
            {
                lineEvents = new List<LineEvent>();
                switch (lineEventEnum)
                {
                    case LineEventEnum.MoveX:  MoveXEvents  = lineEvents; break;
                    case LineEventEnum.MoveY:  MoveYEvents  = lineEvents; break;
                    case LineEventEnum.Rotate: RotateEvents = lineEvents; break;
                    case LineEventEnum.Alpha:  AlphaEvents  = lineEvents; break;
                    case LineEventEnum.Speed:  SpeedEvents  = lineEvents; break;
                }
            }

            return lineEvents;
        }

        public void RefreshSpeedEventsPrefix()
        {
            if (SpeedEvents == null || SpeedEvents.Count == 0) return;

            float totalX = 0; // 总位移
            //遍历所有速度事件
            for (int i = 0; i < SpeedEvents.Count; i++)
            {
                LineEvent ev = SpeedEvents[i];

                //每一个事件都需要处理上一个事件的位移和间隙的位移
                if(i == 0)
                {
                    //第一个事件起始时间与0的间隙，默认速度为0
                    ev.prefixX = 0;
                }
                else
                {
                    //上个事件的位移
                    float lastX = (SpeedEvents[i-1].Start + SpeedEvents[i-1].End)*120f * (SpeedEvents[i-1].endSec - SpeedEvents[i-1].startSec) / 2f;
                    //事件间隙的位移
                    float gapX = SpeedEvents[i-1].End*120f * (ev.startSec - SpeedEvents[i-1].endSec);

                    totalX += lastX + gapX;

                    //GD.Print($"lastX:{lastX}, gapX:{gapX}");

                    //赋值给前缀和
                    ev.prefixX = totalX;
                }

            }
        }
    }
}
