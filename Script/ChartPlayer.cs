using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

public partial class ChartPlayer : BaseChartPlayer
{
    
    public ObservableCollection<Note> notes;

    public Image BgImage { get; set; }                //背景图片，由上级设置
    public AudioStream MusicAudio { get; set; }       //音乐，由上级设置

    #region 打击音效
    [ExportGroup("打击音效")]
    [Export] public AudioStream tapSound;
    [Export] public AudioStream dragSound;
    [Export] public AudioStream flickSound;

    [ExportGroup("")]
    #endregion

    public AudioStreamPlayer audioStreamPlayer;

    // public bool LogicDisabled { get; set; } // 是否禁用位置计算

    private HitEffectPool hitEffectPool;
    [Export] private SpriteFrames hitFrames; // 打击特效
    private AudioPool audioPool;

    private List<JudgeLineNode> judgeLineNodes = new();

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

    private bool needRebuildLines = false;

    

    //存储判定线父线关系的拓扑排序
    private List<int> _topologicalOrder = new();

    private void BuildTopologicalOrder()
    {
        _topologicalOrder.Clear();
        int n = judgeLineNodes.Count;
        if (n == 0) return;

        // 计算入度
        int[] inDegree = new int[n];
        for (int i = 0; i < n; i++)
        {
            int father = judgeLineNodes[i].Data.Father;
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
                if (judgeLineNodes[v].Data.Father == u)
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
    

    //将Beat（int[]）转换为秒
    public float BeatToSeconds(int[] beat)
    {
        return TimeUtil.BeatToSecond(beat, Chart?.BpmList);
    }


    private void SetJudgeLineList()
    {
        judgeLineNodes.Clear();
        if (Chart?.JudgeLineList == null) return;

        int noteCount = 0; // 统计note数量

        foreach (JudgeLine lineData in Chart.JudgeLineList)
        {
            // 为每条判定线创建一个节点
            var lineNode = new JudgeLineNode();
            int index = Chart.JudgeLineList.IndexOf(lineData);
            
            // 传入数据及对ChartPlayer的引用（用于时间转换等）、贴图、索引
            lineNode.SetData(lineData, this, index, judgeLineNodes); 
            
            judgeLineNodes.Add(lineNode);

            if(lineData.Notes != null)
                noteCount += lineData.Notes.Count;
        }

        // 预分配 buffer
        if (_lineRenderBuffer == null || _lineRenderBuffer.Length < judgeLineNodes.Count)
            _lineRenderBuffer = new JudgeLineRenderData[MathUtil.NextPowerOfTwo(judgeLineNodes.Count)];
        if (_noteRenderBuffer == null || _noteRenderBuffer.Length < noteCount)
            _noteRenderBuffer = new NoteRenderData[MathUtil.NextPowerOfTwo(noteCount)];

        // 构建拓扑序
        BuildTopologicalOrder();
    }

    /// <summary>
    /// 在指定位置创建一个打击特效
    /// </summary>
    public void CreateHitEffect(Vector2 parentPos)
    {
        hitEffectPool.Spawn(parentPos);
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
            NoteType.Tap => tapSound,
            NoteType.Hold => tapSound,
            NoteType.Flick => flickSound,
            NoteType.Drag => dragSound,
            _ => tapSound
        };

        var player = audioPool.Get();
        player.Stream = audioStream;
        player.Play(); // 播放完成后自动回收（通过 Finished 信号）
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
        hitEffectPool = new HitEffectPool(parent, hitFrames, 50);
        parent.AddChild(hitEffectPool);

        //设置打击音效
        audioPool = new AudioPool(parent);
        parent.AddChild(audioPool);

        //监听谱面数据变化
        ChartEventBus.OnChartDataChanged += OnChartDataChanged;

        //预计算所有事件时间的秒数
        ChartDataHelper.RefreshAllEventSec(chart);
        //预计算所有note时间的秒数
        ChartDataHelper.RefreshAllNoteSec(chart);
        //预计算所有速度事件的前缀和
        ChartDataHelper.RefreshAllEventPrefix(chart);
        //预计算所有note的累积位移
        ChartDataHelper.RefreshAllNoteAllDisplacement(chart);

        // 初始化所有判定线节点
        SetJudgeLineList();
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        //取消订阅事件，防止内存泄漏
        ChartEventBus.OnChartDataChanged -= OnChartDataChanged;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        // 处理谱面数据更新
        if (needRebuildLines)
        {
            SetJudgeLineList();
            needRebuildLines = false;

            GD.Print($"[{this.Name}] 成功重建谱面数据");
        }
    }

    private void OnChartDataChanged()
    {
        // 标记需要重新生成判定线节点，在每帧更新时处理
        needRebuildLines = true;
    }

    /// <summary>
    /// 生成渲染数据 坐标系: Parent坐标（已弃用）
    /// </summary>
    // private void CreateRenderDatas()
    // {
    //     // 生成渲染数据 坐标系: Parent坐标
    //     judgeLineRenderDatas.Clear();
    //     noteRenderDatas.Clear();
    //     for(int lineId = 0; lineId < judgeLineNodes.Count; lineId++)
    //     {
    //         JudgeLineNode judgeLineNode = judgeLineNodes[lineId];

    //         Vector2 linePos = new Vector2(judgeLineNode.CurrentMoveX, judgeLineNode.CurrentMoveY);//坐标系: 谱面坐标

    //         //谱面坐标转换为Parent坐标
    //         Vector2 lineParentPos = PosUtil.ChartPosToViewportPos(
    //             linePos,
    //             Parent.Size
    //         );

    //         JudgeLineRenderData lineRenderData = new()
    //         {
    //             Pos = lineParentPos,
    //             Rotate = judgeLineNode.CurrentRotate,
    //             Alpha = judgeLineNode.CurrentAlpha
    //         };
    //         judgeLineRenderDatas.Add(lineRenderData);


    //         for(int noteIndex = 0;noteIndex < judgeLineNode.noteNodes.Count; noteIndex++)
    //         {
    //             NoteNode noteNode = judgeLineNode.noteNodes[noteIndex];

    //             if(noteNode.Visible == false) continue;

    //             if(noteNode is HoldNoteNode holdNoteNode)
    //             {
    //                 // //计算note的全局坐标
    //                 Vector2 headGlobalPos = PosUtil.GetChildGlobalPosition(
    //                     linePos,
    //                     holdNoteNode.Position,
    //                     judgeLineNode.CurrentRotate
    //                 );

    //                 //谱面坐标转换为Parent坐标
    //                 Vector2 headParentPos = PosUtil.ChartPosToViewportPos(
    //                     headGlobalPos,
    //                     Parent.Size
    //                 );

    //                 Vector2 endGlobalPos = PosUtil.GetChildGlobalPosition(
    //                     linePos,
    //                     holdNoteNode.EndPosition, 
    //                     judgeLineNode.CurrentRotate
    //                 );

    //                 //谱面坐标转换为Parent坐标
    //                 Vector2 endParentPos = PosUtil.ChartPosToViewportPos(
    //                     endGlobalPos,
    //                     Parent.Size
    //                 );

    //                 //计算旋转
    //                 float noteRotation = judgeLineNode.CurrentRotate;

    //                 NoteRenderData noteRenderData = new()
    //                 {
    //                     Type = NoteType.Hold,
    //                     HeadPos = headParentPos,
    //                     EndPos = endParentPos,
    //                     Rotate = noteRotation,
    //                     Alpha = noteNode.Alpha,
    //                     HeadVisible = noteNode.HeadVisible,
    //                     SizeX = noteNode.SizeX,
    //                 };

    //                 noteRenderDatas.Add(noteRenderData);

    //             }
    //             else // Tap Flick Drag
    //             {
    //                 Vector2 notePos = noteNode.Position; // 坐标系：谱面坐标，相对于判定线

    //                 //计算note的全局坐标 坐标系：谱面坐标
    //                 Vector2 globalPos = PosUtil.GetChildGlobalPosition(
    //                     linePos,
    //                     notePos,
    //                     judgeLineNode.CurrentRotate
    //                 );

    //                 //谱面坐标转换为Parent坐标
    //                 Vector2 noteParentPos = PosUtil.ChartPosToViewportPos(
    //                     globalPos,
    //                     Parent.Size
    //                 );

    //                 //计算旋转
    //                 float noteRotation = judgeLineNode.CurrentRotate;

    //                 NoteRenderData noteRenderData = new()
    //                 {
    //                     Type = (NoteType)noteNode.data.Type,
    //                     HeadPos = noteParentPos,
    //                     EndPos = noteParentPos,
    //                     Rotate = noteRotation,
    //                     Alpha = noteNode.Alpha,
    //                     SizeX = noteNode.SizeX,
    //                 };

    //                 noteRenderDatas.Add(noteRenderData);
    //             }
    //         }

    //     }
    // }

    public override void UpdateLogic()
    {
        if(Chart == null) return;

        if (IsPlaying)
        {
            // 获取音乐当前播放位置（秒）
            double musicTime = audioStreamPlayer.GetPlaybackPosition();
            // 应用偏移：谱面逻辑时间 = 音乐时间 - 偏移（偏移为正表示音乐滞后）
            ChartTime = musicTime - chartOffset / 1000.0;

            Time = musicTime;
        }
        else
        {
            ChartTime = ExternalTime;
        }

        if (Disabled)
        {
            // 建议清空计数，避免 Render 误读到上一帧残留
            _lineRenderCount = 0;
            _noteRenderCount = 0;

            // 需要刷新所有note的_hasPlayedHitSound
            foreach(JudgeLineNode judgeLineNode in judgeLineNodes)
            {
                foreach(NoteNode noteNode in judgeLineNode.noteNodes)
                {
                    noteNode.UpdateHitRecord(ChartTime);
                }
            }

            return;
        }

        _lineRenderCount = 0;
        _noteRenderCount = 0;

        // 更新每条判定线及其上的音符
        // 按拓扑序更新：父线先于子线
        foreach (int idx in _topologicalOrder)
        {
            JudgeLineNode judgeLineNode = judgeLineNodes[idx];
            judgeLineNode.UpdateLine(ChartTime, _lineRenderBuffer, ref _lineRenderCount, 
            _noteRenderBuffer, ref _noteRenderCount);
        }
        
    }

    public override void Play(float time)
    {
        audioStreamPlayer.Play(time);
        IsPlaying = true;
    }

    public override void Pause()
    {
        audioStreamPlayer.Stop();
        IsPlaying = false;
    }
}

/// <summary>
/// 代表一条判定线的节点
/// </summary>
public class JudgeLineNode
{
    public JudgeLine Data { get; set; }                 // 原始数据
    private ChartPlayer _chartPlayer;         // 用于获取BPM等
    public List<NoteNode> noteNodes = new(); // 该线上的音符节点
    public int _index;                       //索引

