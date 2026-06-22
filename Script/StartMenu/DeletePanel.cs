using Godot;
using System;
using System.Diagnostics.CodeAnalysis;

public partial class DeletePanel : PanelContainer
{
	[Export] private Label label;

	[Signal] public delegate void DeleteConfirmedEventHandler(string chartId);
	[Signal] public delegate void CancelledEventHandler();

	private string selectedChartId;

	/// <summary>
	/// 由外部调用，用于展示将要删除的铺面的信息
	/// </summary>
	/// <param name="chartInfo">谱面信息</param>
	public void SetInfo(ChartInfo chartInfo)
	{
		if(chartInfo == null)
		{
			GD.PrintErr($"{this.Name} SetInfo() chartInfo == null");
			return;
		}
		selectedChartId = chartInfo.Id;
		label.Text = $"确认要删除谱面吗？（不可撤回）\n名称：{chartInfo.Name}\n作曲家：{chartInfo.Composer}\nid:{chartInfo.Id}";
	}
	public void OnConfirm()
	{
		EmitSignal(SignalName.DeleteConfirmed, selectedChartId);
	}

	public void OnCancelled()
	{
		EmitSignal(SignalName.Cancelled);
	}
}
