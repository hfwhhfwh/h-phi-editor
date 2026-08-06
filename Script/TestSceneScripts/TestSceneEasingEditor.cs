using Godot;
using System;

public partial class TestSceneEasingEditor : Node
{
    [Export] private EasingEditor easingEditor;

    public override void _Ready()
    {
        // base._Ready();

        // easingEditor.EasingFuncChanged += OnEasingFuncChanged;
        // easingEditor.EasingIOChanged += OnEasingIOChanged;
        // easingEditor.EasingLeftChanged += OnEasingLeftChanged;
        // easingEditor.EasingRightChanged += OnEasingRightChanged;

        // easingEditor.Init(
        //     EasingFunc.Sine,
        //     EasingIO.In,
        //     0f,
        //     1f
        // );

    }

    public override void _ExitTree()
    {
        // base._ExitTree();

        // easingEditor.EasingFuncChanged -= OnEasingFuncChanged;
        // easingEditor.EasingIOChanged -= OnEasingIOChanged;
        // easingEditor.EasingLeftChanged -= OnEasingLeftChanged;
        // easingEditor.EasingRightChanged -= OnEasingRightChanged;
    }


    private void OnEasingFuncChanged(EasingFunc easingFunc)
    {
        GD.Print($"用户修改了缓动函数:{easingFunc}");
    }

    private void OnEasingIOChanged(EasingIO easingIO)
    {
        GD.Print($"用户修改了缓动IO:{easingIO}");
    }

    private void OnEasingLeftChanged(float easingLeft)
    {
        GD.Print($"用户修改了缓动左边界:{easingLeft}");
    }

    private void OnEasingRightChanged(float easingRight)
    {
        GD.Print($"用户修改了缓动右边界:{easingRight}");
    }

}
