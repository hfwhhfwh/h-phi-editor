using Godot;
using QuickType;
using System;
using HPhiEditorGame.Editor;

public partial class EasingEditor : PropertyEditorBase<EasingData>
{
    private SpinBox _easingTypeSpinBox;
    private OptionButton _funcBtn, _ioBtn;
    private LineEdit _leftEdit, _rightEdit;
    private EasingCurvePreview _easingCurvePreview;
    private EasingData _value;
    

    public override EasingData Value
    {
        get => _value;
        set { 
            _value = value; 

            // 检查数据是否有效
            int easingNum = EasingHelper.Convert.EasingToNumber(_value.EasingFunc, _value.EasingIO);
            if(easingNum == -1) _easingTypeSpinBox.Value = 1; // 同时会自动修改LineEvent数据

            RefreshUI();
        }
    }

    protected override void BuildUI()
    {
        var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(vbox);

        // 第一行：Func + IO
        HBoxContainer row1 = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddChild(row1);

        {    
            Label label = new Label{ Text = "缓动函数" };
            row1.AddChild(label);

            _easingTypeSpinBox = new SpinBox();
            row1.AddChild(_easingTypeSpinBox);

            _funcBtn = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            for (int i = 0; i < 11; i++) _funcBtn.AddItem($"{(EasingFunc)i}");
            row1.AddChild(_funcBtn);

            _ioBtn = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            for (int i = 0; i < 3; i++) _ioBtn.AddItem($"{(EasingIO)i}");
            row1.AddChild(_ioBtn);
        }

        
        // 第二行：Left + Right
        HBoxContainer row2 = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddChild(row2);

        {
            HBoxContainer rowLeft = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row2.AddChild(rowLeft);

            Label label = new Label{ Text = "左剪切" };
            rowLeft.AddChild(label);

            _leftEdit = new LineEdit { PlaceholderText = "Left", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            rowLeft.AddChild(_leftEdit);
        }

        {
            HBoxContainer rowRight = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row2.AddChild(rowRight);

            Label label = new Label{ Text = "右剪切" };
            rowRight.AddChild(label);

            _rightEdit = new LineEdit { PlaceholderText = "Right", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            rowRight.AddChild(_rightEdit);
        }

        // 第三行：曲线预览
        {
            CenterContainer row3 = new CenterContainer{SizeFlagsHorizontal = SizeFlags.ExpandFill};
            vbox.AddChild(row3);

            MarginContainer marginContainer = new MarginContainer{SizeFlagsHorizontal = SizeFlags.ExpandFill};
            marginContainer.AddThemeConstantOverride("margin_top", 50);
            marginContainer.AddThemeConstantOverride("margin_bottom", 50);
            row3.AddChild(marginContainer);

            _easingCurvePreview = new EasingCurvePreview();
            _easingCurvePreview.CustomMinimumSize = new Vector2(300, 200);
            marginContainer.AddChild(_easingCurvePreview);
        }

        // 事件
        _funcBtn.ItemSelected += idx => { _value.EasingFunc = (EasingFunc)idx; NotifyChanged(_value); RefreshUI();};
        _ioBtn.ItemSelected   += idx => { _value.EasingIO   = (EasingIO)idx;   NotifyChanged(_value); RefreshUI();};

        _easingTypeSpinBox.ValueChanged += (double v) => 
        {
            int intValue = Mathf.RoundToInt(v);
            if (EasingHelper.IsNumberValid(intValue))
            {
                (EasingFunc func, EasingIO io) = EasingHelper.Convert.NumberToEasing(intValue);
                _value.EasingFunc = func;
                _value.EasingIO = io;
            }

            NotifyChanged(_value);
            
            RefreshUI();
        };

        _leftEdit.TextSubmitted += text =>
        {
            if (float.TryParse(text, out float v))
            {
                _value.EasingLeft = Math.Clamp(v, 0f, 1f);
                _leftEdit.Text = _value.EasingLeft.ToString();
                NotifyChanged(_value);
            }
            else _leftEdit.Text = _value.EasingLeft.ToString();
        };

        _rightEdit.TextSubmitted += text =>
        {
            if (float.TryParse(text, out float v))
            {
                _value.EasingRight = Math.Clamp(v, 0f, 1f);
                _rightEdit.Text = _value.EasingRight.ToString();
                NotifyChanged(_value);
            }
            else _rightEdit.Text = _value.EasingRight.ToString();
        };
    }

    private void RefreshUI()
    {
        // if (_funcBtn == null) return;

        _funcBtn.Select((int)_value.EasingFunc);
        _ioBtn.Select((int)_value.EasingIO);
        
        int easingNum = EasingHelper.Convert.EasingToNumber(_value.EasingFunc, _value.EasingIO);
        _easingTypeSpinBox.SetValueNoSignal(easingNum == -1 ? 1 : easingNum);

        _leftEdit.Text = _value.EasingLeft.ToString();
        _rightEdit.Text = _value.EasingRight.ToString();

        _easingCurvePreview.EasingType = easingNum;
        _easingCurvePreview.LeftCut = _value.EasingLeft;
        _easingCurvePreview.RightCut = _value.EasingRight;
        _easingCurvePreview.QueueRedraw();
    }
}