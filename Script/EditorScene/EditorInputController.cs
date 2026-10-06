using Godot;
using System;

/// <summary>
/// 编辑器输入控制器。
///
/// 职责：
/// 1. 把鼠标滚轮 / 触摸板（InputManager）、虚拟摇杆的输入转换成视图状态变化（滚动、缩放、按时间滚动）；
/// 2. 维护视图平滑（HorOffsetSmoothed / HorSeparationSmoothed）；
/// 3. 处理全局快捷键（Ctrl+Z / Ctrl+Shift+Z / Ctrl+Y）。
///
/// 所有状态都写在 EditorContext 上，面板从 Context 读取，控制器本身不保存视图状态。
/// </summary>
public partial class EditorInputController : Node
{
    private EditorContext _context;
    private InputManager _inputManager;
    private ChartEditService _editService;

    private VirtualJoystick _slideJoystick;
    private VirtualJoystick _zoomJoystick;

    private float _verMouseSensitivity = 100f;   // 鼠标滚轮竖直滚动的灵敏度
    private float _zoomMouseSensitivity = 1f;    // 鼠标滚轮竖直缩放的灵敏度
    private float _verJoystickSensitivity = 1500f; // 虚拟摇杆竖直滚动的灵敏度
    private float _zoomJoystickSensitivity = 2f;   // 虚拟摇杆缩放的灵敏度
    private float _verJoystickTimeSens = 2f;       // 滚动时间时的虚拟摇杆灵敏度

    private bool _inputSubscribed;

    #if TOOLS
    private double _viewTimeUs;
    #endif

    public void Initialize(
        EditorContext context,
        InputManager inputManager,
        ChartEditService editService,
        VirtualJoystick slideJoystick,
        VirtualJoystick zoomJoystick,
        float verMouseSensitivity,
        float zoomMouseSensitivity,
        float verJoystickSensitivity,
        float zoomJoystickSensitivity,
        float verJoystickTimeSens)
    {
        _context = context;
        _inputManager = inputManager;
        _editService = editService;
        _slideJoystick = slideJoystick;
        _zoomJoystick = zoomJoystick;

        _verMouseSensitivity = verMouseSensitivity;
        _zoomMouseSensitivity = zoomMouseSensitivity;
        _verJoystickSensitivity = verJoystickSensitivity;
        _zoomJoystickSensitivity = zoomJoystickSensitivity;
        _verJoystickTimeSens = verJoystickTimeSens;

        if (_inputManager != null)
        {
            _inputManager.Slide += OnMouseSlide;
            _inputManager.Zoom += OnMouseZoom;
            _inputSubscribed = true;
        }

        // 显式启用未处理输入（Ctrl+Z / Ctrl+Y 等全局快捷键）
        SetProcessUnhandledInput(true);

        #if TOOLS
        Performance.AddCustomMonitor("EditorInput/ViewTimeUs", Callable.From(() => _viewTimeUs));
        #endif
    }

    /// <summary>
    /// InputManager 是 Autoload，生命周期比编辑器场景长，必须在这里断开订阅。
    /// </summary>
    public override void _ExitTree()
    {
        if (_inputSubscribed && _inputManager != null)
        {
            _inputManager.Slide -= OnMouseSlide;
            _inputManager.Zoom -= OnMouseZoom;
            _inputSubscribed = false;
        }

        #if TOOLS
        Performance.RemoveCustomMonitor("EditorInput/ViewTimeUs");
        #endif

        base._ExitTree();
    }

    private void OnMouseSlide(float x)
    {
        Slide(x * _verMouseSensitivity);
    }

    private void OnMouseZoom(float x)
    {
        Zoom(x * _zoomMouseSensitivity);
    }

    /// <summary>
    /// 由 EditorScene 每帧调用：处理虚拟摇杆并做视图平滑。
    /// </summary>
    public void ProcessInput(double delta)
    {
        if (_context == null) return;

        #if TOOLS
        ulong t1 = Time.GetTicksUsec();
        #endif

        //处理摇杆垂直滚动
        if (_slideJoystick != null && _slideJoystick.Output != Vector2.Zero)
        {
            if (PlayModeManager.PlayMode == PlayModeEnum.PlayerPause)
            {
                // 播放器暂停时，摇杆改为滚动时间轴
                SlideTime(-_slideJoystick.Output.Y * _verJoystickTimeSens * (float)delta);
            }
            else
            {
                Slide(-_slideJoystick.Output.Y * _verJoystickSensitivity * (float)delta);
            }
        }

        //处理摇杆缩放
        if (_zoomJoystick != null && _zoomJoystick.Output != Vector2.Zero)
        {
            Zoom(_zoomJoystick.Output.Y * _zoomJoystickSensitivity * (float)delta);
        }

        //平滑竖直滚动
        if (Math.Abs(_context.HorOffset - _context.HorOffsetSmoothed) > 0.001f)
        {
            _context.HorOffsetSmoothed += (_context.HorOffset - _context.HorOffsetSmoothed) * (float)delta * 18f;
        }
        else
        {
            _context.HorOffsetSmoothed = _context.HorOffset;
        }

        //平滑竖直缩放
        if (Math.Abs(_context.HorSeparation - _context.HorSeparationSmoothed) > 0.001f)
        {
            _context.HorSeparationSmoothed += (_context.HorSeparation - _context.HorSeparationSmoothed) * (float)delta * 18f;
        }
        else
        {
            _context.HorSeparationSmoothed = _context.HorSeparation;
        }

        #if TOOLS
        _viewTimeUs = Time.GetTicksUsec() - t1;
        #endif
    }

    /// <summary>
    /// 用于缩放
    /// </summary>
    /// <param name="zoomDelta">缩放比例</param>
    public void Zoom(float zoomDelta)
    {
        if (_context == null) return;

        _context.HorSeparation *= 1f + zoomDelta;

        // 限制不能缩放到负数
        if (_context.HorSeparation < 0) _context.HorSeparation = -_context.HorSeparation;

        //确保当前处于的beat不变
        _context.HorOffset = _context.BeatValue * _context.HorSeparation;
    }

    /// <summary>
    /// 按照编辑面板的距离滚动
    /// </summary>
    /// <param name="deltaY"></param>
    public void Slide(float deltaY)
    {
        if (_context == null) return;

        _context.HorOffset += deltaY;
        //限制不能滚动到0以下
        if (_context.HorOffset < 0) _context.HorOffset = 0;

        _context.BeatValue = _context.HorOffset / _context.HorSeparation;
    }

    /// <summary>
    /// 按照时间单位滚动
    /// </summary>
    /// <param name="deltaTime"></param>
    public void SlideTime(float deltaTime)
    {
        if (_context == null) return;

        _context.ChartTime = _context.ChartTime + deltaTime;

        //限制不能滚动到0以下
        if (_context.ChartTime < 0) _context.ChartTime = 0;

        // 此时BeatValue收到牵连改变，需要更新HorOffset
        _context.HorOffset = _context.BeatValue * _context.HorSeparation;
    }

    /// <summary>
    /// 处理全局快捷键（Ctrl+Z / Ctrl+Y）
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_editService == null) return;

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            bool ctrl = key.CtrlPressed || key.MetaPressed;

            if (ctrl && key.Keycode == Key.Z)
            {
                if (key.ShiftPressed) _editService.Redo();
                else                  _editService.Undo();
                GetViewport().SetInputAsHandled();
            }
            else if (ctrl && key.Keycode == Key.Y)
            {
                _editService.Redo();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
