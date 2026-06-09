using Godot;
using QuickType;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;


public enum NoteType
{
    Tap=1,
    Drag=4,
    Flick=3,
    Hold=2
}
public partial class ChartPlayer : Control
{
    public double time = 0;                // 当前游戏时间（秒），由音乐播放控制
    public double chartTime = 0;           // 当前谱面时间，应用了偏移
    public double externalTime = 0;         //由外部设置的游戏时间（秒）
    public bool isPlaying;                //是否正在播放，由上级设置
    public Chart chart;                    // 加载的谱面数据，由上级设置
    public Image bgImage;                //背景图片，由上级设置
    public AudioStream audioStream;       //音乐，由上级设置

    public List<JudgeLineNode> judgeLines = new(); // 动态创建的判定线节点

    #region 纹理贴图
    [ExportGroup("纹理贴图")]
    [Export] public Texture2D tapTexture;
    [Export] public Texture2D dragTexture;
    [Export] public Texture2D flickTexture;
    [Export] public Texture2D holdHeadTexture;
    [Export] public Texture2D holdBodyTexture;
    [Export] public Texture2D holdEndTexture;
    [Export] public Texture2D lineTexture;

    #endregion

    #region 打击音效
    [ExportGroup("打击音效")]
    [Export] public AudioStream tapSound;
    [Export] public AudioStream dragSound;
    [Export] public AudioStream flickSound;

    [ExportGroup("")]
    #endregion

    [Export] private SpriteFrames hitFrames; // 打击特效

    public AudioStreamPlayer audioStreamPlayer;
    private int chartOffset;  // 谱面偏移（以毫秒计量）

    private string extractPath = "user://ChartImport"; // 谱面文件解压目录

    // 对象池
    private HitEffectPool hitEffectPool;

    private Label fpsLabel;

    //将Beat（int[]）转换为秒
    public float BeatToSeconds(int[] beat)
    {
        return TimeUtil.BeatToSecond(beat, chart?.BpmList);
    }

    // private void InitHitEffectPool()
    // {
    //     for (int i = 0; i < poolInitSize; i++)
    //     {
    //         var fx = CreateNewHitEffectInstance();
    //         fx.Visible = false;          // 初始不可见，并且从场景树移除
    //         hitEffectPool.Add(fx);
    //     }
    // }

    // // 创建一个全新的特效实例（只做创建和基础设置）
    // private AnimatedSprite2D CreateNewHitEffectInstance()
    // {
    //     var fx = new AnimatedSprite2D
    //     {
    //         SpriteFrames = hitFrames,
    //         Modulate = new Color
    //         {
    //             R8 = 237,
    //             G8 = 236,
    //             B8 = 176,
    //             A8 = 255
    //         },
    //         ZIndex = 3
    //     };
    //     // 连接信号：播放完后自动回收
    //     fx.AnimationFinished += () => OnHitEffectFinished(fx);
    //     return fx;
    // }

    // private AnimatedSprite2D GetHitEffectFromPool()
    // {
    //     AnimatedSprite2D fx;
    //     if (hitEffectPool.Count > 0)
    //     {
    //         // 取最后一个（O(1)）
    //         fx = hitEffectPool[hitEffectPool.Count - 1];
    //         hitEffectPool.RemoveAt(hitEffectPool.Count - 1);
    //     }
    //     else
    //     {
    //         // 池空时动态扩容
    //         fx = CreateNewHitEffectInstance();
    //     }
    //     return fx;
    // }

    // private void OnHitEffectFinished(AnimatedSprite2D fx)
    // {
    //     // 从场景树移除（如果还在）
    //     if (fx.GetParent() != null)
    //         RemoveChild(fx);
        
    //     // 重置状态
    //     fx.Visible = false;
    //     fx.Position = Vector2.Zero;
    //     fx.Stop();            // 停止动画播放
    //     fx.Frame = 0;         // 重置到第一帧（视情况需要）
        
