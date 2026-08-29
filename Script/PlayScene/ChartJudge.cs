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

public enum InputType
{
    Click, Touch, Flick
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

    private readonly List<NoteCandidate> _sortedAllNotes = new();
    private readonly HashSet<Note> _judgedNotes = new();
    private readonly HashSet<Note> _missedNotes = new();

    /// <summary>
    /// Drag和Flick如果提前判定，暂存进集合中，在击打时刻再判定
    /// </summary>
    private readonly Dictionary<Note, JudgeResult> _judgedNotesBuffer = new();
    private readonly HashSet<Note> _pressingHold = new();
    private readonly List<JudgeResult> _pendingResults = new();

    private float _holdDetectTimer = 2000; // 单位：ms

    public Chart Chart { get; private set; }
    public GameChartPlayer Player { get; private set; }
    public Control Parent { get; private set; }

    public Action<JudgeResult> OnJudgeResult { get; set; }

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
    }

    public void Update(double gameTime, double deltaTime)
    {
        if (Chart == null || Player == null) return;

        // 处理缓存的Drag和Flick
        foreach(KeyValuePair<Note, JudgeResult> kvp in _judgedNotesBuffer)
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
        
        // // 处理正在长按的Hold
        // _holdDetectTimer -= (float)deltaTime * 1000;
        // if(_holdDetectTimer < 0)
        // {
        //     // 检测所有hold
        //     foreach(Note hold in _pressingHold)
        //     {
        //         // 防御
        //         if(hold.Type != 2)
        //         {
        //             _pressingHold.Remove(hold);
        //             continue;
        //         }

        //         if(CanBeJudged())
        //     }

        //     while(_holdDetectTimer < 0) _holdDetectTimer += 2000;
        // }

        // 处理Miss判定
        foreach (NoteCandidate candidate in _sortedAllNotes)
        {
            Note note = candidate.Note;
            if (note == null || note.IsFake || _judgedNotes.Contains(note) || _missedNotes.Contains(note)) continue;

            float startSec = note.startSec;
            float endSec = note.Type == 2 ? note.endSec : startSec;
            bool shouldMiss = false;

            if (note.Type == 2)
            {
                if (gameTime > endSec + GoodMs / 1000f)
                {
                    shouldMiss = true;
                }
            }
            else if (gameTime > startSec + GoodMs / 1000f)
            {
                shouldMiss = true;
            }

            if (shouldMiss)
            {
                _missedNotes.Add(note);
                _judgedNotes.Add(note);
                _pendingResults.Add(new JudgeResult
                {
                    Note = note,
                    LineIndex = candidate.LineIndex,
                    NoteIndex = candidate.NoteIndex,
                    Grade = JudgeGrade.Miss,
                    TimeDeltaMs = (float)((gameTime - startSec) * 1000d),
                    HitPosition = GetNotePosition(candidate.LineIndex, note)
                });
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
        TryHitNote(screenPos, gameTime, InputType.Click);
    }

    public void OnTouchInput(Vector2 screenPos, double gameTime)
    {
        TryHitNote(screenPos, gameTime, InputType.Touch);
    }

    public void OnFlickInput(Vector2 screenPos, double gameTime)
    {
        TryHitNote(screenPos, gameTime, InputType.Flick);
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

        // string all = "所有note按照时间顺序排序:";
        // for (int i = 0; i < _sortedAllNotes.Count; i++)
        // {
        //     NoteCandidate candidate = _sortedAllNotes[i];
        //     all += $"line{candidate.LineIndex}_{candidate.NoteIndex}({candidate.Note.Type}), ";
        // }

        // GD.Print(all);

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
                
            
            JudgeResult result = ResolveJudge(candidate.LineIndex, note, candidate.NoteIndex, gameTime, pos);
            //GD.Print($"Note_{i} 可以判定，判定结果:{result.Grade}, time:{gameTime}");

            if(note.Type == 3 || note.Type == 4 && gameTime < note.startSec)
            {
                _judgedNotesBuffer[note] = result;
            }
            else if(note.Type == 2)
            {
                _pressingHold.Add(note);
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
