using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public partial class InfoEditPanel : Control
{
	public class Data
	{
		public string Name{ get; set; }

		public Dictionary<string, object> Properties { get; set; } = new();
	}

	private Data _data;
	private Theme _theme;
	[Export] private VBoxContainer _vBoxContainer;
	[Export] private Label _nameLabel;
	[Export] private Button _confirmButton;

	public Action<string, object> PropertyChanged;

	[Signal] public delegate void OnConfirmedEventHandler();

    public override void _Ready()
    {
        base._Ready();

		_theme = GD.Load<Theme>("res://theme_gray.tres");
		// _vBoxContainer = GetNode<VBoxContainer>(
		// 	"MarginContainer/VBoxContainer/ScrollContainer/VBoxContainer");
		// _nameLabel = GetNode<Label>("MarginContainer/VBoxContainer/Label");

		_confirmButton.ButtonUp += () =>
		{
			EmitSignal(SignalName.OnConfirmed);
		};
		
    }

	public void ShowInfos(Data data)
	{
		if (data == null) return;

		_data = data;

		_nameLabel.Text = $"正在编辑:{_data.Name}";

		// 清空原有控件（注意释放资源）
		foreach (Node child in _vBoxContainer.GetChildren())
		{
			_vBoxContainer.RemoveChild(child);
			child.QueueFree();
		}

		foreach (var kvp in _data.Properties)
		{
			string key = kvp.Key;
			object value = kvp.Value;

			// 每行一个 HBoxContainer：标签 + 编辑器
			HBoxContainer row = new HBoxContainer();
			row.SizeFlagsHorizontal = SizeFlags.Fill;

			Label label = new Label();
			label.Text = key;
			row.AddChild(label);

			Control editor = CreateEditorForValue(key, value);
			editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(editor);

			_vBoxContainer.AddChild(row);
		}

	}

	private Control CreateEditorForValue(string key, object value)
	{
		if (value == null)
		{
			// 空值默认显示为字符串输入
			return CreateStringEditor(key, "");
		}

		Type type = value.GetType();

		if (type == typeof(string))
		{
			return CreateStringEditor(key, (string)value);
		}
		else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
		{
			return CreateIntEditor(key, Convert.ToInt64(value));
		}
		else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
		{
			return CreateDoubleEditor(key, Convert.ToDouble(value));
		}
		else if (type == typeof(bool))
		{
			return CreateBoolEditor(key, (bool)value);
		}
		else if (type.IsEnum)
		{
			return CreateEnumEditor(key, value);
		}
		else if(type == typeof(Beat))
		{
			return CreateBeatEditor(key, (Beat)value);
		}
		// else if (type == typeof(EasingData))
		// {
		// 	// return CreateEasingEditor(key, (EasingData)value);
		// }
		else
		{
			// 未知类型回退为字符串输入（调用 ToString）
			return CreateStringEditor(key, value.ToString());
		}
	}

	private Control CreateStringEditor(string key, string initialValue)
	{
		LineEdit lineEdit = new LineEdit();
		lineEdit.Text = initialValue;
		lineEdit.TextSubmitted += (newText) =>
		{
			_data.Properties[key] = newText;
			OnValueChanged(key, newText);
		};
		return lineEdit;
	}

	private Control CreateIntEditor(string key, long initialValue)
    {
        SpinBox spinBox = new SpinBox();
        //spinBox.MinValue = min;
        //spinBox.MaxValue = max;
        //spinBox.Step = step;
        spinBox.AllowGreater = true;
        spinBox.AllowLesser = true;
        spinBox.Value = initialValue;

        // if (!allowDecimal)
        // {
        //     spinBox.Step = 1;
        // }

        spinBox.ValueChanged += (newValue) =>
        {
            long newInt;
			try
			{
				newInt = Convert.ToInt64(newValue);
				
			}
			catch(Exception e)
			{
				GD.PrintErr($"[{this.Name}] 输入整数非法:{e.Message}");
				//lineEdit.Text = $"{Convert.ToDouble(_data.Properties[key])}";
				return;
			}
        	_data.Properties[key] = newInt;
			OnValueChanged(key, newInt);
        };
        return spinBox;
    }

	private Control CreateDoubleEditor(string key, double initialValue)
	{
		LineEdit lineEdit = new LineEdit();
		lineEdit.Text = $"{initialValue}";
		lineEdit.TextSubmitted += (newValue) =>
		{
			double newDouble;
			try
			{
				newDouble = Convert.ToDouble(newValue);
				
			}
			catch(Exception e)
			{
				GD.PrintErr($"[{this.Name}] 输入浮点数非法:{e.Message}");
				lineEdit.Text = $"{Convert.ToDouble(_data.Properties[key])}";
				return;
			}
			
			_data.Properties[key] = newDouble;
			lineEdit.Text = $"{newDouble}";
			OnValueChanged(key, newDouble);
		};
		return lineEdit;
	}

	// private Control CreateNumberEditor(string key, double initialValue, double min, double max, double step, bool allowDecimal)
    // {
    //     SpinBox spinBox = new SpinBox();
    //     spinBox.MinValue = min;
    //     spinBox.MaxValue = max;
    //     spinBox.Step = step;
    //     spinBox.AllowGreater = false;
    //     spinBox.AllowLesser = false;
    //     spinBox.Value = initialValue;

    //     if (!allowDecimal)
    //     {
    //         spinBox.Step = 1;
    //     }

    //     spinBox.ValueChanged += (newValue) =>
    //     {
    //         if (allowDecimal)
    //             _data.Properties[key] = (double)newValue;
    //         else
    //             _data.Properties[key] = (int)newValue;
    //     };
    //     return spinBox;
    // }

    private Control CreateBoolEditor(string key, bool initialValue)
    {
        CheckBox checkBox = new CheckBox();
        checkBox.ButtonPressed = initialValue;
        checkBox.Toggled += (bool pressed) =>
		{
			_data.Properties[key] = pressed;
			OnValueChanged(key, pressed);
		};
        return checkBox;
    }

    private Control CreateEnumEditor(string key, object enumValue)
    {
        OptionButton optionButton = new OptionButton();
        Type enumType = enumValue.GetType();
        string[] names = Enum.GetNames(enumType);
        Array values = Enum.GetValues(enumType);

        int currentIndex = 0;
        for (int i = 0; i < names.Length; i++)
        {
            optionButton.AddItem(names[i]);
            if (Equals(enumValue, values.GetValue(i)))
                currentIndex = i;
        }
        optionButton.Selected = currentIndex;

        optionButton.ItemSelected += (index) =>
        {
			//GD.Print($"ItemSelected:{index}");
            object newEnum = values.GetValue((int)index);
            _data.Properties[key] = newEnum;
			OnValueChanged(key, newEnum);
        };
        return optionButton;
    }

	private Control CreateBeatEditor(string key, Beat initialValue)
	{
		HBoxContainer hBoxContainer = new();

		//LineEdit[] lineEdits = new LineEdit[3];
		
		for(int i = 0; i < 3; i++)
		{
			int index = i; // 捕获当前索引

			LineEdit lineEdit = new LineEdit();
			hBoxContainer.AddChild(lineEdit);

			lineEdit.Text = $"{initialValue.Values[i]}";

			lineEdit.TextSubmitted += (string newValue) =>
			{
				//尝试转换为数字
				long newInt;
				try
				{
					newInt = Convert.ToInt64(newValue);
					
				}
				catch(Exception e)
				{
					GD.PrintErr($"[{this.Name}] 输入整数非法:{e.Message}");
					// 恢复为当前值（从 _data 中取对应索引）
					Beat currentBeat = (Beat)_data.Properties[key];
					lineEdit.Text = currentBeat.Values[index].ToString();
					return;
				}
				// 更新 _data 中的 Beat 对象
				Beat beat = (Beat)_data.Properties[key];
				beat.Values[index] = (int)newInt;
				OnValueChanged(key, beat);
			};
		}

		return hBoxContainer;
	}

	// private Control CreateEasingEditor(string key, EasingData easingData)
	// {
	// 	EasingEditor editor = new();
	// 	// 初始化显示
	// 	editor.Init(
	// 		easingData.EasingFunc, 
	// 		easingData.EasingIO, 
	// 		easingData.EasingLeft, 
	// 		easingData.EasingRight
	// 	);

	// 	// 订阅所有子属性变更事件，更新数据并触发父级变化
	// 	editor.EasingFuncChanged += (newFunc) =>
	// 	{
	// 		var data = (EasingData)_data.Properties[key];
	// 		data.EasingFunc = newFunc;
	// 		OnValueChanged(key, data);
	// 	};
	// 	editor.EasingIOChanged += (newIO) =>
	// 	{
	// 		var data = (EasingData)_data.Properties[key];
	// 		data.EasingIO = newIO;
	// 		OnValueChanged(key, data);
	// 	};
	// 	editor.EasingLeftChanged += (newLeft) =>
	// 	{
	// 		var data = (EasingData)_data.Properties[key];
	// 		data.EasingLeft = newLeft;
	// 		OnValueChanged(key, data);
	// 	};
	// 	editor.EasingRightChanged += (newRight) =>
	// 	{
	// 		var data = (EasingData)_data.Properties[key];
	// 		data.EasingRight = newRight;
	// 		OnValueChanged(key, data);
	// 	};

	// 	return editor;
	// }

	public void OnValueChanged(string key, object value)
	{
		PropertyChanged?.Invoke(key, value);
	}

}

public class EasingData
{
	public EasingIO EasingIO { get; set; }

	public EasingFunc EasingFunc { get; set; }

	public float EasingLeft { get; set; }
	public float EasingRight { get; set; }

	public EasingData Duplicate()
	{
		return new EasingData
		{
			EasingIO = EasingIO,
			EasingFunc = EasingFunc,
			EasingLeft = EasingLeft,
			EasingRight = EasingRight
		};
	}
}
