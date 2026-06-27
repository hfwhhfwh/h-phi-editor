using Godot;
using System;
using System.Collections.Generic;

public partial class InfoEditPanel : Panel
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

	private Control CreateIntEditor(string key, double initialValue)
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

	public void OnValueChanged(string key, object value)
	{
		PropertyChanged?.Invoke(key, value);
	}

}
