using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public enum JudgeGrade
{
    Perfect = 0,
    Good = 1,
    Bad = 2,
    Miss = 3,
}

public struct JudgeResult
{
    public Note Note;
    public int LineIndex;
    public int NoteIndex;
    public JudgeGrade Grade;
    public float TimeDeltaMs;
    public Vector2 HitPosition;
}

public partial class ChartJudge : Node
{
    public float PerfectMs { get; set; } = 80f;
    public float GoodMs { get; set; } = 160f;
    public float BadMs { get; set; } = 240f;


    public float HitDistancePixels { get; set; } = 160f;
    public float FlickSpeedThreshold { get; set; } = 500f;

    private sealed class NoteCandidate
    {
        public int LineIndex;
        public int NoteIndex;
        public Note Note;
    }

    private struct PressingHoldData
    {
        // public int LineIndex;
        // public int NoteIndex;
        // public Note Note;

        public NoteCandidate CandidateData;

        public bool IsReleased;
        public float Timer; // 单位：ms
        public JudgeResult JudgeResult;
        
    }

    private enum InputType
    {
        Click, Touch, Flick
    }

    private struct InputData
    {
        public Vector2 Position;
        public InputType Type;
        public double Time;
    }

    private readonly Queue<InputData> _inputDataQueue = new();

    private readonly List<NoteCandidate> _sortedAllNotes = new();
    private readonly List<NoteCandidate> _noteRemoveCashe = new();
    private readonly HashSet<Note> _judgedNotes = new();
    private readonly HashSet<Note> _missedNotes = new();

    /// <summary>
    /// Drag和Flick如果提前判定，暂存进集合中，在击打时刻再判定
    /// </summary>
    private readonly Dictionary<Note, JudgeResult> _judgedNotesBuffer = new();

    /// <summary>
    /// 正在长按中的Hold音符 值为计时器，初始为
    /// </summary>
    private readonly Dictionary<Note, PressingHoldData> _pressingHold = new();
    private readonly List<Note> _removePressingHoldCashe = new();

    /// <summary>
    /// 已经完成miss判定的hold音符
    /// </summary>
    private readonly Dictionary<Note, JudgeResult> _missedHold = new();
    private readonly List<Note> _removeMissedHoldCashe = new();

    public Chart Chart { get; private set; }
    public GameChartPlayer Player { get; private set; }
    public Control Parent { get; private set; }

    public event Action<JudgeResult> OnJudgeResult;
    public event Action<JudgeResult> OnHoldEndJudgeResult;

    /// <summary>
    /// 初始化，依赖Note的StartSec，需要提前计算
    /// </summary>
    /// <param name="player"></param>
    /// <param name="parent"></param>
    /// <param name="chart"></param>
    public void Initialize(GameChartPlayer player, Control parent, Chart chart)
    {
        Player = player;
        Parent = parent;
        Chart = chart;
        RebuildSortedNotes();

        player.judgedNotes = _judgedNotes;
        player.missedHold = _missedHold;
    }

