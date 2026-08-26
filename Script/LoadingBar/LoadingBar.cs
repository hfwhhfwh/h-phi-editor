using Godot;
using System;

public partial class LoadingBar : CanvasLayer
{
	[Export] private Label _titleLabel;
	[Export] private ProgressBar _progressBar;
	[Export] private Label _numLabel;
	[Export] private Label _descriptionLabel;

	private string _title;
	private int _number, _total;
	private string _description;

	public string Title
	{
		get
		{
			return _title;
		}
		set
		{
			_title = value;
			_titleLabel.Text = value;
		}
	}

	public void SetProgress(int num, int total, string des)
	{
		_number = num;
		_total = total;
		_description = des;

		_numLabel.Text = $"{num}/{total}";
		_descriptionLabel.Text = des;
		_progressBar.Value = total <= 0 ? 100 : num * 100.0 / total;
	}
}