    //     // 放回池中
    //     hitEffectPool.Add(fx);
    // }

    /// <summary>
    /// 在指定位置创建一个打击特效
    /// </summary>
    public void CreateHitEffect(Vector2 position)
    {
        // // 创建 AnimatedSprite2D 节点
        // var animatedSprite = new AnimatedSprite2D
        // {
        //     SpriteFrames = hitFrames,
        //     Position = position,
        //     Modulate = new Color
        //     {
        //         R8 = 237,
        //         G8 = 236,
        //         B8 = 176,
        //         A8 = 255
        //     }
        // };

        // // 播放默认动画
        // animatedSprite.Play();
        
        // // 连接动画结束信号，播放完后自动销毁
        // animatedSprite.AnimationFinished += () => animatedSprite.QueueFree();
        
        // // 将特效添加到当前场景（或指定的特效容器节点）
        // AddChild(animatedSprite);

        hitEffectPool.Spawn(position);
    }

    // /// <summary>
    // /// 在指定目录下查找第一个 .json 文件（用于定位谱面文件）
    // /// </summary>
    // private string FindFirstJsonFile(string directory)
    // {
    //     var dir = DirAccess.Open(directory);
    //     if (dir == null) return null;

    //     dir.ListDirBegin();
    //     string fileName = dir.GetNext();
    //     while (!string.IsNullOrEmpty(fileName))
    //     {
    //         if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
    //         {
    //             return Path.Combine(directory, fileName);
    //         }
    //         fileName = dir.GetNext();
    //     }
    //     return null;
    // }

    public override void _Ready()
    {
        fpsLabel = GetNode<Label>("FPSLabel");

        //初始化对象池
        // 初始化特效池
        hitEffectPool = new HitEffectPool(this, hitFrames, initSize: 50);
        AudioPool.Initialize(this);
    }

    public void Initialize()
    {
        // 1.加载背景图片
        TextureRect bgNode = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(bgImage),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            //AnchorLeft = -0.5f, AnchorRight = 0.5f, AnchorTop = -0.5f, AnchorBottom = 0.5f,
            Modulate = new Color(0.3f, 0.3f, 0.3f, 1f),
            ZIndex = -999
        };
        bgNode.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bgNode);

        // 2. 加载音乐
        audioStreamPlayer = new AudioStreamPlayer();
        audioStreamPlayer.Stream = audioStream;
        if (audioStreamPlayer.Stream == null)
        {
            GD.PrintErr($"[{this.Name}] 音乐文件加载失败");
            return;
        }

        AddChild(audioStreamPlayer);
        //设置音乐偏移
        chartOffset = (int)chart.Meta.Offset;

        // 创建所有判定线节点
        CreateJudgeLines();

        //预计算所有事件时间的秒数
        ChartDataHelper.RefreshEventSec(chart);
        //预计算所有note时间的秒数
        ChartDataHelper.RefreshNoteSec(chart);
        //预计算所有速度事件的前缀和
        ChartDataHelper.RefreshAllEventPrefix(chart);
        
        //播放音乐
        //audioStreamPlayer.Play(musicStartPosition);
        //GD.Print($"audioStreamPlayer Play({musicStartPosition})");
    }


    private void CreateJudgeLines()
    {
        if (chart?.JudgeLineList == null) return;

        foreach (JudgeLine lineData in chart.JudgeLineList)
        {
            // 为每条判定线创建一个节点（你可以将JudgeLineNode做成一个独立的场景，这里简单用Node2D）
            var lineNode = new JudgeLineNode();
            int index = Array.IndexOf(chart.JudgeLineList, lineData);
            lineNode.Name = $"JudgeLine_{index}";
            // 传入数据及对ChartPlayer的引用（用于时间转换等）、贴图、索引
            lineNode.SetData(lineData, this, lineTexture, index); 
            AddChild(lineNode);
            judgeLines.Add(lineNode);
        }
    }

    public override void _Process(double delta)
    {
        if (chart == null) return;

        if (isPlaying)
        {
            // 获取音乐当前播放位置（秒）
            double musicTime = audioStreamPlayer.GetPlaybackPosition();
            // 应用偏移：谱面逻辑时间 = 音乐时间 - 偏移（偏移为正表示音乐滞后）
            chartTime = musicTime - chartOffset / 1000.0;

            time = musicTime;
        }
        else
        {
            chartTime = externalTime;
        }
        

        // 更新每条判定线及其上的音符
        foreach (var line in judgeLines)
        {
            line.UpdateLine(chartTime);
        }

        //在屏幕上显示帧率
        fpsLabel.Text = $"FPS:{Performance.GetMonitor(Performance.Monitor.TimeFps)}";

        // 可选的：谱面播放完毕检测
        // if (time >= chart.Meta?.Duration)
        // {
        //     GD.Print("谱面播放结束");
        //     // 可以停止处理或循环等
        // }
    }

    // 辅助方法：从谱面根获取BPMList（供JudgeLineNode使用）
    public BpmEvent[] GetBpmList() => chart?.BpmList;
}