    public void Update(double gameTime, double deltaTime)
    {
        if (Chart == null || Player == null) return;

        // 处理hold在末尾的miss判定
        _removeMissedHoldCashe.Clear();
        foreach(KeyValuePair<Note, JudgeResult> kvp in _missedHold)
        {
            Note hold = kvp.Key;
            if(gameTime >= hold.endSec)
            {
                _removeMissedHoldCashe.Add(hold);
                _missedNotes.Add(hold);
                _judgedNotes.Add(hold);

                OnHoldEndJudgeResult?.Invoke(kvp.Value);
            }
        }

        foreach(Note note in _removeMissedHoldCashe)
        {
            _missedHold.Remove(note);
        }
        

        // 从队列中的输入进行判定
        foreach(InputData inputData in _inputDataQueue)
        {
            TryHitNote(inputData.Position, inputData.Time, inputData.Type);
        }

        // 检查所有正在按压的 Hold 是否已到达尾部
        foreach (var kvp in _pressingHold)
        {
            Note hold = kvp.Key;
            if (gameTime >= hold.endSec) // endSec 需提前计算
            {
                // 正常结束，触发一次判定
                JudgeResult result = new JudgeResult
                {
                    Note = hold,
                    LineIndex = kvp.Value.CandidateData.LineIndex,
                    NoteIndex = kvp.Value.CandidateData.NoteIndex,
                    Grade = kvp.Value.JudgeResult.Grade,
                    TimeDeltaMs = kvp.Value.JudgeResult.TimeDeltaMs,
                    HitPosition = GetNotePosition(kvp.Value.CandidateData.LineIndex, hold)
                };
                OnHoldEndJudgeResult?.Invoke(result);

                // 停止特效并移除
                Player.StopHoldHitEffect(hold);
                _removePressingHoldCashe.Add(hold);
                _judgedNotes.Add(hold);
            }
        }
        foreach(Note note in _removePressingHoldCashe)
        {
            _pressingHold.Remove(note);
        }
        _removePressingHoldCashe.Clear();

        // 处理Hold中途松手的倒计时和miss
        foreach(KeyValuePair<Note, PressingHoldData> kvp in _pressingHold)
        {
            Note hold = kvp.Key;
            PressingHoldData data = kvp.Value;

            bool isReleased = true;

            // 1. 判断是否依然被按下
            foreach (InputData inputData in _inputDataQueue)
            {
                // 触摸事件用于维持Hold的按下状态 先记录Hold是否被按下
                if (inputData.Type == InputType.Touch)
                {
                    if (CanHoldBePressed(data.CandidateData.LineIndex, hold, inputData.Position, gameTime))
                    {
                        isReleased = false;
                    }

                }
            }

            if (isReleased == false) continue;

            // 2. 更新计时器
            data.IsReleased = true;
            data.Timer -= (float)deltaTime * 1000;
            _pressingHold[hold] = data;

            // 3. 判断miss
            if (data.Timer < 0)
            {
                // GD.Print($"Hold中断");
                // 准备触发miss
                _removePressingHoldCashe.Add(hold);
                Player.StopHoldHitEffect(hold);

                JudgeResult result = new JudgeResult
                {
                    Note = hold,
                    LineIndex = data.CandidateData.LineIndex,
                    NoteIndex = data.CandidateData.NoteIndex,
                    Grade = JudgeGrade.Miss,
                    TimeDeltaMs = (float)gameTime - hold.startSec, // 不需要
                    HitPosition = Vector2.Zero // 不需要
                };
                _missedHold[hold] = result;
            }
        }

        foreach(Note note in _removePressingHoldCashe)
        {
            _pressingHold.Remove(note);
        }
        _removePressingHoldCashe.Clear();

        _inputDataQueue.Clear();

        // 处理缓存的Drag和Flick
        foreach (KeyValuePair<Note, JudgeResult> kvp in _judgedNotesBuffer)
        {
            Note note = kvp.Key;
            if(gameTime >= note.startSec)
            {
                // 触发判定
                _judgedNotesBuffer.Remove(note);
                _judgedNotes.Add(note);

                OnJudgeResult?.Invoke(kvp.Value);
            }
        }

        // 处理Miss判定(Hold只处理未点击导致的Miss)
        foreach (NoteCandidate candidate in _sortedAllNotes)
        {
            Note note = candidate.Note;

            if(note.startSec > gameTime)
            {
                break;
            }

            if (note == null || note.IsFake || 
                _judgedNotes.Contains(note) || 
                _missedNotes.Contains(note) || 
                _pressingHold.ContainsKey(note) || 
                _missedHold.ContainsKey(note) || 
                _judgedNotesBuffer.ContainsKey(note)) continue;

            float startSec = note.startSec;
            float endSec = note.Type == 2 ? note.endSec : startSec;
            bool shouldMiss = false;

            if (gameTime > startSec + GoodMs / 1000f)
            {
                shouldMiss = true;
            }

            if (shouldMiss)
            {
                if(note.Type != 2) // 立刻触发miss判定
                {
                    _missedNotes.Add(note);
                    _judgedNotes.Add(note);

                    JudgeResult result = new JudgeResult
                    {
                        Note = note,
                        LineIndex = candidate.LineIndex,
                        NoteIndex = candidate.NoteIndex,
                        Grade = JudgeGrade.Miss,
                        TimeDeltaMs = (float)((gameTime - startSec) * 1000d),
                        HitPosition = GetNotePosition(candidate.LineIndex, note)
                    };
                    OnJudgeResult?.Invoke(result);
                }
                else // 记录，在末尾触发Miss判定
                {
                    // 准备触发miss
                    Player.StopHoldHitEffect(note);

                    JudgeResult result = new JudgeResult
                    {
                        Note = note,
                        LineIndex = candidate.LineIndex,
                        NoteIndex = candidate.NoteIndex,
                        Grade = JudgeGrade.Miss,
                        TimeDeltaMs = (float)gameTime - note.startSec, // 不需要
                        HitPosition = Vector2.Zero // 不需要
                    };
                    _missedHold[note] = result;

                }
            }
        }
    }

