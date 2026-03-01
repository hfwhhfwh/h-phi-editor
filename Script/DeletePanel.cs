using Godot;
using System;

public partial class DeletePanel : PanelContainer
{
	private int choosedChartId;

	public void SetInfo(int id)
	{
		choosedChartId = id;
	}
	public void OnDeleteButtonPressed()
	{
		
	}
}
