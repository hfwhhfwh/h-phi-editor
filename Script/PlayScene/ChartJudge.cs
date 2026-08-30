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


    public float HitDistancePixels { get; set; } = 200f;
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

    /// <summary>
    /// 已经完成miss判定的hold音符
    /// </summary>
    private readonly Dictionary<Note, JudgeResult> _missedHold = new();
    

    private readonly List<JudgeResult> _pendingResults = new();

    private float _holdDetectTimer = 2000; // 单位：ms

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
        foreach(KeyValuePair<Note, JudgeResult> kvp in _missedHold.ToList())
        {
            Note hold = kvp.Key;
            if(gameTime >= hold.endSec)
            {
                _missedHold.Remove(hold);
                _missedNotes.Add(hold);
                _judgedNotes.Add(hold);

                OnHoldEndJudgeResult?.Invoke(kvp.Value);
            }
        }
        

        // 从队列中的输入进行判定
        foreach(InputData inputData in _inputDataQueue)
        {
            TryHitNote(inputData.Position, inputData.Time, inputData.Type);
        }

        // 检查所有正在按压的 Hold 是否已到达尾部
        foreach (var kvp in _pressingHold.ToList())
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
                _pressingHold.Remove(hold);
                _judgedNotes.Add(hold);
            }
        }

        // 处理Hold中途松手的倒计时和miss
        foreach(KeyValuePair<Note, PressingHoldData> kvp in _pressingHold.ToList()) // 创建副本
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
                // 准备触发miss
                _pressingHold.Remove(hold);
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

        if (_pendingResults.Count > 0)
        {
            foreach (var result in _pendingResults)
            {
                OnJudgeResult?.Invoke(result);
            }
            _pendingResults.Clear();
        }
    }

    public void OnTapInput(Vector2 screenPos, double gameTime)
    {
        _inputDataQueue.Enqueue(new InputData
        {
            Position = screenPos,
            Time = gameTime,
            Type = InputType.Click
        });
        // TryHitNote(screenPos, gameTime, InputType.Click);
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
        _inputDataQueue.Enqueue(new InputData
        {
            Position = screenPos,
            Time = gameTime,
            Type = InputType.Flick
        });

        // TryHitNote(screenPos, gameTime, InputType.Flick);
    }

    public void ResetState()
    {
        _judgedNotes.Clear();
        _judgedNotesBuffer.Clear();
        _missedNotes.Clear();
        _pressingHold.Clear();
        _pendingResults.Clear();
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
        if (Chart == null || Player == null) return;
        
        for (int i = 0; i < _sortedAllNotes.Count; i++)
        {
            //GD.Print($"遍历Note候选:{i}");
            NoteCandidate candidate = _sortedAllNotes[i];
            
            Note note = candidate.Note;
            if (note == null || note.IsFake || _judgedNotes.Contains(note) || _missedNotes.Contains(note)) continue;
            if (!CanBeJudged(candidate.LineIndex, candidate.Note, pos, gameTime, inputType))
            {
                //GD.Print($"note不能被判定:{i}");
                continue;
            }
            if(_judgedNotesBuffer.ContainsKey(note)) continue; // 避免重复判定
                
            
            JudgeResult result = ResolveJudge(candidate.LineIndex, note, candidate.NoteIndex, gameTime, pos);
            //GD.Print($"Note_{i} 可以判定，判定结果:{result.Grade}, time:{gameTime}");

            if(note.Type == 3 || note.Type == 4 && gameTime < note.startSec)
            {
                _judgedNotesBuffer[note] = result;
            }
            else if(note.Type == 2)
            {
                _pressingHold[note] = new PressingHoldData
                {
                    CandidateData = candidate,
                    IsReleased = false,
                    Timer = 30f,
                    JudgeResult = result
                };

                // 调用玩家特效（线索引、位置、是否完美）
                Player.StartHoldHitEffect(note, GetNotePosition(candidate.LineIndex, note), result.Grade == JudgeGrade.Good, candidate.LineIndex);
                
            }
            else
            {
                _judgedNotes.Add(note);
                OnJudgeResult?.Invoke(result);
            }
            
            return;
        }
    }

    private bool CanBeJudged(int lineIndex, Note note, Vector2 screenPos, double gameTime, InputType inputType)
    {
        float deltaSec = (float)(gameTime - note.startSec);
        float deltaMs = deltaSec * 1000f;

        // 1. 输入类型判定
        if(note.Type == 1) // Tap
        {
            if(inputType != InputType.Click) return false;
        }
        else if(note.Type == 2) // Hold
        {
            if(inputType != InputType.Click) return false;
        }
        else if(note.Type == 3) // Flick
        {
            if(inputType != InputType.Flick) return false;
        }
        else if(note.Type == 4) // Drag
        {
            if(inputType != InputType.Touch) return false;
        }

        // 2. 时间窗口判定
        if(note.Type == 1) // Tap
        {
            if(deltaMs < -BadMs) return false; // 过早
            else if(deltaMs > GoodMs) return false; // 过晚
        }
        else if(note.Type == 2) // Hold
        {
            if(deltaMs < -GoodMs) return false; // 过早
            else if(deltaMs > GoodMs) return false; // 过晚
        }
        else if(note.Type == 3 || note.Type == 4) // Flick Drag
        {
            if(deltaMs < -GoodMs) return false; // 过早
            else if(deltaMs > GoodMs) return false; // 过晚
        }

        // 3. 距离判定
        float distanceToLine = DistanceToLine(lineIndex, screenPos, note);
        return distanceToLine <= HitDistancePixels;
        
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
        if (Player is not GameChartPlayer chartPlayer)
            return Vector2.Zero;

        FieldInfo lineMoveX = chartPlayer.GetType().GetField("_lineMoveX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        FieldInfo lineMoveY = chartPlayer.GetType().GetField("_lineMoveY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (lineMoveX == null || lineMoveY == null) return Vector2.Zero;

        float[] x = (float[])lineMoveX.GetValue(chartPlayer);
        float[] y = (float[])lineMoveY.GetValue(chartPlayer);
        if (x == null || y == null || lineIndex >= x.Length || lineIndex >= y.Length) return Vector2.Zero;
        return new Vector2(x[lineIndex], y[lineIndex]);
    }

    private float GetCurrentLineRotate(int lineIndex)
    {
        if (Player is not GameChartPlayer chartPlayer)
            return 0f;

        var field = chartPlayer.GetType().GetField("_lineRotate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return 0f;
        var arr = (float[])field.GetValue(chartPlayer);
        if (arr == null || lineIndex >= arr.Length) return 0f;
        return arr[lineIndex];
    }
}
