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
public partial class ChartPlayer : Node
{
    private double time = 0;                // 当前游戏时间（秒）
    private Chart chart;                    // 加载的谱面数据
    public List<JudgeLineNode> judgeLines = new(); // 动态创建的判定线节点

    #region 纹理贴图
    [Export]
    public Texture2D tapTexture;
    [Export]
    public Texture2D dragTexture;
    [Export]
    public Texture2D flickTexture;
    [Export]
    public Texture2D holdHeadTexture;
    [Export]
    public Texture2D holdBodyTexture;
    [Export]
    public Texture2D holdEndTexture;
    [Export]
    public Texture2D lineTexture;

    #endregion

    #region 打击音效
    [Export]
    public AudioStream tapSound;
    [Export]
    public AudioStream dragSound;
    [Export]
    public AudioStream flickSound;
    #endregion

    private AudioStreamPlayer audioStreamPlayer;
    private int chartOffset;  // 谱面偏移（以毫秒计量）
    [Export]
    private string zipLoadPath; // zip格式谱面文件的路径
    [Export]
    private float musicStartPosition;

    private string extractPath = "user://ChartImport"; // 谱面文件解压目录

    public EffectsManager effectsManager;
    public ZipExtractor zipExtractor;

    private Label fpsLabel;

    // 时间转换：将Beat（int[]）转换为秒
    public float BeatToSeconds(int[] beat)
    {
        if (chart?.BpmList == null || chart.BpmList.Length == 0)
            return 0;

        // 将Beat转为以拍为单位的总拍数： beat[0] + beat[1]/beat[2]
        float totalBeats = beat[0] + (float)beat[1] / beat[2];

        // 找到当前Beat所在的BPM段并累积时间
        float elapsedSeconds = 0;
        float lastBpmBeat = 0; // 上一个BPM事件的总拍数
        float currentBpm = chart.BpmList[0].Bpm; // 默认第一个BPM

        for (int i = 0; i < chart.BpmList.Length; i++)
        {
            var bpmEvent = chart.BpmList[i];
            float eventBeat = bpmEvent.StartTime[0] + (float)bpmEvent.StartTime[1] / bpmEvent.StartTime[2];

            if (totalBeats >= eventBeat)
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
        float remainingBeats = totalBeats - lastBpmBeat;
        elapsedSeconds += remainingBeats * 60f / currentBpm;

        return elapsedSeconds;
    }

    /// <summary>
    /// 在指定目录下查找第一个 .json 文件（用于定位谱面文件）
    /// </summary>
    private string FindFirstJsonFile(string directory)
    {
        var dir = DirAccess.Open(directory);
        if (dir == null) return null;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
            {
                return Path.Combine(directory, fileName);
            }
            fileName = dir.GetNext();
        }
        return null;
    }
    public override void _Ready()
    {
        base._Ready();
        effectsManager = GetNode<EffectsManager>("/root/EffectsManager");
        zipExtractor = GetNode<ZipExtractor>("/root/ZipExtractor");
        fpsLabel = GetNode<Label>("FPSLabel");

        //解压谱面压缩包
        zipExtractor.UnzipFileTo(zipLoadPath, extractPath);

        // 1.加载谱面（使用之前写好的静态方法）
        string chartFilePath = FindFirstJsonFile(extractPath);
        if (chartFilePath == null)
        {
            GD.PrintErr("解压目录中未找到 JSON 谱面文件");
            return;
        }
        chart = ChartLoader.LoadChart(chartFilePath);

        if (chart == null)
        {
            GD.PrintErr("谱面导入失败");
            return;
        }
        GD.Print("谱面导入成功，总时长: ", chart.Meta?.Duration, "秒");

        // 2.加载背景图片
        Image bgImage = Image.LoadFromFile(Path.Combine(extractPath, chart.Meta.Background));
        if (bgImage == null)
        {
            GD.PrintErr("背景图片导入失败");
            return;
        }
        //TODO 图片模糊效果
        Image blurred = bgImage;
        
        //创建TextureRect节点
        TextureRect bgNode = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(blurred),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            AnchorLeft = -0.5f, AnchorRight = 0.5f, AnchorTop = -0.5f, AnchorBottom = 0.5f,
            Modulate = new Color(0.4f, 0.4f, 0.4f, 1f),
            ZIndex = -999
        };
        AddChild(bgNode);