/// <summary>
/// 代表一条判定线的节点
/// </summary>
public partial class JudgeLineNode : Node2D
{
    public JudgeLine _data;                 // 原始数据
    private ChartPlayer _chartPlayer;         // 用于获取BPM等
    private Texture2D _texture;               //贴图
    private List<NoteNode> _noteNodes = new(); // 该线上的音符节点
    public int _index;                       //索引
    Sprite2D spriteNode;                     //sprite2D节点，在SetData函数中创建

    
    // 当前帧的事件插值结果
    public float _currentMoveX = 0;
    public float _currentMoveY = 0;
    public float _currentRotate = 0;
    public float _currentAlpha = 1;
    public float _currentSpeed = 1; // 速度系数

    public void SetData(JudgeLine data, ChartPlayer player, Texture2D texture, int index)
    {
        _data = data;
        _chartPlayer = player;
        _texture = texture;
        _index = index;
        

        // 创建该线上的所有音符节点
        if (_data.Notes != null)
        {
            for (int i = 0; i < _data.Notes.Length; i++)
            {
                Note noteData = _data.Notes[i];

                NoteNode noteNode;
                //选择贴图和音效
                Texture2D noteTexture;
                AudioStream noteSound;
                switch (noteData.Type)
                {
                    case 1:noteTexture = _chartPlayer.tapTexture;noteSound = _chartPlayer.tapSound;break;
                    case 2:noteTexture = _chartPlayer.holdHeadTexture;noteSound = _chartPlayer.tapSound;break;
                    case 3:noteTexture = _chartPlayer.flickTexture;noteSound = _chartPlayer.flickSound;break;
                    case 4:noteTexture = _chartPlayer.dragTexture;noteSound = _chartPlayer.dragSound;break;
                    default:noteTexture = _chartPlayer.tapTexture;noteSound = _chartPlayer.tapSound;break;
                }
                //noteNode.SetData(noteData, this, _chartPlayer, noteTexture, noteSound, i);
                // 根据类型创建具体的音符节点
                if (noteData.Type == 2) // Hold
                {
                    var holdNode = new HoldNoteNode();
                    holdNode.SetData(noteData, this, _chartPlayer, noteTexture, noteSound, i);
                    holdNode.InitializeHold(_chartPlayer.holdBodyTexture, _chartPlayer.holdEndTexture);
                    noteNode = holdNode;
                }
                else
                {
                    noteNode = new NoteNode();
                    noteNode.SetData(noteData, this, _chartPlayer, noteTexture, noteSound, i);
                }

                AddChild(noteNode);
                _noteNodes.Add(noteNode);
            }
        }

        //添加sprite2D节点，用于渲染
        spriteNode = new Sprite2D
        {
            Name = "Sprite2D",
            Texture = texture,
            //颜色和透明度
            Modulate = new Color{
                R8 = 237,
                G8 = 236,
                B8 = 176,
                A8 = Mathf.RoundToInt(_currentAlpha)
            },
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(spriteNode);

        //添加label节点，用于显示判定线编号
        Label labelNode = new Label();
        labelNode.Text = $"{index}";
        labelNode.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        labelNode.HorizontalAlignment = HorizontalAlignment.Center;
        labelNode.AddThemeFontSizeOverride("font_size", 24);
        //labelNode.Position = new Vector2(0,-30);
        AddChild(labelNode);
    }


    /// <summary>
    /// 根据当前游戏时间更新判定线状态
    /// </summary>
    public void UpdateLine(double gameTime)
    {
        if (_data?.EventLayers == null || _data.EventLayers.Length == 0) return;

        // 我们需要综合所有事件层（通常只有一层，但可能有多个）
        // 这里简化：取第一个事件层
        //TODO
        EventLayer layer = _data.EventLayers[0];

        // 对每种事件类型进行插值
        _currentMoveX = InterpolateEvent(layer.MoveXEvents, gameTime, 0);
        _currentMoveY = InterpolateEvent(layer.MoveYEvents, gameTime, 0);
        _currentRotate = InterpolateEvent(layer.RotateEvents, gameTime, 0);
        _currentAlpha = InterpolateEvent(layer.AlphaEvents, gameTime, 255);
        _currentSpeed = InterpolateEvent(layer.SpeedEvents, gameTime, 10);

        //处理父判定线  father为-1代表没有父线
        if(_data.Father != -1)
        {
            JudgeLineNode father = _chartPlayer.judgeLines[_data.Father];
            //先更新父线位置
            father.UpdateLine(gameTime);
            // //在将自己的坐标加上父线的坐标
            // _currentMoveX += father._currentMoveX;
            // _currentMoveY += father._currentMoveY;
            //这里不能直接将自己的坐标加上父线的坐标，因为父线的旋转会导致子线的位置变化
            Vector2 currentPos = PosUtil.GetChildGlobalPosition(new Vector2(father._currentMoveX, father._currentMoveY),
                new Vector2(_currentMoveX, _currentMoveY),
                father._currentRotate);
            
            // //输出日志
            // if(Name == "JudgeLine_1" && father._currentRotate > 0)
            // {
            //     GD.Print($"fatherPos:{new Vector2(father._currentMoveX, father._currentMoveY)},localPos:{new Vector2(_currentMoveX, _currentMoveY)}, father._currentRotate:{father._currentRotate}, currentPos:{currentPos}");
            // }

            _currentMoveX = currentPos.X;
            _currentMoveY = currentPos.Y;

            

        }


        // 应用变换
        Position = PosUtil.ChartPosToViewportPos(new Vector2(_currentMoveX, _currentMoveY), _chartPlayer.Size);
        Rotation = Mathf.DegToRad(_currentRotate); // 事件值是角度

        //调整颜色和透明度
        spriteNode.Modulate = new Color{
            R8 = 237,
            G8 = 236,
            B8 = 176,
            A8 = Mathf.RoundToInt(_currentAlpha)
        };
        
        // 更新该线上所有音符（音符位置受判定线速度和位置影响）
        foreach (var note in _noteNodes)
        {
            note.UpdateNote(gameTime, this);
        }
    }

    

    /// <summary>
    /// 通用事件插值（用于float类型的事件，如moveX, alpha等）
    /// </summary>
    /// <param name="events"></param>
    /// <param name="time">游戏运行时间</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns></returns>
    private float InterpolateEvent(LineEvent[] events, double time, float defaultValue)
    {
        if (events == null || events.Length == 0) return defaultValue;

        // 找到当前时间所在的事件段
        for (int i = 0; i < events.Length; i++)
        {
            LineEvent ev = events[i];
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
            else if (time < startSec)
            {
                // if (i == 0)
                // {
                //     //GD.PrintErr($"[{this.Name}] InterpolateEvent i==0 \n startSec:{startSec}, endSec:{endSec}, time:{time}");
                //     return 0;
                // }
                // 在当前事件之前，返回上一个事件的结束值
                if(i == 0) return 0f;
                else return (float)events[i-1].End;
                
            }
        }

        // 在所有事件之后，返回最后一个事件的结束值
        var lastEv = events[events.Length - 1];
        return (float)lastEv.End;
    }

//     // 速度事件插值（SpeedEvent结构略有不同）
//     private float InterpolateEventSpeed(SpeedEvent[] events, double time, float defaultValue)
//     {
//         if (events == null || events.Length == 0) return defaultValue;

//         float targetSec = (float)time;
//         for (int i = 0; i < events.Length; i++)
//         {
//             var ev = events[i];
//             float startSec = ev.startSec;
//             float endSec = ev.endSec;

//             if (targetSec >= startSec && targetSec <= endSec)
//             {
//                 // 速度事件这里按线性处理
//                 float t = (targetSec - startSec) / (endSec - startSec);
//                 return (float)(ev.Start + (ev.End - ev.Start) * t);
//             }
//             else if (targetSec < startSec)
//             {
//                 return (float)ev.Start;
//             }
//         }
//         return (float)events[events.Length - 1].End;
//     }

    
}

/// <summary>
/// 代表一个音符的节点
/// </summary>
public partial class NoteNode : Node2D
{
    protected Note _data;
    private JudgeLineNode _parentLine;
    protected ChartPlayer _chartPlayer;
    private Texture2D _texture;
    private AudioStream _sound;
    private int _index;
    //private AudioStreamPlayer audioStreamPlayer; // 在SetData方法中新建