    public void OnTapInput(Vector2 screenPos, double gameTime)
    {
        // _inputDataQueue.Enqueue(new InputData
        // {
        //     Position = screenPos,
        //     Time = gameTime,
        //     Type = InputType.Click
        // });

        // Tap 直接判定，不入队
        TryHitNote(screenPos, gameTime, InputType.Click);
        
    }

    public void OnTouchInput(Vector2 screenPos, double gameTime)
    {
        _inputDataQueue.Enqueue(new InputData
        {
            Position = screenPos,
            Time = gameTime,
            Type = InputType.Touch
        });
        // TryHitNote(screenPos, gameTime, InputType.Touch);
    }

    public void OnFlickInput(Vector2 screenPos, double gameTime)
    {
        // _inputDataQueue.Enqueue(new InputData
        // {
        //     Position = screenPos,
        //     Time = gameTime,
        //     Type = InputType.Flick
        // });
        // Flick 也直接判定（或根据你的需求决定是否入队）
        TryHitNote(screenPos, gameTime, InputType.Flick);
    }

    public void ResetState()
    {
        _judgedNotes.Clear();
        _judgedNotesBuffer.Clear();
        _missedNotes.Clear();
        _pressingHold.Clear();
    }

    private void RebuildSortedNotes()
    {
        _sortedAllNotes.Clear();
        if (Chart?.JudgeLineList == null) return;

        for (int lineIndex = 0; lineIndex < Chart.JudgeLineList.Count; lineIndex++)
        {
            JudgeLine line = Chart.JudgeLineList[lineIndex];
            if (line?.Notes == null || line.Notes.Count == 0) continue;

            for (int noteIndex = 0; noteIndex < line.Notes.Count; noteIndex++)
            {
                Note note = line.Notes[noteIndex];
                if (note == null || note.IsFake) continue;

                _sortedAllNotes.Add(new NoteCandidate
                {
                    LineIndex = lineIndex,
                    NoteIndex = noteIndex,
                    Note = note
                });
            }
        }

        _sortedAllNotes.Sort((a, b) =>
        {
            int timeCompare = a.Note.startSec.CompareTo(b.Note.startSec);
            if (timeCompare != 0) return timeCompare;
            return a.LineIndex.CompareTo(b.LineIndex);
        });
    }