    public float nowDisplacement;         // 从0到当前时刻累积的所有位移

    
    // 当前帧的事件插值结果
    public float CurrentMoveX { get; set; } = 0;
    public float CurrentMoveY { get; set; } = 0;
    public float CurrentRotate { get; set; } = 0;
    public float CurrentAlpha { get; set; } = 1;
    public float CurrentSpeed { get; set; } = 1; // 速度系数

    private List<JudgeLineNode> judgeLineNodes; // 所有的判定线节点，由ChartPlayer传入，用于获取父线

    public void SetData(JudgeLine data, ChartPlayer player, int index, List<JudgeLineNode> judgeLineNodes)
    {
        Data = data;
        _chartPlayer = player;
        _index = index;
        this.judgeLineNodes = judgeLineNodes;

        // 创建该线上的所有音符节点
        if (Data.Notes != null)
        {
            for (int i = 0; i < Data.Notes.Count; i++)
            {
                Note noteData = Data.Notes[i];

                NoteNode noteNode;
                
                // 根据类型创建具体的音符节点
                if (noteData.Type == 2) // Hold
                {
                    var holdNode = new HoldNoteNode();
                    holdNode.SetData(noteData, this, _chartPlayer, i);
                    noteNode = holdNode;
                }
                else
                {
                    noteNode = new NoteNode();
                    noteNode.SetData(noteData, this, _chartPlayer, i);
                }

                // 用于生成打击特效
                noteNode.onNoteHited += (Vector2 parentPos) => { // 坐标系：parent坐标
                    _chartPlayer.onNoteHited?.Invoke(parentPos); 
                    _chartPlayer.CreateHitEffect(parentPos);
                    _chartPlayer.PlayHitSound((NoteType)noteData.Type); // NoteType枚举类型与谱面文件的数字对应，可以强转
                };

                noteNodes.Add(noteNode);
            }
        }
    }