    protected Sprite2D _sprite; // 在SetData方法中新建

    private bool _hasPlayedHitSound = false;//用于标记是否已播放过音效

    protected Vector2 localChartPos = new Vector2(); // 在铺面坐标系下的本地坐标
    

    public void SetData(Note data, JudgeLineNode line, ChartPlayer player, Texture2D texture, AudioStream sound, int index)
    {
        _data = data;
        _parentLine = line;
        _chartPlayer = player;
        _texture = texture;
        _sound = sound;
        _index = index;

        //设置节点名称，方便调试
        Name = $"Note {_parentLine._index}_{_index}";

        // 添加sprite2D节点，贴图
        _sprite = new Sprite2D
        {
            Texture = _texture,
            Scale = new Vector2(0.161f, 0.161f)
        };
        //holdHead和holdEnd的贴图需要设置offset，但不再这里设置，在HoldNoteNode类中设置
        AddChild(_sprite);

        //hold需要显示在其他音符的下面
        ZIndex = 1;

        //添加AudioStreamPlayer节点，用于播放音效
        //audioStreamPlayer = new AudioStreamPlayer();
        //audioStreamPlayer.Stream = sound;
        //AddChild(audioStreamPlayer);
    }

    

    
    private void PlayHitSound()
    {
        var player = AudioPool.Get();
        player.Stream = _sound;
        player.Play(); // 播放完成后自动回收（通过 Finished 信号）
    }
    
