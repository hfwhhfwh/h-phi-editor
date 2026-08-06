using Godot;
using System;

public partial class EasingEditor : Control
{
	[Export] private OptionButton easingFuncOptionButton;
	[Export] private OptionButton easingIOOptionButton;
	[Export] private LineEdit easingLeftLineEdit;
	[Export] private LineEdit easingRightLineEdit;
	
	private EasingFunc currentEasingFunc;
	private EasingIO currentEasingIO;
	private float currentEasingLeft;
	private float currentEasingRight;

	public event Action<EasingFunc> EasingFuncChanged;
	public event Action<EasingIO> EasingIOChanged;
	public event Action<float> EasingLeftChanged;
	public event Action<float> EasingRightChanged;

	private bool _isReady = false;
	
	/// <summary>
	/// 初始化函数（在Ready之后调用）
	/// </summary>
	/// <param name="easingFunc"></param>
	/// <param name="easingIO"></param>
	/// <param name="easingLeft"></param>
	/// <param name="easingRight"></param>
	public void Init(EasingFunc easingFunc, EasingIO easingIO, float easingLeft, float easingRight)
	{
		currentEasingFunc = easingFunc;
		currentEasingIO = easingIO;
		currentEasingLeft = easingLeft;
		currentEasingRight = easingRight;
		if (_isReady) RefreshUI();
		

		// currentEasingFunc = easingFunc;
		// easingFuncOptionButton.Select((int)easingFunc);

		// currentEasingIO = easingIO;
		// easingIOOptionButton.Select((int)easingIO);

		// currentEasingLeft = easingLeft;
		// easingLeftLineEdit.Text = $"{easingLeft}";

		// currentEasingRight = easingRight;
		// easingRightLineEdit.Text = $"{easingRight}";

	}

	private void RefreshUI()
    {
        easingFuncOptionButton.Select((int)currentEasingFunc);
        easingIOOptionButton.Select((int)currentEasingIO);
        easingLeftLineEdit.Text = currentEasingLeft.ToString();
        easingRightLineEdit.Text = currentEasingRight.ToString();
    }

    public override void _Ready()
    {
        base._Ready();

		// 设置easingFuncOptionButton和easingIOOptionButton
		// 填充下拉选项
		for(int i = 0; i < 11; i++)
		{
			EasingFunc func = (EasingFunc)i;
			easingFuncOptionButton.AddItem($"{func}");
		}

		for(int i = 0; i < 3; i++)
		{
			EasingIO io = (EasingIO)i;
			easingIOOptionButton.AddItem($"{io}");
		}

		easingFuncOptionButton.ItemSelected += (long index) =>
		{
			EasingFunc easingFunc = (EasingFunc)index;
			EasingFuncChanged?.Invoke(easingFunc);
		};

		easingIOOptionButton.ItemSelected += (long index) =>
		{
			EasingIO easingIO = (EasingIO)index;
			EasingIOChanged?.Invoke(easingIO);
		};

		// 设置easingLeftLineEdit
		easingLeftLineEdit.TextSubmitted += (string newString) =>
		{
			// 尝试转换为double
			double newDouble;
			try
			{
				newDouble = Convert.ToDouble(newString);
				
			}
			catch(Exception e)
			{
				GD.PrintErr($"[{this.Name}] 输入浮点数非法:{e.Message}");
				easingLeftLineEdit.Text = $"{currentEasingLeft}";
				return;
			}

			// 范围[0,1]
			newDouble = Math.Clamp(newDouble, 0, 1);
			
			currentEasingLeft = (float)newDouble;
			easingLeftLineEdit.Text = $"{newDouble}";

			//触发事件
			EasingLeftChanged?.Invoke((float)newDouble);
		};

		// 设置easingRightLineEdit
		easingRightLineEdit.TextSubmitted += (string newString) =>
		{
			// 尝试转换为double
			double newDouble;
			try
			{
				newDouble = Convert.ToDouble(newString);
				
			}
			catch(Exception e)
			{
				GD.PrintErr($"[{this.Name}] 输入浮点数非法:{e.Message}");
				easingLeftLineEdit.Text = $"{currentEasingRight}";
				return;
			}

			// 范围[0,1]
			newDouble = Math.Clamp(newDouble, 0, 1);
			
			currentEasingRight = (float)newDouble;
			easingRightLineEdit.Text = $"{newDouble}";

			//触发事件
			EasingRightChanged?.Invoke((float)newDouble);
		};

		// 应用初始数据
        RefreshUI();
        _isReady = true;
		
    }

}