    /// <summary>
    /// 根据当前游戏时间更新判定线状态
    /// </summary>
    public void UpdateLine(double gameTime, JudgeLineRenderData[] lineBuffer, ref int lineCount,
        NoteRenderData[] noteBuffer, ref int noteCount)
    {
        if (Data?.EventLayers == null || Data.EventLayers.Length == 0) return;

        // 我们需要综合所有事件层，第0层为主层
        {
            EventLayer layer = Data.EventLayers[0];

            // 对每种事件类型进行插值 坐标系: 谱面坐标[-675,675] [-450,450]
            CurrentMoveX = ChartDataHelper.InterpolateEvent(layer.MoveXEvents, gameTime, 0);
            CurrentMoveY = ChartDataHelper.InterpolateEvent(layer.MoveYEvents, gameTime, 0);
            CurrentRotate = ChartDataHelper.InterpolateEvent(layer.RotateEvents, gameTime, 0);
            CurrentAlpha = ChartDataHelper.InterpolateEvent(layer.AlphaEvents, gameTime, 0);
            CurrentSpeed = ChartDataHelper.InterpolateEvent(layer.SpeedEvents, gameTime, 0);
        }

        //其余1-3层的值叠加到第0层上
        for(int i = 1; i <= 3; i++)
        {
            if(i > Data.EventLayers.Length - 1) break;
            EventLayer layer = Data.EventLayers[i];
            if(layer == null) continue;
            // 对每种事件类型进行插值 并叠加 坐标系: 谱面坐标[-675,675] [-450,450]
            CurrentMoveX += ChartDataHelper.InterpolateEvent(layer.MoveXEvents, gameTime, 0);
            CurrentMoveY += ChartDataHelper.InterpolateEvent(layer.MoveYEvents, gameTime, 0);
            CurrentRotate += ChartDataHelper.InterpolateEvent(layer.RotateEvents, gameTime, 0);
            CurrentAlpha += ChartDataHelper.InterpolateEvent(layer.AlphaEvents, gameTime, 0);
            CurrentSpeed += ChartDataHelper.InterpolateEvent(layer.SpeedEvents, gameTime, 0);
        }

        //第4层：特殊事件
        //TODO 第4事件层 特殊事件

        //处理父判定线  father为-1代表没有父线
        if(Data.Father >= 0)
        {
            JudgeLineNode father = judgeLineNodes[Data.Father];
            // 拓扑序保证 father 已经更新完毕
            // 将自己的坐标加上父线的坐标
            // 父线的位置和旋转会影响子线的位置，但不会影响子线的旋转
            // 这里不能直接将自己的坐标加上父线的坐标，因为父线的旋转会导致子线的位置变化
            Vector2 currentPos = PosUtil.GetChildGlobalPosition(     // 坐标系: 谱面坐标[-675,675] [-450,450]
                new Vector2(father.CurrentMoveX, father.CurrentMoveY),
                new Vector2(CurrentMoveX, CurrentMoveY),
                father.CurrentRotate
            );
            
            CurrentMoveX = currentPos.X; // 坐标系: 谱面坐标[-675,675] [-450,450]
            CurrentMoveY = currentPos.Y; 
        }

        //调整透明度
        CurrentAlpha = Math.Clamp(CurrentAlpha, 0f, 255f);
        
        
        // 提前计算累计位移，供note使用（简化计算） // 坐标系: 谱面坐标
        nowDisplacement = ChartDataHelper.GetDisplacementAtTime(
            Data.EventLayers[0].SpeedEvents, 
            (float)gameTime
        ); 

        // 写入LineBuffer
        Vector2 linePos = new Vector2(CurrentMoveX, CurrentMoveY); //坐标系: 谱面坐标
        int lineIdx = lineCount++;
        lineBuffer[lineIdx] = new JudgeLineRenderData
        {
            Pos = PosUtil.ChartPosToViewportPos(linePos, _chartPlayer.Parent.Size),
            Rotate = CurrentRotate,
            Alpha = CurrentAlpha
        };

        // 更新该线上所有音符（音符位置受判定线速度和位置影响）
        foreach (var noteNode in noteNodes)
        {
            noteNode.UpdateNote(gameTime);

            // 直接生成 NoteRenderData 写入缓冲
            if(noteNode.Visible == false) continue;

            int noteIdx = noteCount++;

            if(noteNode is HoldNoteNode holdNoteNode)
            {
                // //计算note的全局坐标
                Vector2 headGlobalPos = PosUtil.GetChildGlobalPosition(
                    linePos,
                    holdNoteNode.Position,
                    CurrentRotate
                );

                //谱面坐标转换为Parent坐标
                Vector2 headParentPos = PosUtil.ChartPosToViewportPos(
                    headGlobalPos,
                    _chartPlayer.Parent.Size
                );

                Vector2 endGlobalPos = PosUtil.GetChildGlobalPosition(
                    linePos,
                    holdNoteNode.EndPosition, 
                    CurrentRotate
                );

                //谱面坐标转换为Parent坐标
                Vector2 endParentPos = PosUtil.ChartPosToViewportPos(
                    endGlobalPos,
                    _chartPlayer.Parent.Size
                );

                //计算旋转
                float noteRotation = CurrentRotate;

                noteBuffer[noteIdx] = new NoteRenderData
                {
                    Type = NoteType.Hold,
                    HeadPos = headParentPos,
                    EndPos = endParentPos,
                    Rotate = noteRotation,
                    Alpha = noteNode.Alpha,
                    HeadVisible = noteNode.HeadVisible,
                    SizeX = noteNode.SizeX,
                };

            }
            else // Tap Flick Drag
            {
                Vector2 notePos = noteNode.Position; // 坐标系：谱面坐标，相对于判定线

                //计算note的全局坐标 坐标系：谱面坐标
                Vector2 globalPos = PosUtil.GetChildGlobalPosition(
                    linePos,
                    notePos,
                    CurrentRotate
                );

                //谱面坐标转换为Parent坐标
                Vector2 noteParentPos = PosUtil.ChartPosToViewportPos(
                    globalPos,
                    _chartPlayer.Parent.Size
                );

                //计算旋转
                float noteRotation = CurrentRotate;
                
                noteBuffer[noteIdx] = new NoteRenderData
                {
                    Type = (NoteType)noteNode.data.Type,
                    HeadPos = noteParentPos,
                    EndPos = noteParentPos,
                    Rotate = noteRotation,
                    Alpha = noteNode.Alpha,
                    SizeX = noteNode.SizeX,
                };
            }
        }
    }
    
}

/// <summary>
/// 代表一个音符的节点
/// </summary>
public class NoteNode
{
    public Note data;
    protected ChartPlayer _chartPlayer;
    protected int _index;
    protected JudgeLineNode _judgeLineNode;