    /// <summary>
    /// 更新音符位置（受判定线位置和速度影响）
    /// 可被HoldNoteNode重写
    /// </summary>
    public virtual void UpdateNote(double gameTime, JudgeLineNode fatherLine)
    {
        if (_data == null) return;

        float noteStartSec = _data.startSec;
        float noteEndSec = _data.EndTime != null ? _data.endSec : noteStartSec;

        // _data.VisibleTime 音符可视时间（打击前多少秒开始显现，默认99999.0）

        //处理显示和隐藏
        {
            if(_data.Type == 2) // hold需要特殊处理，当head到达判定线时，隐藏head的贴图
            {
                if(gameTime >= noteStartSec)
                {
                    _sprite.Visible = false;
                }
                else
                {
                    _sprite.Visible = true;
                }
            }
            float appearSec = noteStartSec - _data.VisibleTime; // 出现时刻
            float disappearSec = noteEndSec; // 消失时刻（如果是长按，Hold尾部）
            if (gameTime < appearSec || gameTime > disappearSec)
            {
                // 不在显示区间内，隐藏
                Visible = false;
            }
            else
            {
                // 在显示区间内，显示
                Visible = true;
            }
        }

        // 音符到达判定线时播放音效，并生成打击特效
        if(_data.IsFake == false) // 假note不需要击打
        {
            float hitTime = noteStartSec; // 头部到达判定线的时间
            if (gameTime >= hitTime && !_hasPlayedHitSound)
            {
                if (_chartPlayer.isPlaying) // 只有播放状态下显示特效，编辑器滚动时不显示
                {
                    // 播放音效并生成打击特效
                    // if (audioStreamPlayer == null || audioStreamPlayer.Stream == null)
                    // {
                    //     GD.PrintErr($"[{this.Name}] 无法播放打击音效");
                    // }
                    // audioStreamPlayer.Play();

                    PlayHitSound();

                    //显示打击特效
                    //理论上此时note应该在的位置，防止note速度过快导致的误差
                    Vector2 calculatedLocalChartPos = new Vector2(_data.PositionX, 0);
                    Vector2 globalChartPos = PosUtil.GetChildGlobalPosition(
                        new Vector2(fatherLine._currentMoveX, fatherLine._currentMoveY),
                        calculatedLocalChartPos,
                        fatherLine._currentRotate
                    );
                    Vector2 hitViewportPos = PosUtil.ChartPosToViewportPos(globalChartPos, _chartPlayer.Size);
                    _chartPlayer.CreateHitEffect(hitViewportPos);
                }
                
                _hasPlayedHitSound = true;
            }
            else if (gameTime < hitTime)
            {
                // 时间回退到击中点之前，重置标记，允许再次触发
                _hasPlayedHitSound = false;
            }
        }

        //计算note位置
        //相对于判定线的Y坐标 = 速度随时间变化的函数的积分
        //简单起见，这里分段计算位移，用到匀变速直线运动的公式
        //下落速度由判定线速度和note速度相乘共同决定
        //RPE中每个速度单位表示每秒下降120像素
        {
            float localChartX, localChartY;
            localChartX = _data.PositionX; 

            //全部位移
            // float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, noteStartSec);
            float allDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine._data.EventLayers[0].SpeedEvents, noteStartSec);

            //note已经移动的位移
            // float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
            float nowDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);