    private void TryHitNote(Vector2 pos, double gameTime, InputType inputType)
    {
        if (Chart == null || Player == null || _sortedAllNotes.Count == 0) return;

        // 取最大可能的时间扫描窗口（Tap 的负向窗口最大：BadMs）
        float minStartSec = (float)(gameTime - BadMs / 1000f);
        float maxStartSec = (float)(gameTime + GoodMs / 1000f);

        int startIdx = FindFirstNoteIndexByTime(minStartSec);
        if (startIdx >= _sortedAllNotes.Count) return; // 所有音符都太早

        NoteCandidate bestCandidate = null;
        float bestDistance = float.MaxValue;
        float bestStartSec = float.MaxValue;
        const float TIME_EPSILON = 0.0001f; // 0.1ms，用于判定"同时"

        // 只在时间窗口内做局部扫描，通常只有几十个候选
        for (int i = startIdx; i < _sortedAllNotes.Count; i++)
        {
            var candidate = _sortedAllNotes[i];
            var note = candidate.Note;

            // 时间剪枝：startSec 已经超出最大允许值
            if (note.startSec > maxStartSec) break;

            // 状态剪枝
            if (note.IsFake ||
                _judgedNotes.Contains(note) ||
                _missedNotes.Contains(note) ||
                _pressingHold.ContainsKey(note) ||
                _missedHold.ContainsKey(note) ||
                _judgedNotesBuffer.ContainsKey(note))
                continue;

            // 综合判定（类型+时间+距离），out 获取距离
            if (!CanBeJudged(candidate.LineIndex, note, pos, gameTime, inputType, out float distance))
                continue;

            // 最优候选策略：
            // 1) startSec 明显更小（更早）→ 替换
            // 2) startSec 相同（同时）→ 距离更近的替换
            if (bestCandidate == null ||
                note.startSec < bestStartSec - TIME_EPSILON ||
                (Math.Abs(note.startSec - bestStartSec) <= TIME_EPSILON && distance < bestDistance))
            {
                bestCandidate = candidate;
                bestDistance = distance;
                bestStartSec = note.startSec;
            }
        }

        if (bestCandidate != null)
        {
            ProcessHit(bestCandidate, gameTime, pos);
        }
    }

    /// <summary>
    /// 综合判定：类型 + 时间窗口 + 距离。若可判定，通过 out 返回精确距离。
    /// </summary>
    private bool CanBeJudged(int lineIndex, Note note, Vector2 screenPos, double gameTime, InputType inputType, out float distance)
    {
        distance = float.MaxValue;

        // 1. 输入类型
        if (!MatchInputType(note.Type, inputType)) return false;

        // 2. 时间窗口
        float deltaMs = (float)((gameTime - note.startSec) * 1000d);
        if (!IsInTimeWindow(note.Type, deltaMs)) return false;

        // 3. 距离
        distance = DistanceToLine(lineIndex, screenPos, note);
        return distance <= HitDistancePixels;
    }

    /// <summary>
    /// 判断Hold音符能否被继续长按(仅考虑位置)
    /// </summary>
    /// <param name="lineIndex"></param>
    /// <param name="note"></param>
    /// <param name="screenPos"></param>
    /// <param name="gameTime"></param>
    /// <returns></returns>
    private bool CanHoldBePressed(int lineIndex, Note note, Vector2 screenPos, double gameTime)
    {
        float distanceToLine = DistanceToLine(lineIndex, screenPos, note);
        return distanceToLine <= HitDistancePixels;
    }

    /// <summary>
    /// 判定命中后的统一处理（Tap/Hold/Drag/Flick 分支）
    /// </summary>
    private void ProcessHit(NoteCandidate candidate, double gameTime, Vector2 pos)
    {
        var note = candidate.Note;
        var result = ResolveJudge(candidate.LineIndex, note, candidate.NoteIndex, gameTime, pos);

        // Flick 或提前的 Drag → 进缓冲，到击打时刻再触发
        if (note.Type == 3 || (note.Type == 4 && gameTime < note.startSec))
        {
            _judgedNotesBuffer[note] = result;
        }
        // Hold → 进入长按状态
        else if (note.Type == 2)
        {
            _pressingHold[note] = new PressingHoldData
            {
                CandidateData = candidate,
                IsReleased = false,
                Timer = 30f,
                JudgeResult = result
            };
            Player.StartHoldHitEffect(note, GetNotePosition(candidate.LineIndex, note), result.Grade == JudgeGrade.Good, candidate.LineIndex);
        }
        // Tap → 立即触发
        else
        {
            _judgedNotes.Add(note);
            OnJudgeResult?.Invoke(result);
        }
    }