        // 3.加载info.txt

        // 4. 加载音乐
        audioStreamPlayer = new AudioStreamPlayer();
        string songPath = extractPath.TrimEnd('/') + "/" + chart.Meta.Song;

        if (!Godot.FileAccess.FileExists(songPath))
        {
            GD.PrintErr($"音乐文件不存在: {songPath}");
            return;
        }

        // 因为MP3文件时解压时动态生成的，所以需要使用 AudioStreamMP3.LoadFromFile 加载 MP3
        audioStreamPlayer.Stream = AudioStreamMP3.LoadFromFile(songPath);
        if (audioStreamPlayer.Stream == null)
        {
            GD.PrintErr($"音乐文件加载失败: {songPath}");
            return;
        }

        AddChild(audioStreamPlayer);
        GD.Print($"音乐文件加载成功: {songPath}");
        //设置音乐偏移
        chartOffset = (int)chart.Meta.Offset;
        

        // 创建所有判定线节点
        CreateJudgeLines();

        //播放音乐
        audioStreamPlayer.Play(musicStartPosition);
        GD.Print($"audioStreamPlayer Play({musicStartPosition})");

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


        // 获取音乐当前播放位置（秒）
        double musicTime = audioStreamPlayer.GetPlaybackPosition();
        // 应用偏移：谱面逻辑时间 = 音乐时间 - 偏移（偏移为正表示音乐滞后）
        double chartTime = musicTime - chartOffset / 1000.0;

        time = musicTime;

        // 更新每条判定线及其上的音符
        foreach (var line in judgeLines)
        {
            line.UpdateLine(chartTime);
        }

        //在屏幕上显示帧率
        fpsLabel.Text = $"FPS:{Performance.GetMonitor(Performance.Monitor.TimeFps)}";

        // 可选的：谱面播放完毕检测
        if (time >= chart.Meta?.Duration)
        {
            GD.Print("谱面播放结束");
            // 可以停止处理或循环等
        }
    }

    // 辅助方法：从谱面根获取BPMList（供JudgeLineNode使用）
    public BpmEvent[] GetBpmList() => chart?.BpmList;
}