    private bool _hasPlayedHitSound = false;//用于标记是否已播放过音效

    /// <summary>在铺面坐标系下的本地坐标</summary>
    protected Vector2 localChartPos = new Vector2(); 

    public bool HeadVisible { get; set; }
    public bool Visible { get; set; }
    public Vector2 Position { get; set; } //坐标系: 谱面坐标[-675,675] [-450,450]

    /// <summary>
    /// note透明度 [0,255]
    /// </summary>
    /// <value></value>
    public float Alpha { get; set; } = 255; // 

    /// <summary>
    /// note的横向大小缩放 [0,1]
    /// </summary>
    /// <value></value>
    public float SizeX { get; set; } = 1;

    /// <summary>
    /// 当note落到判定线上时触发，参数是点击的位置(坐标系：parent坐标)
    /// </summary>
    public Action<Vector2> onNoteHited; 
    

    public void SetData(Note data, JudgeLineNode line, ChartPlayer player, int index)
    {
        this.data = data;
        _judgeLineNode = line;
        _chartPlayer = player;
        _index = index;
    }

    /// <summary>
    /// 只更新note的_hasPlayedHitSound，不生成实际打击特效
    /// 用于在ChartPlayer禁用时实时更新
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public void UpdateHitRecord(double gameTime)
    {
        if (data == null) return;

        float hitTime = data.startSec; // 头部到达判定线的时间

        // 音符到达判定线时播放音效，并生成打击特效
        if(!data.IsFake) // 假note不需要击打
        {
            if (gameTime >= hitTime && !_hasPlayedHitSound)
            {   
                _hasPlayedHitSound = true;
            }
            else if (gameTime < hitTime)
            {
                // 时间回退到击中点之前，重置标记，允许再次触发
                _hasPlayedHitSound = false;
            }
        }
    }
    