    /// <summary>
    /// 二分查找：返回第一个 startSec >= minTime 的索引
    /// </summary>
    private int FindFirstNoteIndexByTime(float minTime)
    {
        int left = 0;
        int right = _sortedAllNotes.Count - 1;
        int result = _sortedAllNotes.Count;

        while (left <= right)
        {
            int mid = (left + right) >> 1;
            if (_sortedAllNotes[mid].Note.startSec >= minTime)
            {
                result = mid;
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }
        return result;
    }

    /// <summary>
    /// 输入类型与音符类型匹配
    /// </summary>
    private bool MatchInputType(int noteType, InputType inputType)
    {
        return noteType switch
        {
            1 or 2 => inputType == InputType.Click,
            3       => inputType == InputType.Flick,
            4       => inputType == InputType.Touch,
            _       => false
        };
    }

    /// <summary>
    /// 时间窗口判定（仅时间，不含距离）
    /// </summary>
    private bool IsInTimeWindow(int noteType, float deltaMs)
    {
        return noteType switch
        {
            1 => deltaMs >= -BadMs && deltaMs <= GoodMs,
            2 => Math.Abs(deltaMs) <= GoodMs,
            3 or 4 => Math.Abs(deltaMs) <= GoodMs,
            _ => false
        };
    }

    private JudgeResult ResolveJudge(int lineIndex, Note note, int noteIndex, double gameTime, Vector2 screenPos)
    {
        float deltaMs = (float)((gameTime - note.startSec) * 1000f);
        JudgeGrade grade;

        if (note.Type == 1) // tap
        {
            if (Math.Abs(deltaMs) <= PerfectMs) grade = JudgeGrade.Perfect;
            else if (Math.Abs(deltaMs) <= GoodMs) grade = JudgeGrade.Good;
            else if (-deltaMs <= BadMs) grade = JudgeGrade.Bad;
            else grade = JudgeGrade.Miss;
        }
        else if (note.Type == 4)
        {
            if (Math.Abs(deltaMs) <= GoodMs) grade = JudgeGrade.Perfect;
            else grade = JudgeGrade.Miss;
        }
        else if (note.Type == 3)
        {
            if (Math.Abs(deltaMs) <= GoodMs) grade = JudgeGrade.Perfect;
            else grade = JudgeGrade.Miss;
        }
        else if (note.Type == 2)
        {
            grade = Math.Abs(deltaMs) <= PerfectMs ? JudgeGrade.Perfect : JudgeGrade.Good;
        }
        else
        {
            GD.PrintErr($"[{Name}] 未知的Note类型:{note.Type}");
            grade = JudgeGrade.Perfect;
        }

        return new JudgeResult
        {
            Note = note,
            LineIndex = lineIndex,
            NoteIndex = noteIndex,
            Grade = grade,
            TimeDeltaMs = deltaMs,
            HitPosition = GetNotePosition(lineIndex, note)
        };
    }

    private float DistanceToLine(int lineIndex, Vector2 screenPos, Note note)
    {
        if (Player == null || Chart == null) return float.MaxValue;

        var line = Chart.JudgeLineList[lineIndex];
        if (line == null || line.EventLayers == null || line.EventLayers.Count == 0) return float.MaxValue;

        Vector2 lineAnchor = new Vector2(GetCurrentLinePos(lineIndex).X, GetCurrentLinePos(lineIndex).Y);
        Vector2 noteLocal = new Vector2(note.PositionX, 0f);
        Vector2 noteWorld = PosUtil.GetChildGlobalPosition(lineAnchor, noteLocal, GetCurrentLineRotate(lineIndex));
        Vector2 noteScreen = PosUtil.ChartPosToViewportPos(noteWorld, Parent.Size);

        Vector2 lineNormal = new Vector2(0f, 1f).Rotated(Mathf.DegToRad(GetCurrentLineRotate(lineIndex)));
        Vector2 delta = screenPos - noteScreen;
        return Math.Abs(delta.X * lineNormal.Y - delta.Y * lineNormal.X);
    }

    private Vector2 GetNotePosition(int lineIndex, Note note)
    {
        var lineAnchor = GetCurrentLinePos(lineIndex);
        var noteLocal = new Vector2(note.PositionX, 0f);
        var world = PosUtil.GetChildGlobalPosition(lineAnchor, noteLocal, GetCurrentLineRotate(lineIndex));
        return PosUtil.ChartPosToViewportPos(world, Parent.Size);
    }

    private Vector2 GetCurrentLinePos(int lineIndex)
    {
        return new Vector2(Player.LineMoveX[lineIndex], Player.LineMoveY[lineIndex]);
    }

    private float GetCurrentLineRotate(int lineIndex)
    {
        return Player.LineRotate[lineIndex];
    }
}