//缓动函数
public enum EasingFunc
{
    Linear,Sine,Quad,Cubic,Quart,Quint,Expo,Circ,Back,Elastic,Bounce
}
public enum EasingIO
{
    In,Out,IO
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
            }
        };
        AddChild(spriteNode);

        //添加label节点，用于显示判定线编号
        Label labelNode = new Label();
        labelNode.Text = $"{index}";
        labelNode.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        labelNode.HorizontalAlignment = HorizontalAlignment.Center;
        labelNode.Position = new Vector2(0,-30);
        AddChild(labelNode);
    }

    /// <summary>
    /// 从父物体局部坐标转换到全局坐标
    /// </summary>
    /// <param name="fatherPos">父物体在世界空间中的位置</param>
    /// <param name="childLocalPos">子物体在父物体局部坐标系中的位置</param>
    /// <param name="fatherRotationDegrees">父物体坐标系的旋转角度，单位为度，正值表示逆时针旋转</param>
    /// <returns></returns>
    public Vector2 GetChildGlobalPosition(Vector2 fatherPos, Vector2 childLocalPos, float fatherRotationDegrees)
    {
        // // 构建父物体的变换矩阵（旋转 + 平移）
        // Transform2D parentTransform = new Transform2D(Mathf.DegToRad(fatherRotationDegrees), fatherPos);

        // // 将局部坐标转换为全局坐标
        // return parentTransform * childLocalPos;

        float rad = Mathf.DegToRad(fatherRotationDegrees);
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        // 顺时针旋转矩阵：
        // [ cosθ  sinθ ]
        // [ -sinθ cosθ ]
        Vector2 rotated = new Vector2(
            childLocalPos.X * cos + childLocalPos.Y * sin,
            -childLocalPos.X * sin + childLocalPos.Y * cos
        );

        // 加上父物体位置
        return fatherPos + rotated;
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
        _currentSpeed = InterpolateEventSpeed(layer.SpeedEvents, gameTime, 10);

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
            Vector2 currentPos = GetChildGlobalPosition(new Vector2(father._currentMoveX, father._currentMoveY),
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
        Position = ChartPosToViewportPos(new Vector2(_currentMoveX, _currentMoveY));
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
    /// 缓动插值函数
    /// </summary>
    /// <param name="t">[0,1]</param>
    /// <param name="func">缓动函数的函数类型</param>
    /// <param name="io">缓动函数的缓急类型</param>
    /// <returns>缓动后的结果</returns>
    public float Interpolate(float t, EasingFunc func, EasingIO io)
    {
        if(io == EasingIO.IO)
        {
            //由In和Out拼接而成
            if(t>=0 && t < 0.5f)
            {
                return 0.5f * Interpolate(2f * t, func, EasingIO.In);
            }
            else if(t>=0.5 && t <= 1)
            {
                return 0.5f + 0.5f * Interpolate(2f*t-1, func, EasingIO.Out);
            }
        }
        else if(io == EasingIO.In)
        {
            switch (func)
            {
                case EasingFunc.Linear : return t;
                case EasingFunc.Sine : return (float)(1 - Mathf.Cos(Math.PI * t / 2f));
                case EasingFunc.Quad : return t*t;
                case EasingFunc.Cubic : return t*t*t;
                case EasingFunc.Quart : return (float)Math.Pow(t,4);
                case EasingFunc.Quint : return (float)Math.Pow(t,5);
                case EasingFunc.Expo:
                    if (t == 0) return 0;
                    return (float)Math.Pow(2, 10 * t - 10);
                case EasingFunc.Circ: return (float)(1 - Math.Sqrt(1 - t * t));
                case EasingFunc.Back: return (float)((2.70158f * t - 1.70158f) * t * t);
                case EasingFunc.Elastic:
                    if (t == 0) return 0;
                    if (t == 1) return 1;
                    const float c4 = 2f * (float)Math.PI / 3f;
                    return (float)(-Math.Pow(2, 10 * t - 10) * Math.Sin((t * 10 - 10.75) * c4));
                case EasingFunc.Bounce:
                    return 1 - Interpolate(1 - t, func, EasingIO.Out); // InBounce = 1 - OutBounce(1-t)
                default: return t;
            }
        }
        else if(io == EasingIO.Out)
        {
            switch (func)
            {
                case EasingFunc.Linear: return t;
                case EasingFunc.Sine: return (float)Math.Sin(Math.PI * t / 2f);
                case EasingFunc.Quad: return 1 - (1 - t) * (1 - t);
                case EasingFunc.Cubic: return 1 - (1 - t) * (1 - t) * (1 - t);
                case EasingFunc.Quart: return 1 - (float)Math.Pow(1 - t, 4);
                case EasingFunc.Quint: return 1 - (float)Math.Pow(1 - t, 5);
                case EasingFunc.Expo:
                    if (Math.Abs(t - 1) < 1e-6) return 1f;
                    return (float)(1 - Math.Pow(2, -10 * t));
                case EasingFunc.Circ: return (float)Math.Sqrt(1 - (t - 1) * (t - 1));
                case EasingFunc.Back:
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1;
                    return 1 + c3 * u * u * u + c1 * u * u;
                case EasingFunc.Elastic:
                    if (t == 0) return 0;
                    if (t == 1) return 1;
                    const float c4_elastic = (2f * (float)Math.PI) / 3f;
                    return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4_elastic) + 1);
                case EasingFunc.Bounce:
                    // 标准 easeOutBounce 分段函数
                    float n1 = 7.5625f;
                    float d1 = 2.75f;
                    if (t < 1f / d1)
                    {
                        return n1 * t * t;
                    }
                    else if (t < 2f / d1)
                    {
                        t -= 1.5f / d1;
                        return n1 * t * t + 0.75f;
                    }
                    else if (t < 2.5f / d1)
                    {
                        t -= 2.25f / d1;
                        return n1 * t * t + 0.9375f;
                    }
                    else
                    {
                        t -= 2.625f / d1;
                        return n1 * t * t + 0.984375f;
                    }
                default: return t;
            }
        }
        return t; // 理论上不会执行到这里
    }

    /// <summary>
    /// 缓动插值函数
    /// </summary>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <returns>缓动后的结果</returns>
    public float Interpolate(float t, int easingType)
    {
        switch (easingType)
        {
            case 0: return 0; // Fixed
            case 1: return Interpolate(t, EasingFunc.Linear, EasingIO.In);   // Linear
            case 2: return Interpolate(t, EasingFunc.Sine, EasingIO.Out);    // easeOutSine
            case 3: return Interpolate(t, EasingFunc.Sine, EasingIO.In);     // easeInSine
            case 4: return Interpolate(t, EasingFunc.Quad, EasingIO.Out);    // easeOutQuad
            case 5: return Interpolate(t, EasingFunc.Quad, EasingIO.In);     // easeInQuad
            case 6: return Interpolate(t, EasingFunc.Sine, EasingIO.IO);     // easeInOutSine
            case 7: return Interpolate(t, EasingFunc.Quad, EasingIO.IO);     // easeInOutQuad
            case 8: return Interpolate(t, EasingFunc.Cubic, EasingIO.Out);   // easeOutCubic
            case 9: return Interpolate(t, EasingFunc.Cubic, EasingIO.In);    // easeInCubic
            case 10: return Interpolate(t, EasingFunc.Quart, EasingIO.Out);  // easeOutQuart
            case 11: return Interpolate(t, EasingFunc.Quart, EasingIO.In);   // easeInQuart
            case 12: return Interpolate(t, EasingFunc.Cubic, EasingIO.IO);   // easeInOutCubic (注意：原文写的是Cubic不是Quart)
            case 13: return Interpolate(t, EasingFunc.Quart, EasingIO.IO);   // easeInOutQuart
            case 14: return Interpolate(t, EasingFunc.Quint, EasingIO.Out);  // easeOutQuint
            case 15: return Interpolate(t, EasingFunc.Quint, EasingIO.In);   // easeInQuint
            case 16: return Interpolate(t, EasingFunc.Expo, EasingIO.Out);   // easeOutExpo
            case 17: return Interpolate(t, EasingFunc.Expo, EasingIO.In);    // easeInExpo
            case 18: return Interpolate(t, EasingFunc.Circ, EasingIO.In);    // easeInCirc (注意：Circ的In/Out编号与其他相反)
            case 19: return Interpolate(t, EasingFunc.Circ, EasingIO.Out);   // easeOutCirc
            case 20: return Interpolate(t, EasingFunc.Back, EasingIO.Out);   // easeOutBack
            case 21: return Interpolate(t, EasingFunc.Back, EasingIO.In);    // easeInBack
            case 22: return Interpolate(t, EasingFunc.Circ, EasingIO.IO);    // easeInOutCirc
            case 23: return Interpolate(t, EasingFunc.Back, EasingIO.IO);    // easeInOutBack
            case 24: return Interpolate(t, EasingFunc.Elastic, EasingIO.Out);// easeOutElastic
            case 25: return Interpolate(t, EasingFunc.Elastic, EasingIO.In); // easeInElastic
            case 26: return Interpolate(t, EasingFunc.Bounce, EasingIO.Out); // easeOutBounce
            case 27: return Interpolate(t, EasingFunc.Bounce, EasingIO.In);  // easeInBounce
            case 28: return Interpolate(t, EasingFunc.Bounce, EasingIO.IO);  // easeInOutBounce
            case 29: return Interpolate(t, EasingFunc.Elastic, EasingIO.IO); // easeInOutElastic
            default: return Interpolate(t, EasingFunc.Linear, EasingIO.In);
        }
    }

    /// <summary>
    /// 带有实际值的插值
    /// </summary>
    /// <param name="x1"></param>
    /// <param name="x2"></param>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <returns></returns>
    public float InterpolateValue(float x1, float x2, float t, int easingType)
    {
        return x1 + (x2-x1) * Interpolate(t,easingType);
    }

    /// <summary>
    /// 经过裁剪的缓动插值
    /// </summary>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <param name="left">左切割，[0,1]</param>
    /// <param name="right">右切割，[0,1]</param>
    /// <returns></returns>
    public float CutInterpolate(float t, int easingType, float left, float right)
    {
        float leftX = InterpolateValue(0,1,left,easingType);
        float rightX = InterpolateValue(0,1,right,easingType);
        float T = left + (right - left) * t;
        float TX = InterpolateValue(0,1,T,easingType);

        float result = (TX - leftX) / (rightX - leftX);
        return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="x1">初始值</param>
    /// <param name="x2">末尾值</param>
    /// <param name="t">[0,1]</param>
    /// <param name="easingType">RPE中的缓动类型，为0~29整数</param>
    /// <param name="left">左切割，[0,1]</param>
    /// <param name="right">右切割，[0,1]</param>
    /// <returns></returns>
    public float CutInterpolateValue(float x1, float x2, float t, int easingType, float left, float right)
    {
        return x1 + (x2-x1) * CutInterpolate(t, easingType, left, right);
    }

    // 坐标映射
    public Vector2 ChartPosToViewportPos(Vector2 pos)
    {
        //获取屏幕大小
        // Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        // //[-675,675] -> [0,X]
        // float newX = (viewportSize.X/1350f) * pos.X + (viewportSize.X/2f);
        // //[-450,450] -> [Y,0]
        // float newY = (-viewportSize.Y / 900f) * pos.Y + (viewportSize.Y/2f);

        float newX = pos.X;
        float newY = -pos.Y;

        return new Vector2(newX,newY);
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
            float startSec = _chartPlayer.BeatToSeconds(ev.StartTime);
            float endSec = _chartPlayer.BeatToSeconds(ev.EndTime);

            if (time >= startSec && time <= endSec)
            {
                // 插值，需要考虑事件切割
                float t = (float)((time - startSec) / (endSec - startSec));
                float leftCut = ev.EasingLeft;
                float rightCut = ev.EasingRight;
                return CutInterpolateValue(ev.Start, ev.End, t, ev.EasingType, leftCut, rightCut);
                
            }
            else if (time < startSec)
            {
                // 在当前事件之前，返回上一个事件的结束值
                return (float)events[i-1].End;
            }
        }

        // 在所有事件之后，返回最后一个事件的结束值
        var lastEv = events[events.Length - 1];
        return (float)lastEv.End;
    }

    // 速度事件插值（SpeedEvent结构略有不同）
    private float InterpolateEventSpeed(SpeedEvent[] events, double time, float defaultValue)
    {
        if (events == null || events.Length == 0) return defaultValue;

        float targetSec = (float)time;
        for (int i = 0; i < events.Length; i++)
        {
            var ev = events[i];
            float startSec = _chartPlayer.BeatToSeconds(ev.StartTime);
            float endSec = _chartPlayer.BeatToSeconds(ev.EndTime);

            if (targetSec >= startSec && targetSec <= endSec)
            {
                // 速度事件这里按线性处理
                float t = (targetSec - startSec) / (endSec - startSec);
                return (float)(ev.Start + (ev.End - ev.Start) * t);
            }
            else if (targetSec < startSec)
            {
                return (float)ev.Start;
            }
        }
        return (float)events[events.Length - 1].End;
    }
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
    private AudioStreamPlayer audioStreamPlayer; // 在SetData方法中新建

    protected Sprite2D _sprite; // 在SetData方法中新建

    private bool _hasPlayedHitSound = false;//用于标记是否已播放过音效

    protected Vector2 localChartPos = new Vector2(); // 在铺面坐标系下的本地坐标


    // 辅助方法：坐标映射
    public Vector2 ChartPosToViewportPos(Vector2 pos)
    {
        //获取屏幕大小
        // Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        // //[-675,675] -> [0,X]
        // float newX = (viewportSize.X/1350f) * pos.X + (viewportSize.X/2f);
        // //[-450,450] -> [Y,0]
        // float newY = (-viewportSize.Y / 900f) * pos.Y + (viewportSize.Y/2f);

        float newX = pos.X;
        float newY = -pos.Y;

        return new Vector2(newX,newY);
    }
    

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

        //添加AudioStreamPlayer节点，用于播放音效
        audioStreamPlayer = new AudioStreamPlayer();
        audioStreamPlayer.Stream = sound;
        AddChild(audioStreamPlayer);
    }

    /// <summary>
    /// 根据速度事件，计算note的位移
    /// </summary>
    /// <param name="events">速度事件</param>
    /// <param name="time">游戏时间</param>
    /// <returns></returns>
    protected float IntegralSpeedEvent(SpeedEvent[] events, float time)
    {
        float totalX = 0; // Y轴上的总位移
        //遍历所有速度事件
        for (int i = 0; i < events.Length; i++)
        {
            SpeedEvent ev = events[i];

            float start = ev.Start;
            float end = ev.End;
            float startSec = _chartPlayer.BeatToSeconds(ev.StartTime);
            float endSec = _chartPlayer.BeatToSeconds(ev.EndTime);

            // 如果time已经在这个事件之后
            if(time > endSec)
            {
                totalX += 120f * (start + end) * (endSec - startSec) / 2f;

            }
            // 如果time正在这个事件中
            else if(time >= startSec && time <= endSec)
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
                float nextStartSec = _chartPlayer.BeatToSeconds(events[i+1].StartTime);
                //如果time正在这个间隔中
                if(time >= endSec && time <= nextStartSec)
                {
                    totalX += 120f * (float)(end * (time - endSec));
                    break;
                }
                //如果time在这个间隔之后
                else if(time > nextStartSec)
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

    /// <summary>
    /// 获取某一时刻的判定线速度（是谱面文件中写的数值，每个单位代表120px/s）
    /// </summary>
    /// <param name="events">速度事件</param>
    /// <param name="time">游戏时间</param>
    /// <returns></returns>
    protected float GetSpeed(SpeedEvent[] events, float time)
    {
        //遍历所有速度事件
        for (int i = 0; i < events.Length; i++)
        {
            SpeedEvent ev = events[i];

            float start = ev.Start;
            float end = ev.End;
            float startSec = _chartPlayer.BeatToSeconds(ev.StartTime);
            float endSec = _chartPlayer.BeatToSeconds(ev.EndTime);

            // 如果time正在这个事件中
            if(time >= startSec && time <= endSec)
            {
                float a = (end - start) / (endSec - startSec); // 加速度 a = △v/△t
                float t = (float)(time - startSec); // 时间
                return start + a * t;
            }
            
            //同时也要处理与下一个速度事件之间的部分
            if(i < events.Length - 1) // 如果这不是最后一个事件
            {
                float nextStartSec = _chartPlayer.BeatToSeconds(events[i+1].StartTime);
                //如果time正在这个间隔中
                if(time >= endSec && time <= nextStartSec)
                {
                    return end;
                }
                //如果time在这个间隔之后
                else if(time > nextStartSec)
                {
                    continue;//继续到下一个速度事件
                }
            }
            else// 如果这是最后一个事件之后的间隔
            {
                return end;
            }
        }
        return events[^1].End;//理论上不会执行到这里
    }
    /// <summary>
    /// 更新音符位置（受判定线位置和速度影响）
    /// 可被HoldNoteNode重写
    /// </summary>
    public virtual void UpdateNote(double gameTime, JudgeLineNode fatherLine)
    {
        if (_data == null) return;

        float noteStartSec = _chartPlayer.BeatToSeconds(_data.StartTime);
        float noteEndSec = _data.EndTime != null ? _chartPlayer.BeatToSeconds(_data.EndTime) : noteStartSec;

        // _data.VisibleTime 音符可视时间（打击前多少秒开始显现，默认99999.0）

        //处理显示和隐藏
        {
            if(_data.Type == 2) // hold需要特殊处理，当head到达判定线时，隐藏head的贴图
            {
                if(gameTime >= noteStartSec)
                {
                    _sprite.Visible = false;
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
                // 播放音效并生成打击特效
                if (audioStreamPlayer != null && audioStreamPlayer.Stream != null)
                {
                    audioStreamPlayer.Play();
                    // GD.Print($"[{Name}] 播放了音效 gameTime:{gameTime}");
                    //显示打击特效，此时不能用ChartPosToViewportPos(localChartPos)，应该用世界坐标
                    Vector2 globalChartPos = localChartPos + new Vector2(fatherLine._currentMoveX, fatherLine._currentMoveY);
                    _chartPlayer.effectsManager.CreateHitEffect(ChartPosToViewportPos(globalChartPos));
                }
                _hasPlayedHitSound = true;
            }
            else if (gameTime < hitTime)
            {
                // 时间回退到击中点之前，重置标记，允许再次触发
                _hasPlayedHitSound = false;
            }
            //输出日志
            // if(Name == "Note 0_0")
            // {
            //     GD.Print($"[{Name}] hitTime:{hitTime} gameTime:{gameTime} _hasPlayedHitSound:{_hasPlayedHitSound}");
            // }
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
            float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, noteStartSec);
            //note已经移动的位移
            float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
            localChartY = Math.Max(0, allDisplacement - nowDisplacement);

            localChartPos = new Vector2(localChartX,localChartY);

            //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
            Vector2 viewportPos = ChartPosToViewportPos(localChartPos);
            
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
        // {
        //     //第一阶段：head到达之前，localPosition保持变不变
        //     if(gameTime <= startSec)
        //     {
        //         // s = vt
        //         float s = speed * (endSec - startSec);
        //         endLocalChartPos = new Vector2(0,s);
        //         _endSprite.Position = ChartPosToViewportPos(new Vector2(0,s));
        //     }
        //     //第二阶段：hold正在缩小，localPosition不断减小至y=0
        //     else if(gameTime > startSec && gameTime < endSec)
        //     {
        //         // s = vt
        //         float s = (float)(speed * (endSec - gameTime));
        //         endLocalChartPos = new Vector2(0,s);
        //         _endSprite.Position = ChartPosToViewportPos(new Vector2(0,s));

        //     }
        //     //第三阶段：hold结束，隐藏自己
        //     //由于父类设置了隐藏，所以这里不需要进行任何操作
        // }

        // {
        //     float localChartX, localChartY;
        //     localChartX = _data.PositionX; 

        //     //全部位移
        //     float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, endSec);
        //     //note已经移动的位移
        //     float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
        //     localChartY = Math.Max(0, allDisplacement - nowDisplacement);

        //     endLocalChartPos = new Vector2(localChartX,localChartY);

        //     //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
        //     Vector2 viewportPos = ChartPosToViewportPos(localChartPos);

        //     //设定位置
        //     _endSprite.Position = viewportPos;

        // }

        {
            //第一阶段：head到达之前，localPosition保持变不变
            if(gameTime <= startSec)
            {
                float startSpeed = 120f*GetSpeed(fatherLine._data.EventLayers[0].SpeedEvents, startSec); // head落在判定线上时的速度
                float s = (float)(startSpeed * (endSec - startSec));
                endLocalChartPos = new Vector2(0,s);
                _endSprite.Position = ChartPosToViewportPos(endLocalChartPos);

                
            }
            //第二阶段：hold正在缩小，localPosition不断减小至y=0
            else if(gameTime > startSec && gameTime < endSec)
            {
                float localChartY;
                //全部位移
                float allDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, endSec);
                //note已经移动的位移
                float nowDisplacement = IntegralSpeedEvent(fatherLine._data.EventLayers[0].SpeedEvents, (float)gameTime);
                localChartY = Math.Max(0, allDisplacement - nowDisplacement);

                endLocalChartPos = new Vector2(0,localChartY);

                //注意：localChartX和localChartY是谱面坐标系的坐标，需要转换为godot坐标系
                Vector2 viewportPos = ChartPosToViewportPos(endLocalChartPos);

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
            Vector2 viewportPos = ChartPosToViewportPos(bodyLocalChartPos);
            
            //设定body位置
            _bodySprite.Position = viewportPos;

            //hold原尺寸为1900，缩放后为sizeY
            float sizeY = _sprite.Position.Y - _endSprite.Position.Y;
            _bodySprite.Scale = new Vector2(0.161f, sizeY/1900f);
        }

        
    }

    
}