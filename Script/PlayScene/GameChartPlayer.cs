using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameChartPlayer : BaseChartPlayer
{
    public Image BgImage { get; set; }                //背景图片，由上级设置
    public AudioStream MusicAudio { get; set; }       //音乐，由上级设置

    #region 默认打击效果
    [ExportGroup("默认打击效果")]
    [Export] private AudioStream _defaultTapSound;
    [Export] private AudioStream _defaultDragSound;
    [Export] private AudioStream _defaultFlickSound;
    [Export] private SpriteFrames _defaultHitFrames; // 打击特效

    [ExportGroup("")]
    #endregion

    public AudioStreamPlayer audioStreamPlayer;

    private HitEffectPool hitEffectPool;
    
    private AudioPool audioPool;

    // ---- 每帧判定线状态缓存（替代 JudgeLineNode 的字段）----
    private float[] _lineMoveX;
    private float[] _lineMoveY;
    private float[] _lineRotate;
    private float[] _lineAlpha;
    private float[] _lineSpeed;
    private float[] _lineDisplacement;

    // ---- 打击记录（替代 NoteNode._hasPlayedHitSound）----
    // 只在播放模式下有意义；编辑模式下时间来回拖动时自动清理
    private readonly HashSet<Note> _playedNotes = new();

    // 判定为Perfect,Good(Early)的Tap音符需要提前隐藏，暂存在这里，到达实际的打击时间之后从集合中移除
    private readonly HashSet<Note> _earlyHideTap = new();

    private class HoldEffectData
    {
        public int LineIndex;
        public float Timer;   // 单位：ms
        public bool IsGood;
    }
    private readonly Dictionary<Note, HoldEffectData> _holdEffectData = new();

    // 预分配渲染数据数组，避免 List 扩容
    private JudgeLineRenderData[] _lineRenderBuffer;
    private NoteRenderData[] _noteRenderBuffer;
    private int _lineRenderCount = 0;
    private int _noteRenderCount = 0;

    public override (JudgeLineRenderData[] Data, int Count) GetLineRenderDatas()
        => (_lineRenderBuffer, _lineRenderCount);

    public override (NoteRenderData[] Data, int Count) GetNoteRenderDatas()
        => (_noteRenderBuffer, _noteRenderCount);

    public Control Parent { get; set; } // 所有JudgeLine和Note都将渲染到Parent中

    // private bool needRebuildLines = false;

    private bool _needsTopologyRebuild = false;

    //存储判定线父线关系的拓扑排序
    private List<int> _topologicalOrder = new();
    
    private double _startMusicTime;
    private double _startSystemTime;

    private readonly Color _perfectColor = new Color
    {
        R8 = 254,
        G8 = 255,
        B8 = 169,
        A8 = 255
    };

    private readonly Color _goodColor = new Color
    {
        R8 = 162,
        G8 = 238,
        B8 = 255,
        A8 = 255
    };

    private readonly Color _colorWhite = Colors.White;

    //将Beat（int[]）转换为秒
    public float BeatToSeconds(int[] beat)
    {
        return TimeUtil.BeatToSecond(beat, Chart?.BpmList);
    }

    public void TriggerHit(JudgeResult result)
    {
        Note note = result.Note;

        Color modulate;
        if(result.Grade == JudgeGrade.Perfect) modulate = _perfectColor;
        else if(result.Grade == JudgeGrade.Good) modulate = _goodColor;
        else modulate = _colorWhite;

        CreateHitEffect(result.HitPosition, modulate);
        PlayHitSound((NoteType)note.Type);

        // Tap 打击之后隐藏音符
        if(note.Type == 1 && result.TimeDeltaMs < 0)
        {
            _earlyHideTap.Add(note);
        }
    }

    private void TriggerHit(int lineIdx, Note note)
    {
        Vector2 parentPos = GetNoteJudgementPosition(lineIdx, note);

        // onNoteHited?.Invoke(parentPos);
        CreateHitEffect(parentPos);
        PlayHitSound((NoteType)note.Type);
    }

    public void StartHoldHitEffect(Note hold, Vector2 position, bool isGood, int lineIdx)
    {
        // 存储数据
        _holdEffectData[hold] = new HoldEffectData
        {
            LineIndex = lineIdx,
            Timer = 150f,
            IsGood = isGood
        };

        // 立即生成一次特效和音效
        Color modulate = isGood ? _goodColor : _perfectColor;
        CreateHitEffect(position, modulate);
        PlayHitSound(NoteType.Tap);
    }

    public void StopHoldHitEffect(Note hold)
    {
        _holdEffectData.Remove(hold);
    }

    public Vector2 GetNoteJudgementPosition(int lineIdx, Note note)
    {
        var linePos = new Vector2(_lineMoveX[lineIdx], _lineMoveY[lineIdx]);
        var notePos = new Vector2(note.PositionX, 0);
        var globalPos = PosUtil.GetChildGlobalPosition(linePos, notePos, _lineRotate[lineIdx]);
        return PosUtil.ChartPosToViewportPos(globalPos, Parent.Size);
    }

    /// <summary>
    /// 在指定位置创建一个打击特效
    /// </summary>
    public override void CreateHitEffect(Vector2 parentPos)
    {
        CreateHitEffect(parentPos, new Color(0.93f, 0.92f, 0.69f, 1f));
    }

    public override void CreateHitEffect(Vector2 parentPos, Color modulate)
    {
        if (hitEffectPool == null) return;
        hitEffectPool.Spawn(parentPos, modulate);
    }

    /// <summary>
    /// 播放打击音效
    /// </summary>
    /// <param name="noteType">note的类型</param>
    public void PlayHitSound(NoteType noteType)
    {
        //选择对应的音效
        AudioStream audioStream = noteType switch
        {
            NoteType.Tap => TapSound,
            NoteType.Hold => TapSound,
            NoteType.Flick => FlickSound,
            NoteType.Drag => DragSound,
            _ => TapSound
        };

        var player = audioPool.Get();
        player.Stream = audioStream;
        player.Play(); // 播放完成后自动回收（通过 Finished 信号）
    }

    public override void UseDefaultResource()
    {
        TapSound = _defaultTapSound;
        DragSound = _defaultDragSound;
        FlickSound = _defaultFlickSound;
        HitFrames = _defaultHitFrames;
    }


    public override void Initialize(Control parent, Chart chart, Image bgImage, AudioStream audio)
    {
        //1. 设置谱面
        Chart = chart;

        //2. 设置背景图片
        if (bgImage == null)
        {
            GD.PrintErr($"[{this.Name}] 背景图片导入失败: bgImage == null");
            return;
        }
        BgImage = bgImage;

        TextureRect bgNode = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(bgImage),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.3f, 0.3f, 0.3f, 1f),
            ZIndex = -999
        };
        bgNode.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(bgNode);

        //3. 设置音乐
        if (audio == null)
        {
            GD.PrintErr($"[{this.Name}] 音乐导入失败: audio == null");
            return;
        }
        MusicAudio = audio;

        audioStreamPlayer = new AudioStreamPlayer();
        audioStreamPlayer.Stream = MusicAudio;
        if (audioStreamPlayer.Stream == null)
        {
            GD.PrintErr($"[{this.Name}] 音乐文件加载失败");
            return;
        }

        AddChild(audioStreamPlayer);
        //设置音乐偏移
        chartOffset = (int)chart.Meta.Offset;

        // ===================其他设置===================

        Parent = parent;

        //设置打击特效
        hitEffectPool = new HitEffectPool(parent, HitFrames, 50);
        parent.AddChild(hitEffectPool);

        //设置打击音效
        audioPool = new AudioPool(parent);
        parent.AddChild(audioPool);

        //监听谱面数据变化
        ChartEventBus.LineCountChanged += OnLineCountChanged;
        ChartEventBus.LineFatherChanged += OnLineFatherChanged;
        ChartEventBus.NoteCountChanged += OnNoteCountChanged;

        //预计算所有事件时间的秒数
        ChartDataHelper.RefreshAllEventSec(chart);
        //预计算所有note时间的秒数
        ChartDataHelper.RefreshAllNoteSec(chart);
        //预计算所有速度事件的前缀和
        ChartDataHelper.RefreshAllEventPrefix(chart);
        //预计算所有note的累积位移
        ChartDataHelper.RefreshAllNoteAllDisplacement(chart);

        // 初始化所有判定线节点
        // SetJudgeLineList();

        _needsTopologyRebuild = true;
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        //取消订阅事件，防止内存泄漏
        ChartEventBus.LineCountChanged -= OnLineCountChanged;
        ChartEventBus.LineFatherChanged -= OnLineFatherChanged;
        ChartEventBus.NoteCountChanged -= OnNoteCountChanged;
    }

    private void OnLineCountChanged()
    {
        // 标记需要重新生成判定线拓扑排序，在每帧更新时处理
        _needsTopologyRebuild = true;
        // _playedNotes.Clear(); // 结构变化时重置打击记录
    }

    private void OnLineFatherChanged(int lineId, int father)
    {
        // 标记需要重新生成判定线拓扑排序，在每帧更新时处理
        _needsTopologyRebuild = true;
    }

    private void OnNoteCountChanged(int lineId)
    {
        // 音符数量变化不需要重建拓扑序，只需确保渲染缓冲区够大
        EnsureNoteBufferCapacity();
        _playedNotes.Clear();
    }

    public override void UpdateLogic(double deltaTime)
    {
        if(Chart == null) return;

        if (IsPlaying)
        {
            // 获取音乐当前播放位置（秒）
            // double musicTime = audioStreamPlayer.GetPlaybackPosition();

            // 系统级计时器
            double elapsedTime = Godot.Time.GetTicksUsec() / 1_000_000.0 - _startSystemTime;
            double musicTime = _startMusicTime + elapsedTime;

            // 应用偏移：谱面逻辑时间 = 音乐时间 - 偏移（偏移为正表示音乐滞后）
            ChartTime = musicTime - chartOffset / 1000.0;

            Time = musicTime;

            
        }
        else
        {
            ChartTime = ExternalTime;
        }

        // ---- 结构重建（仅增删判定线时触发）----
        if (_needsTopologyRebuild)
        {
            RebuildTopologyAndBuffers();
            _needsTopologyRebuild = false;
        }

        _lineRenderCount = 0;
        _noteRenderCount = 0;

        if (Disabled)
        {
            // 仅更新打击记录（时间回退时允许重新触发）
            UpdateHitRecordsOnly();
            return;
        }

        

        // 更新每条判定线及其上的音符
        // 按拓扑序更新：父线先于子线
        foreach (int idx in _topologicalOrder)
        {
            // JudgeLineNode judgeLineNode = judgeLineNodes[idx];
            // judgeLineNode.UpdateLine(ChartTime, _lineRenderBuffer, ref _lineRenderCount, 
            //     _noteRenderBuffer, ref _noteRenderCount);

            UpdateLine(ChartTime, idx);
        }

        // 在更新完所有线之后，更新 Hold 打击特效
        UpdateHoldEffects(deltaTime);
        
    }

    private void UpdateLine(double time, int index)
    {
        JudgeLine line = Chart.JudgeLineList[index];
        if (line?.EventLayers == null || line.EventLayers.Count == 0) return;

        // 我们需要综合所有事件层，第0层为主层
        {
            EventLayer layer = line.EventLayers[0];

            // 对每种事件类型进行插值 坐标系: 谱面坐标[-675,675] [-450,450]
            _lineMoveX[index] = ChartDataHelper.InterpolateEvent(layer.MoveXEvents, time, 0);
            _lineMoveY[index] = ChartDataHelper.InterpolateEvent(layer.MoveYEvents, time, 0);
            _lineRotate[index] = ChartDataHelper.InterpolateEvent(layer.RotateEvents, time, 0);
            _lineAlpha[index] = ChartDataHelper.InterpolateEvent(layer.AlphaEvents, time, 0);
            _lineSpeed[index] = ChartDataHelper.InterpolateEvent(layer.SpeedEvents, time, 0);
        }

        //其余1-3层的值叠加到第0层上
        for(int i = 1; i < line.EventLayers.Count; i++)
        {
            EventLayer layer = line.EventLayers[i];
            if(layer == null) continue;
            // 对每种事件类型进行插值 并叠加 坐标系: 谱面坐标[-675,675] [-450,450]
            _lineMoveX[index] += ChartDataHelper.InterpolateEvent(layer.MoveXEvents, time, 0);
            _lineMoveY[index] += ChartDataHelper.InterpolateEvent(layer.MoveYEvents, time, 0);
            _lineRotate[index] += ChartDataHelper.InterpolateEvent(layer.RotateEvents, time, 0);
            _lineAlpha[index] += ChartDataHelper.InterpolateEvent(layer.AlphaEvents, time, 0);
            _lineSpeed[index] += ChartDataHelper.InterpolateEvent(layer.SpeedEvents, time, 0);
        }

        //第4层：特殊事件
        //TODO 第4事件层 特殊事件

        //处理父判定线  father为-1代表没有父线
        if(line.Father >= 0)
        {
            // JudgeLineNode father = judgeLineNodes[line.Father];
            int father = line.Father;
            // 拓扑序保证 father 已经更新完毕
            // 将自己的坐标加上父线的坐标
            // 父线的位置和旋转会影响子线的位置，但不会影响子线的旋转
            // 这里不能直接将自己的坐标加上父线的坐标，因为父线的旋转会导致子线的位置变化
            Vector2 currentPos = PosUtil.GetChildGlobalPosition(     // 坐标系: 谱面坐标[-675,675] [-450,450]
                new Vector2(_lineMoveX[father], _lineMoveY[father]),
                new Vector2(_lineMoveX[index], _lineMoveY[index]),
                _lineRotate[father]
            );
            
            _lineMoveX[index] = currentPos.X; // 坐标系: 谱面坐标[-675,675] [-450,450]
            _lineMoveY[index] = currentPos.Y; 
        }

        //调整透明度
        _lineAlpha[index] = Math.Clamp(_lineAlpha[index], 0f, 255f);
        
        
        // 提前计算累计位移，供note使用（简化计算） // 坐标系: 谱面坐标
        float nowDisplacement = line.GetDisplacementAtTime((float)time); 
        _lineDisplacement[index] = nowDisplacement;

        // 写入LineBuffer
        Vector2 linePos = new Vector2(_lineMoveX[index], _lineMoveY[index]); //坐标系: 谱面坐标
        // int lineIdx = lineCount++;
        _lineRenderBuffer[_lineRenderCount++] = new JudgeLineRenderData
        {
            Pos = PosUtil.ChartPosToViewportPos(linePos, Parent.Size),
            Rotate = _lineRotate[index],
            Alpha = _lineAlpha[index]
        };

        // 更新该线上所有音符（音符位置受判定线速度和位置影响）
        if (line.Notes != null)
        {
            for (int i = 0; i < line.Notes.Count; i++)
            {
                UpdateNote(time, index, i);
            }
        }
    }

    private void UpdateNote(double gameTime, int lineId, int noteIndex)
    {
        Note note = Chart.JudgeLineList[lineId].Notes[noteIndex];
        if (note == null) return;

        float noteStartSec = note.startSec;
        float noteEndSec = note.EndTime != null ? note.endSec : noteStartSec;

        bool NoteVisible;
        bool HeadVisible = false;
        Vector2 Position;

        // ------------ 音符到达判定线时播放音效，并生成打击特效 ------------
        if(note.IsFake == false) // 假note不需要击打
        {
            float hitTime = noteStartSec; // 头部到达判定线的时间
            if (AutoHitEnabled && gameTime >= hitTime && !_playedNotes.Contains(note))
            {
                if (IsPlaying) // 只有播放状态下显示特效，编辑器滚动时不显示
                {
                    TriggerHit(lineId, note);
                }
                
                _playedNotes.Add(note);
            }
            else if (gameTime < hitTime)
            {
                _playedNotes.Remove(note);
            }
        }

        // _data.VisibleTime 音符可视时间（打击前多少秒开始显现，默认99999.0）
        // ------------ 处理显示和隐藏 ------------
        {
            if (_earlyHideTap.Contains(note))
            {
                if(gameTime >= noteStartSec) _earlyHideTap.Remove(note);
                
                return; // 不渲染
                
            }
            if(note.Type == 2) // hold需要特殊处理，当head到达判定线时，隐藏head的贴图
            {
                if(gameTime >= noteStartSec)
                {
                    HeadVisible = false;
                }
                else
                {
                    HeadVisible = true;
                }
            }
            float appearSec = noteStartSec - note.VisibleTime; // 出现时刻
            float disappearSec = noteEndSec; // 消失时刻（如果是长按，Hold尾部）
            if (gameTime < appearSec || gameTime > disappearSec)
            {
                // 不在显示区间内，隐藏
                // Visible = false;
                return; // 不计算位置，优化性能
            }
            else
            {
                // 在显示区间内，显示
                NoteVisible = true;
            }
        }

        //------------ 计算note位置 ------------     坐标系: 谱面坐标[-675,675] [-450,450]
        //相对于判定线的Y坐标 = 速度随时间变化的函数的积分
        //简单起见，这里分段计算位移，用到匀变速直线运动的公式
        //下落速度由判定线速度和note速度相乘共同决定
        //RPE中每个速度单位表示每秒下降120像素
        {
            float localChartX, localChartY; // 坐标系: 谱面坐标，相对于判定线
            localChartX = note.PositionX; 

            //全部位移
            float allDisplacement = note.allDisplacement; 

            localChartY = Math.Max(0, allDisplacement - _lineDisplacement[lineId]);

            //音符翻转 1表示上面，2表示下面
            if(note.Above == 2)
            {
                localChartY = -localChartY;
            }

            //设定位置
            Position = new Vector2(localChartX,localChartY);

            //GD.Print($"time:{gameTime:F3}, localChartX:{localChartX:F3}, allDisplacement:{allDisplacement:F3}, nowDisplacement:{nowDisplacement:F3}, localChartY:{localChartY:F3}, globalPos:{globalPos:F3}");
        }

        //设置透明度
        // Alpha = note.Alpha;

        //设置大小缩放
        // SizeX = note.Size;

        Vector2 EndPosition = Vector2.Zero;
        Vector2 BodyPosition;

        if(note.Type == 2)
        {
            //计算下落速度，由判定线速度和note速度共同决定
            //RPE中每个速度单位表示每秒下降120像素
            float speed = _lineSpeed[lineId] * note.Speed * 120; // 坐标系: 谱面坐标

            //计算end位置，可以视为在endTime的音符
            {
                
                float localChartY;

                //全部位移 坐标系: 谱面坐标
                float allDisplacement = note.endAllDisplacement;

                localChartY = Math.Max(0f, allDisplacement - _lineDisplacement[lineId]); // 坐标系: 谱面坐标

                EndPosition = new Vector2(note.PositionX, localChartY); // 坐标系: 谱面坐标
            }

            //计算body位置和大小
            {
                // 计算相对位置:head和end的中间
                Vector2 bodyPos = (Position + EndPosition) / 2f;
                
                //设定body位置 
                BodyPosition = bodyPos; // 坐标系: 谱面坐标[-675,675] [-450,450]
            }

            //GD.Print($"time:{gameTime:F3}, HeadPosition:{Position}, EndPosition:{EndPosition}, BodyScale:{BodyScale}");
        }

        // ================ 直接生成 NoteRenderData 写入缓冲 ================
        if(NoteVisible == false) return;

        int noteIdx = _noteRenderCount++;

        Vector2 linePos = new Vector2(_lineMoveX[lineId], _lineMoveY[lineId]);

        if(note.Type == 2)
        {
            // //计算note的全局坐标
            Vector2 headGlobalPos = PosUtil.GetChildGlobalPosition(
                linePos,
                Position,
                _lineRotate[lineId]
            );

            //谱面坐标转换为Parent坐标
            Vector2 headParentPos = PosUtil.ChartPosToViewportPos(
                headGlobalPos,
                Parent.Size
            );

            Vector2 endGlobalPos = PosUtil.GetChildGlobalPosition(
                linePos,
                EndPosition, 
                _lineRotate[lineId]
            );

            //谱面坐标转换为Parent坐标
            Vector2 endParentPos = PosUtil.ChartPosToViewportPos(
                endGlobalPos,
                Parent.Size
            );

            //计算旋转
            float noteRotation = _lineRotate[lineId];

            _noteRenderBuffer[noteIdx] = new NoteRenderData
            {
                Type = NoteType.Hold,
                HeadPos = headParentPos,
                EndPos = endParentPos,
                Rotate = noteRotation,
                Alpha = note.Alpha,
                HeadVisible = HeadVisible,
                SizeX = note.Size,
            };

        }
        else // Tap Flick Drag
        {
            Vector2 notePos = Position; // 坐标系：谱面坐标，相对于判定线

            //计算note的全局坐标 坐标系：谱面坐标
            Vector2 globalPos = PosUtil.GetChildGlobalPosition(
                linePos,
                notePos,
                _lineRotate[lineId]
            );

            //谱面坐标转换为Parent坐标
            Vector2 noteParentPos = PosUtil.ChartPosToViewportPos(
                globalPos,
                Parent.Size
            );

            //计算旋转
            float noteRotation = _lineRotate[lineId];
            
            _noteRenderBuffer[noteIdx] = new NoteRenderData
            {
                Type = (NoteType)note.Type,
                HeadPos = noteParentPos,
                EndPos = noteParentPos,
                Rotate = noteRotation,
                Alpha = note.Alpha,
                SizeX = note.Size,
            };
        }
    }

    /// <summary>
    /// ChartPlayer 禁用时，只更新打击记录（允许时间回退后重新触发）
    /// </summary>
    private void UpdateHitRecordsOnly()
    {
        _lineRenderCount = 0;
        _noteRenderCount = 0;

        foreach (JudgeLine line in Chart.JudgeLineList)
        {
            if (line.Notes == null) continue;
            foreach (Note note in line.Notes)
            {
                if (note.IsFake) continue;
                float hitTime = note.startSec;

                if (ChartTime >= hitTime && !_playedNotes.Contains(note))
                    _playedNotes.Add(note);
                else if (ChartTime < hitTime)
                    _playedNotes.Remove(note);
            }
        }
    }

    private void UpdateHoldEffects(double delta)
    {
        // 使用 ToList 避免在遍历时修改集合
        foreach (var kvp in _holdEffectData.ToList())
        {
            Note hold = kvp.Key;
            HoldEffectData data = kvp.Value;

            data.Timer -= (float)delta * 1000f;
            if (data.Timer <= 0)
            {
                // 获取 Hold 头部当前屏幕位置
                Vector2 pos = GetNoteJudgementPosition(data.LineIndex, hold);
                Color modulate = data.IsGood ? _goodColor : _perfectColor;
                CreateHitEffect(pos, modulate);

                // 重置计时器，保留溢出部分（防止累积误差）
                data.Timer += 150f;
                _holdEffectData[hold] = data;
            }
        }
    }


    // ==================== 缓冲区 & 拓扑管理 ====================

    private void RebuildTopologyAndBuffers()
    {
        RebuildBuffers();

        // 拓扑排序
        BuildTopologicalOrder();
    }

    private void RebuildBuffers()
    {
        int n = Chart.JudgeLineList?.Count ?? 0;

        // 分配/扩容状态缓存
        EnsureArraySize(ref _lineMoveX, n);
        EnsureArraySize(ref _lineMoveY, n);
        EnsureArraySize(ref _lineRotate, n);
        EnsureArraySize(ref _lineAlpha, n);
        EnsureArraySize(ref _lineSpeed, n);
        EnsureArraySize(ref _lineDisplacement, n);

        // 分配/扩容渲染缓冲
        if (_lineRenderBuffer == null || _lineRenderBuffer.Length < n)
            _lineRenderBuffer = new JudgeLineRenderData[MathUtil.NextPowerOfTwo(n)];

        EnsureNoteBufferCapacity();
    }

    private void EnsureNoteBufferCapacity()
    {
        int noteCount = 0;
        foreach (var line in Chart.JudgeLineList)
            if (line.Notes != null)
                noteCount += line.Notes.Count;

        if (_noteRenderBuffer == null || _noteRenderBuffer.Length < noteCount)
            _noteRenderBuffer = new NoteRenderData[MathUtil.NextPowerOfTwo(noteCount)];
    }

    private static void EnsureArraySize(ref float[] arr, int needed)
    {
        if (arr == null || arr.Length < needed)
            arr = new float[MathUtil.NextPowerOfTwo(needed)];
    }

    private void BuildTopologicalOrder()
    {
        _topologicalOrder.Clear();
        int n = Chart.JudgeLineList?.Count ?? 0;
        if (n == 0) return;

        // 计算入度
        int[] inDegree = new int[n];
        for (int i = 0; i < n; i++)
        {
            int father = Chart.JudgeLineList[i].Father;
            if (father >= 0 && father < n)
                inDegree[i]++;
        }

        // Kahn 算法
        Queue<int> queue = new();
        for (int i = 0; i < n; i++)
            if (inDegree[i] == 0) queue.Enqueue(i);

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            _topologicalOrder.Add(u);

            // 找到所有以 u 为父线的子线
            for (int v = 0; v < n; v++)
            {
                if (Chart.JudgeLineList[v].Father == u)
                {
                    inDegree[v]--;
                    if (inDegree[v] == 0)
                        queue.Enqueue(v);
                }
            }
        }

        // 防御：如果存在循环引用（理论上不应有），回退到原始顺序
        if (_topologicalOrder.Count != n)
        {
            GD.PrintErr($"[{Name}] 判定线存在循环引用，拓扑排序失败，回退到原始顺序");
            _topologicalOrder.Clear();
            for (int i = 0; i < n; i++) _topologicalOrder.Add(i);
        }
    }

    public override void Play(float time)
    {
        base.Play(time);
        audioStreamPlayer.Play(time);

        _startMusicTime = audioStreamPlayer.GetPlaybackPosition();
        _startSystemTime = Godot.Time.GetTicksUsec() / 1_000_000.0;

    }

    public override void Pause()
    {
        base.Pause();

        audioStreamPlayer.Stop();
    }

}