            localChartY = Math.Max(0, allDisplacement - nowDisplacement);

            //音符翻转 1表示上面，2表示下面
            if(_data.Above == 2)
            {
                localChartY = -localChartY;
            }

            localChartPos = new Vector2(localChartX,localChartY);

            //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
            //Vector2 viewportPos = Util.ChartPosToViewportPos(localChartPos, _chartPlayer.Size);

            //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为相对于判定线的坐标系
            Vector2 viewportPos = PosUtil.ChartPosToLocalPos(localChartPos, _chartPlayer.Size);
            
            //设定位置
            this.Position = viewportPos;

        }

        //输出日志
        // if(Name == "Note 0_0")
        // {
        //     GD.Print($"[{Name}] leftTime:{leftTime} localChartY:{localChartY} localChartX:{localChartX} viewportPos:{viewportPos}");
        // }
        
        
    }
}

/// <summary>
/// 代表一个Hold音符的节点
/// </summary>
public partial class HoldNoteNode : NoteNode
{
    private Texture2D _bodyTexture;
    private Texture2D _endTexture;
    private Sprite2D _bodySprite;
    private Sprite2D _endSprite;

    private Vector2 endLocalChartPos; //在铺面坐标系下end的本地坐标

    // 初始化身体和尾部纹理，创建对应的 Sprite 节点
    public void InitializeHold(Texture2D bodyTexture, Texture2D endTexture)
    {
        _bodyTexture = bodyTexture;
        _endTexture = endTexture;

        // 创建身体 Sprite
        _bodySprite = new Sprite2D
        {
            Texture = _bodyTexture,
            Scale = new Vector2(0.161f, 0.161f) // 与头部缩放一致
        };
        AddChild(_bodySprite);

        // 创建尾部 Sprite
        _endSprite = new Sprite2D
        {
            Texture = _endTexture,
            Scale = new Vector2(0.161f, 0.161f)
        };
        AddChild(_endSprite);

        //holdHead和holdEnd的贴图需要设置offset
        _sprite.Offset = new Vector2(0, _chartPlayer.holdHeadTexture.GetHeight() / 2f);//head
        _endSprite.Offset = new Vector2(0, -_chartPlayer.holdHeadTexture.GetHeight() / 2f);//end

        //hold需要显示在其他音符的下面
        ZIndex = 0;
    }

