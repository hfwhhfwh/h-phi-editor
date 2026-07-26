using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class ChartPlayer : BaseChartPlayer
{
    


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

    private List<JudgeLineRenderData> judgeLineRenderDatas = new();
    private List<NoteRenderData> noteRenderDatas = new();

    public override List<JudgeLineRenderData> GetLineRenderDatas() => judgeLineRenderDatas;
    public override List<NoteRenderData> GetNoteRenderDatas() => noteRenderDatas;

    public Control Parent { get; set; } // 所有JudgeLine和Note都将渲染到Parent中
    

    //将Beat（int[]）转换为秒
    public float BeatToSeconds(int[] beat)
    {
        return TimeUtil.BeatToSecond(beat, Chart?.BpmList);
    }


    private void SetJudgeLineList()
    {
        judgeLineNodes.Clear();
        if (Chart?.JudgeLineList == null) return;

        foreach (JudgeLine lineData in Chart.JudgeLineList)
        {
            // 为每条判定线创建一个节点
            var lineNode = new JudgeLineNode();
            int index = Array.IndexOf(Chart.JudgeLineList, lineData);
            
            // 传入数据及对ChartPlayer的引用（用于时间转换等）、贴图、索引
            lineNode.SetData(lineData, this, index, judgeLineNodes); 
            
            judgeLineNodes.Add(lineNode);
        }
    }

    /// <summary>
    /// 在指定位置创建一个打击特效
    /// </summary>
    public void CreateHitEffect(Vector2 parentPos)
    {
        hitEffectPool.Spawn(parentPos);
    }

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

        //预计算所有事件时间的秒数
        ChartDataHelper.RefreshEventSec(chart);
        //预计算所有note时间的秒数
        ChartDataHelper.RefreshNoteSec(chart);
        //预计算所有速度事件的前缀和
        ChartDataHelper.RefreshAllEventPrefix(chart);
        //预计算所有note的累积位移
        ChartDataHelper.RefreshAllNoteAllDisplacement(chart);

        // 初始化所有判定线节点
        SetJudgeLineList();
    }

    /// <summary>
    /// 生成渲染数据 坐标系: Parent坐标
    /// </summary>
    private void CreateRenderDatas()
    {
        // 生成渲染数据 坐标系: Parent坐标
        judgeLineRenderDatas.Clear();
        noteRenderDatas.Clear();
        for(int lineId = 0; lineId < judgeLineNodes.Count; lineId++)
        {
            JudgeLineNode judgeLineNode = judgeLineNodes[lineId];

            Vector2 linePos = new Vector2(judgeLineNode.CurrentMoveX, judgeLineNode.CurrentMoveY);//坐标系: 谱面坐标

            //谱面坐标转换为Parent坐标
            Vector2 lineParentPos = PosUtil.ChartPosToViewportPos(
                linePos,
                Parent.Size
            );

            JudgeLineRenderData lineRenderData = new()
            {
                Pos = lineParentPos,
                Rotate = judgeLineNode.CurrentRotate,
                Alpha = judgeLineNode.CurrentAlpha
            };
            judgeLineRenderDatas.Add(lineRenderData);


            for(int noteIndex = 0;noteIndex < judgeLineNode.noteNodes.Count; noteIndex++)
            {
                NoteNode noteNode = judgeLineNode.noteNodes[noteIndex];

                if(noteNode.Visible == false) continue;

                if(noteNode is HoldNoteNode holdNoteNode)
                {
                    // //计算note的全局坐标
                    Vector2 headGlobalPos = PosUtil.GetChildGlobalPosition(
                        linePos,
                        holdNoteNode.Position,
                        judgeLineNode.CurrentRotate
                    );

                    //谱面坐标转换为Parent坐标
                    Vector2 headParentPos = PosUtil.ChartPosToViewportPos(
                        headGlobalPos,
                        Parent.Size
                    );

                    Vector2 endGlobalPos = PosUtil.GetChildGlobalPosition(
                        linePos,
                        holdNoteNode.EndPosition, 
                        judgeLineNode.CurrentRotate
                    );

                    //谱面坐标转换为Parent坐标
                    Vector2 endParentPos = PosUtil.ChartPosToViewportPos(
                        endGlobalPos,
                        Parent.Size
                    );

                    //计算旋转
                    float noteRotation = judgeLineNode.CurrentRotate;

                    NoteRenderData noteRenderData = new()
                    {
                        Type = NoteType.Hold,
                        HeadPos = headParentPos,
                        EndPos = endParentPos,
                        Rotate = noteRotation,
                        Alpha = 1f, //TODO
                        HeadVisible = noteNode.HeadVisible
                    };

                    noteRenderDatas.Add(noteRenderData);

                }
                else // Tap Flick Drag
                {
                    Vector2 notePos = noteNode.Position; // 坐标系：谱面坐标，相对于判定线

                    //计算note的全局坐标 坐标系：谱面坐标
                    Vector2 globalPos = PosUtil.GetChildGlobalPosition(
                        linePos,
                        notePos,
                        judgeLineNode.CurrentRotate
                    );

                    //谱面坐标转换为Parent坐标
                    Vector2 noteParentPos = PosUtil.ChartPosToViewportPos(
                        globalPos,
                        Parent.Size
                    );

                    //计算旋转
                    float noteRotation = judgeLineNode.CurrentRotate;

                    NoteRenderData noteRenderData = new()
                    {
                        Type = (NoteType)noteNode.data.Type,
                        HeadPos = noteParentPos,
                        EndPos = noteParentPos,
                        Rotate = noteRotation,
                        Alpha = 1f //TODO
                    };

                    noteRenderDatas.Add(noteRenderData);
                }
            }

        }
    }

    public override void UpdateLogic()
    {
        if (Chart == null) return;

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

        // 更新每条判定线及其上的音符
        //SetJudgeLineList();
        foreach (JudgeLineNode judgeLineNode in judgeLineNodes)
        {
            judgeLineNode.UpdateLine(ChartTime);
        }

        //生成渲染数据，供Render方法使用
        CreateRenderDatas();
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
    public void UpdateLine(double gameTime)
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
        //TODO

        //处理父判定线  father为-1代表没有父线
        if(Data.Father >= 0)
        {
            JudgeLineNode father = judgeLineNodes[Data.Father];
            //先更新父线位置
            father.UpdateLine(gameTime);
            //再将自己的坐标加上父线的坐标
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
        
        // 更新该线上所有音符（音符位置受判定线速度和位置影响）
        nowDisplacement = ChartDataHelper.GetDisplacementAtTime(
            Data.EventLayers[0].SpeedEvents, 
            (float)gameTime
        ); // 提前计算累计位移，供note使用（简化计算） // 坐标系: 谱面坐标

        foreach (var note in noteNodes)
        {
            note.UpdateNote(gameTime, this);
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
    private int _index;

    private bool _hasPlayedHitSound = false;//用于标记是否已播放过音效

    /// <summary>在铺面坐标系下的本地坐标</summary>
    protected Vector2 localChartPos = new Vector2(); 

    public bool HeadVisible { get; set; }
    public bool Visible { get; set; }
    public Vector2 Position { get; set; } //坐标系: 谱面坐标[-675,675] [-450,450]

    /// <summary>
    /// 当note落到判定线上时触发，参数是点击的位置(坐标系：parent坐标)
    /// </summary>
    public Action<Vector2> onNoteHited; 
    

    public void SetData(Note data, JudgeLineNode line, ChartPlayer player, int index)
    {
        this.data = data;
        _chartPlayer = player;
        _index = index;

        
    }
    
    /// <summary>
    /// 更新音符位置（受判定线位置和速度影响）
    /// 可被HoldNoteNode重写
    /// </summary>
    public virtual void UpdateNote(double gameTime, JudgeLineNode fatherLine)
    {
        if (data == null) return;

        float noteStartSec = data.startSec;
        float noteEndSec = data.EndTime != null ? data.endSec : noteStartSec;

        // 音符到达判定线时播放音效，并生成打击特效
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
                        new Vector2(fatherLine.CurrentMoveX, fatherLine.CurrentMoveY),
                        calculatedLocalChartPos,
                        fatherLine.CurrentRotate
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
        //处理显示和隐藏
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

        //计算note位置     坐标系: 谱面坐标[-675,675] [-450,450]
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
            float nowDisplacement = fatherLine.nowDisplacement;

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

    // 初始化身体和尾部纹理，创建对应的 Sprite 节点
    // public void InitializeHold(Texture2D bodyTexture, Texture2D endTexture)
    // {
    //     _bodyTexture = bodyTexture;
    //     _endTexture = endTexture;

    //     // 创建身体 Sprite
    //     _bodySprite = new Sprite2D
    //     {
    //         Texture = _bodyTexture,
    //         Scale = new Vector2(_chartPlayer.noteWidthScale, _chartPlayer.noteWidthScale) // 与头部缩放一致
    //     };
    //     AddChild(_bodySprite);

    //     // 创建尾部 Sprite
    //     _endSprite = new Sprite2D
    //     {
    //         Texture = _endTexture,
    //         Scale = new Vector2(_chartPlayer.noteWidthScale, _chartPlayer.noteWidthScale)
    //     };
    //     AddChild(_endSprite);

    //     //holdHead和holdEnd的贴图需要设置offset
    //     _sprite.Offset = new Vector2(0, _chartPlayer.holdHeadTexture.GetHeight() / 2f);//head
    //     _endSprite.Offset = new Vector2(0, -_chartPlayer.holdHeadTexture.GetHeight() / 2f);//end

    //     //hold需要显示在其他音符的下面
    //     ZIndex = 0;
    // }

    public override void UpdateNote(double gameTime, JudgeLineNode fatherLine)
    {
        // 先调用基类更新头部位置和可见性
        base.UpdateNote(gameTime, fatherLine);

        //计算下落速度，由判定线速度和note速度共同决定
        //RPE中每个速度单位表示每秒下降120像素
        float speed = fatherLine.CurrentSpeed * data.Speed * 120; // 坐标系: 谱面坐标
        float startSec = _chartPlayer.BeatToSeconds(data.StartTime);
        float endSec = _chartPlayer.BeatToSeconds(data.EndTime);

        //计算end位置，可以视为在endTime的音符
        //float holdLength = 0;
        {
            //第一阶段：head到达之前，localPosition保持变不变
            // if(gameTime <= startSec)
            // {
            //     float startSpeed = 120f*ChartDataHelper.GetSpeedAtTime(
            //         fatherLine.Data.EventLayers[0].SpeedEvents, startSec); // head落在判定线上时的速度 坐标系: 谱面坐标
            //     float s = (float)(startSpeed * (endSec - startSec)); // 坐标系: 谱面坐标
            //     endLocalChartPos = new Vector2(0,s);
            //     //EndPosition = PosUtil.ChartPosToLocalPos(endLocalChartPos, _chartPlayer.Parent.Size);
            //     holdLength = PosUtil.ChartPosToLocalPos(endLocalChartPos, _chartPlayer.Parent.Size).Y;
                
            // }
            //第二阶段：hold正在缩小，localPosition不断减小至y=0
            //else if(gameTime > startSec && gameTime < endSec)
            // if(gameTime < endSec)
            {
                float localChartY;
                //全部位移 坐标系: 谱面坐标
                //float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, endSec);
                float allDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine.Data.EventLayers[0].SpeedEvents, endSec);

                //note已经移动的位移 坐标系: 谱面坐标
                //float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
                float nowDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine.Data.EventLayers[0].SpeedEvents, (float)gameTime);


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

public class JudgeLineRenderData
{
    // 当前帧的事件插值结果
    public Vector2 Pos { get; set; }
    public float Rotate { get; set; } = 0;
    public float Alpha { get; set; } = 1;
}

public class NoteRenderData
{
    public Vector2 HeadPos { get; set; }
    public NoteType Type { get; set; }
    public float Rotate { get; set; }
    public float Alpha { get; set; }

    //仅限Hold的属性
    public Vector2 EndPos { get; set; }
    public bool HeadVisible { get; set; }
}
