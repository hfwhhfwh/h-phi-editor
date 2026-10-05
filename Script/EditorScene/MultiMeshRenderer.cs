using Godot;
using QuickType;
using System;
using System.Collections.Generic;

/// <summary>
/// 编辑面板的 MultiMesh 渲染器。
///
/// 从 BaseEditPanel 中抽出的职责：
/// 1. 按 key 注册 MultiMesh / MultiMeshInstance2D 池；
/// 2. 每帧渲染「点对象」（RenderObject）与「长条对象」（RenderLongObject）；
/// 3. 可见实例计数、动态扩容，以及超出面板范围的裁剪。
///
/// 说明：它是普通 C# 类，不占场景树节点。
/// MultiMeshInstance2D 挂在构造时传入的父 Control 上。
/// </summary>
public sealed class MultiMeshRenderer
{
    /// <summary>MultiMeshInstance2D 的挂载点，同时用于面板范围内的裁剪判断</summary>
    private readonly Control _parent;

    /// <summary>谱面坐标 → 面板坐标</summary>
    private readonly CoordinateComponent _coord;

    private readonly Dictionary<string, MultiMesh> _multiMeshes = new();
    private readonly Dictionary<string, MultiMeshInstance2D> _instances = new();
    private readonly Dictionary<string, int> _visibleCounts = new();

    public MultiMeshRenderer(Control parent, CoordinateComponent coord)
    {
        _parent = parent;
        _coord = coord;
    }

    /// <summary>
    /// 注册一个 MultiMesh 池
    /// </summary>
    /// <param name="key">池的标识，渲染时按同一个 key 取用</param>
    /// <param name="texture">实例贴图</param>
    /// <param name="instanceCount">初始容量</param>
    /// <param name="zIndex">绘制层级</param>
    public void Register(string key, Texture2D texture, int instanceCount, int zIndex = 1)
    {
        //设置Multimesh
        MultiMesh multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            InstanceCount = 0,
            VisibleInstanceCount = 0,
            UseColors = true, // 用于提示选中
        };
        multiMesh.InstanceCount = instanceCount;
        _multiMeshes[key] = multiMesh;

        MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
        multiMeshInstance.Texture = texture;
        multiMeshInstance.Multimesh = multiMesh;
        multiMeshInstance.ZIndex = zIndex;
        _instances[key] = multiMeshInstance;

        // 根据纹理实际尺寸创建 QuadMesh
        QuadMesh quad = new QuadMesh();
        quad.Size = new Vector2(texture.GetSize().X, -texture.GetSize().Y);   // 保持宽高比，去掉负值
        multiMeshInstance.Multimesh.Mesh = quad;

        _parent.AddChild(multiMeshInstance);

        _visibleCounts[key] = 0;
    }

    /// <summary>
    /// 每帧渲染前把可见数量归零
    /// </summary>
    public void BeginFrame()
    {
        foreach (string key in _visibleCounts.Keys)
        {
            _visibleCounts[key] = 0;
        }
    }

    /// <summary>
    /// 每帧渲染结束后把所有池的可见实例数量同步给 MultiMesh
    /// </summary>
    public void EndFrame()
    {
        foreach (string key in _visibleCounts.Keys)
        {
            _multiMeshes[key].VisibleInstanceCount = _visibleCounts[key];
        }
    }

    /// <summary>
    /// 动态扩容
    /// </summary>
    public void EnsureCapacity(string key, int needed)
    {
        if (needed <= 0) return;

        MultiMesh mm = _multiMeshes[key];
        if (needed <= mm.InstanceCount) return; // 容量足够，直接返回

        mm.InstanceCount = MathUtil.NextPowerOfTwo(needed);
        GD.Print($"[{_parent?.Name}] MultiMesh '{key}' 扩容至 {mm.InstanceCount}");
    }

    /// <summary>
    /// 渲染一个点对象（用 beat 定位 Y，用 localX 定位 X）
    /// </summary>
    public void RenderObject(string key, float localX, Beat beat, Vector2 offset, float scale,
                             Action<MultiMesh, int> renderEffect)
    {
        // 动态扩容
        if (_visibleCounts[key] + 1 > _multiMeshes[key].InstanceCount)
        {
            EnsureCapacity(key, _visibleCounts[key] + 1);
        }

        float beatValue = beat[0] + beat[1] * 1f / beat[2];
        float localY = _coord.GetPanelPosY(beatValue);

        // 裁切：超出面板范围则不渲染
        if (localX < 0 || localX > _parent.Size.X || localY < 0 || localY > _parent.Size.Y) return;

        // 构建变换：位置 + 固定缩放
        Transform2D transform = Transform2D.Identity
            .Translated(offset)                        // 对齐
            .Scaled(new Vector2(scale, scale))         // 缩放
            .Translated(new Vector2(localX, localY));  // 平移

        _multiMeshes[key].SetInstanceTransform2D(_visibleCounts[key], transform);
        _multiMeshes[key].SetInstanceColor(_visibleCounts[key], Colors.White);

        // 渲染效果
        renderEffect?.Invoke(_multiMeshes[key], _visibleCounts[key]);

        _visibleCounts[key]++;
    }

    /// <summary>
    /// 渲染一个长条对象（从 startBeat 拉伸到 endBeat）
    /// </summary>
    public void RenderLongObject(string key, float localX, Beat startBeat, Beat endBeat, Vector2 offset, float scale,
                                 Action<MultiMesh, int> renderEffect)
    {
        // 动态扩容
        if (_visibleCounts[key] + 1 > _multiMeshes[key].InstanceCount)
        {
            EnsureCapacity(key, _visibleCounts[key] + 1);
        }

        float startBeatValue = startBeat[0] + startBeat[1] * 1f / startBeat[2];
        float startLocalY = _coord.GetPanelPosY(startBeatValue);

        float endBeatValue = endBeat[0] + endBeat[1] * 1f / endBeat[2];
        float endLocalY = _coord.GetPanelPosY(endBeatValue);

        float bodyLength = startLocalY - endLocalY;   // 正数表示向下延伸

        float midLocalY = (startLocalY + endLocalY) / 2f;

        // 计算 Y 方向缩放：长度 / 纹理高度
        Texture2D texture = _instances[key].Texture;
        float scaleY = bodyLength / texture.GetSize().Y;

        Transform2D transform = Transform2D.Identity
            .Translated(offset)                        // 对齐
            .Scaled(new Vector2(scale, scaleY))        // 缩放
            .Translated(new Vector2(localX, midLocalY));  // 平移

        _multiMeshes[key].SetInstanceTransform2D(_visibleCounts[key], transform);
        _multiMeshes[key].SetInstanceColor(_visibleCounts[key], Colors.White);

        // 渲染效果
        renderEffect?.Invoke(_multiMeshes[key], _visibleCounts[key]);

        _visibleCounts[key]++;
    }
}