    public override void UpdateNote(double gameTime, JudgeLineNode fatherLine)
    {
        // 先调用基类更新头部位置和可见性
        base.UpdateNote(gameTime, fatherLine);

        //计算下落速度，由判定线速度和note速度共同决定
        //RPE中每个速度单位表示每秒下降120像素
        float speed = fatherLine._currentSpeed * _data.Speed * 120;
        float startSec = _chartPlayer.BeatToSeconds(_data.StartTime);
        float endSec = _chartPlayer.BeatToSeconds(_data.EndTime);

        //计算end位置，可以视为在endTime的音符
        {
            //第一阶段：head到达之前，localPosition保持变不变
            if(gameTime <= startSec)
            {
                float startSpeed = 120f*ChartDataHelper.GetSpeedAtTime(fatherLine._data.EventLayers[0].SpeedEvents, startSec); // head落在判定线上时的速度
                float s = (float)(startSpeed * (endSec - startSec));
                endLocalChartPos = new Vector2(0,s);
                _endSprite.Position = PosUtil.ChartPosToLocalPos(endLocalChartPos, _chartPlayer.Size);
                
            }
            //第二阶段：hold正在缩小，localPosition不断减小至y=0
            else if(gameTime > startSec && gameTime < endSec)
            {
                float localChartY;
                //全部位移
                //float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, endSec);
                float allDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine._data.EventLayers[0].SpeedEvents, endSec);

                //note已经移动的位移
                //float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
                float nowDisplacement = ChartDataHelper.GetDisplacementAtTime(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);


                localChartY = Math.Max(0, allDisplacement - nowDisplacement);

                endLocalChartPos = new Vector2(0,localChartY);

                //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
                Vector2 viewportPos = PosUtil.ChartPosToLocalPos(endLocalChartPos, _chartPlayer.Size);

                //设定位置
                _endSprite.Position = viewportPos;

                
            }
            //第三阶段：hold结束，隐藏自己
            //由于父类设置了隐藏，所以这里不需要进行任何操作

        }

        //计算body位置和大小
        {
            // 计算相对位置:head和end的中间
            Vector2 bodyLocalChartPos = endLocalChartPos / 2;

            //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
            Vector2 viewportPos = PosUtil.ChartPosToLocalPos(bodyLocalChartPos, _chartPlayer.Size);
            
            //设定body位置
            _bodySprite.Position = viewportPos;

            //hold原尺寸为1900，缩放后为sizeY
            float sizeY = _sprite.Position.Y - _endSprite.Position.Y;
            _bodySprite.Scale = new Vector2(0.161f, sizeY/1900f);
        }

        
    }

    
}