    /// <summary>
    /// 更新音符位置（受判定线位置和速度影响）
    /// 可被HoldNoteNode重写
    /// </summary>
    public virtual void UpdateNote(double gameTime)
    {
        if (data == null) return;

        float noteStartSec = data.startSec;
        float noteEndSec = data.EndTime != null ? data.endSec : noteStartSec;

        // ------------ 音符到达判定线时播放音效，并生成打击特效 ------------
        if(data.IsFake == false) // 假note不需要击打
        {
            float hitTime = noteStartSec; // 头部到达判定线的时间
            if (gameTime >= hitTime && !_hasPlayedHitSound)
            {
                if (_chartPlayer.IsPlaying) // 只有播放状态下显示特效，编辑器滚动时不显示
                {
                    // 播放音效并生成打击特效
                    //PlayHitSound();

                    //显示打击特效
                    //理论上此时note应该在的位置，防止note速度过快导致的误差
                    Vector2 calculatedLocalChartPos = new Vector2(data.PositionX, 0); 
                    Vector2 globalChartPos = PosUtil.GetChildGlobalPosition(
                        new Vector2(_judgeLineNode.CurrentMoveX, _judgeLineNode.CurrentMoveY),
                        calculatedLocalChartPos,
                        _judgeLineNode.CurrentRotate
                    );

                    Vector2 parentPos = PosUtil.ChartPosToViewportPos(
                        globalChartPos,
                        _chartPlayer.Parent.Size
                    );

                    onNoteHited?.Invoke(parentPos);
                    //_chartPlayer.CreateHitEffect(globalChartPos); // 坐标系：谱面坐标
                }
                
                _hasPlayedHitSound = true;
            }
            else if (gameTime < hitTime)
            {
                // 时间回退到击中点之前，重置标记，允许再次触发
                _hasPlayedHitSound = false;
            }
        }

        // _data.VisibleTime 音符可视时间（打击前多少秒开始显现，默认99999.0）
        // ------------ 处理显示和隐藏 ------------
        {
            if(data.Type == 2) // hold需要特殊处理，当head到达判定线时，隐藏head的贴图
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
            float appearSec = noteStartSec - data.VisibleTime; // 出现时刻
            float disappearSec = noteEndSec; // 消失时刻（如果是长按，Hold尾部）
            if (gameTime < appearSec || gameTime > disappearSec)
            {
                // 不在显示区间内，隐藏
                Visible = false;
                return; // 不计算位置，优化性能
            }
            else
            {
                // 在显示区间内，显示
                Visible = true;
            }
        }

        //------------ 计算note位置 ------------     坐标系: 谱面坐标[-675,675] [-450,450]
        //相对于判定线的Y坐标 = 速度随时间变化的函数的积分
        //简单起见，这里分段计算位移，用到匀变速直线运动的公式
        //下落速度由判定线速度和note速度相乘共同决定
        //RPE中每个速度单位表示每秒下降120像素
        {
            float localChartX, localChartY; // 坐标系: 谱面坐标，相对于判定线
            localChartX = data.PositionX; 

            //全部位移
            // float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, noteStartSec);
            // float allDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine._data.EventLayers[0].SpeedEvents, noteStartSec);
            float allDisplacement = data.allDisplacement;

            //note已经移动的位移
            // float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
            // float nowDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
            float nowDisplacement = _judgeLineNode.nowDisplacement;

            localChartY = Math.Max(0, allDisplacement - nowDisplacement);

            //音符翻转 1表示上面，2表示下面
            if(data.Above == 2)
            {
                localChartY = -localChartY;
            }

            localChartPos = new Vector2(localChartX,localChartY);
            
            //设定位置
            Position = localChartPos; // 坐标系: 谱面坐标，相对于判定线

            //GD.Print($"time:{gameTime:F3}, localChartX:{localChartX:F3}, allDisplacement:{allDisplacement:F3}, nowDisplacement:{nowDisplacement:F3}, localChartY:{localChartY:F3}, globalPos:{globalPos:F3}");
        }

        //设置透明度
        Alpha = data.Alpha;

        //设置大小缩放
        SizeX = data.Size;

    }
}

/// <summary>
/// 代表一个Hold音符的节点
/// </summary>
public class HoldNoteNode : NoteNode
{
    //private Texture2D _bodyTexture;
    //private Texture2D _endTexture;
    //private Sprite2D _bodySprite;
    //private Sprite2D _endSprite;

    public Vector2 EndPosition { get; set; } // 坐标系: 谱面坐标[-675,675] [-450,450] 相对于判定线
    public Vector2 BodyPosition { get; set; } // 坐标系: 谱面坐标[-675,675] [-450,450] 相对于判定线
    public float BodyScale { get; set; }

    private Vector2 endLocalChartPos; //在铺面坐标系下end的本地坐标 坐标系: 谱面坐标[-675,675] [-450,450]

    public override void UpdateNote(double gameTime)
    {
        // 先调用基类更新头部位置和可见性
        base.UpdateNote(gameTime);

        //计算下落速度，由判定线速度和note速度共同决定
        //RPE中每个速度单位表示每秒下降120像素
        float speed = _judgeLineNode.CurrentSpeed * data.Speed * 120; // 坐标系: 谱面坐标
        float startSec = data.startSec;
        float endSec = data.endSec;

        //计算end位置，可以视为在endTime的音符
        {
            //第一阶段：head到达之前，localPosition保持变不变
            //第二阶段：hold正在缩小，localPosition不断减小至y=0
            //else if(gameTime > startSec && gameTime < endSec)
            // if(gameTime < endSec)
            {
                float localChartY;
                //全部位移 坐标系: 谱面坐标
                // float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, endSec);
                // float allDisplacement = ChartDataHelper.GetDisplacementAtTime(_judgeLineNode.Data.EventLayers[0].SpeedEvents, endSec);
                float allDisplacement = data.endAllDisplacement;

                // 缓存 endSec 对应的位移（Hold 的 endTime 不变）
                // if (_cachedEndSec != data.endSec)
                // {
                //     _cachedEndSec = data.endSec;
                //     _cachedEndDisplacement = ChartDataHelper.GetDisplacementAtTime(
                //         _judgeLineNode.Data.EventLayers[0].SpeedEvents, _cachedEndSec);
                // }

                // note已经移动的位移 坐标系: 谱面坐标
                // float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
                // float nowDisplacement = ChartDataHelper.GetDisplacementAtTime(_judgeLineNode.Data.EventLayers[0].SpeedEvents, (float)gameTime);
                float nowDisplacement = _judgeLineNode.nowDisplacement;

                localChartY = Math.Max(0f, allDisplacement - nowDisplacement); // 坐标系: 谱面坐标
                //GD.Print($"localChartY:{localChartY}");

                endLocalChartPos = new Vector2(data.PositionX, localChartY); // 坐标系: 谱面坐标

                EndPosition = endLocalChartPos;

                //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
                //Vector2 viewportPos = PosUtil.ChartPosToLocalPos(endLocalChartPos, _chartPlayer.Parent.Size);

                //设定位置
                //EndPosition = viewportPos;

                //holdLength = viewportPos.Y;

                
            }
            //第三阶段：hold结束，隐藏自己
            //由于父类设置了隐藏，所以这里不需要进行任何操作

            // 计算holdEnd的全局坐标 坐标系：谱面坐标
            //{
                //计算holdEnd相对于判定线的坐标 坐标系：谱面坐标
                //Vector2 linePos = localChartPos + new Vector2(0, holdLength);

                //计算holdEnd的全局坐标 坐标系：谱面坐标
                // Vector2 globalPos = PosUtil.GetChildGlobalPosition(
                //     new Vector2(fatherLine.CurrentMoveX, fatherLine.CurrentMoveY),
                //     endLocalChartPos,
                //     fatherLine.CurrentRotate
                // );
                // GD.Print($"globalPos:{globalPos}");

                //EndPosition = globalPos;
            //}
            

        }

        //计算body位置和大小
        {
            // 计算相对位置:head和end的中间
            Vector2 bodyPos = (Position + EndPosition) / 2f;

            //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
            //Vector2 viewportPos = PosUtil.ChartPosToLocalPos(bodyLocalChartPos, _chartPlayer.Parent.Size);
            
            //设定body位置 
            BodyPosition = bodyPos; // 坐标系: 谱面坐标[-675,675] [-450,450]

            //hold原尺寸为1900，缩放后为sizeY
            // float sizeY = EndPosition.Y - Position.Y;
            //_bodySprite.Scale = new Vector2(_chartPlayer.noteWidthScale, sizeY/1900f);

            // GD.Print($"time:{gameTime:F4}, bodyPos:{bodyPos}, sizeY:{sizeY}");
        }

        //GD.Print($"time:{gameTime:F3}, HeadPosition:{Position}, EndPosition:{EndPosition}, BodyScale:{BodyScale}");
    }
}

public struct JudgeLineRenderData
{
    // 当前帧的事件插值结果

    public Vector2 Pos { get; set; }
    public float Rotate { get; set; }
    public float Alpha { get; set; }
}

public struct NoteRenderData
{
    public Vector2 HeadPos { get; set; }
    public NoteType Type { get; set; }
    public float Rotate { get; set; }
    public float Alpha { get; set; }
    public float SizeX { get; set; }

    //仅限Hold的属性
    public Vector2 EndPos { get; set; }
    public bool HeadVisible { get; set; }
}
