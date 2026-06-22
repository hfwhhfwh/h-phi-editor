using Godot;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

public partial class ExportPanel : PanelContainer
{
	private string exportingChartID;
	FileDialogManager fileDialogManager;

	[Signal] public delegate void ConfirmedEventHandler(string chartID);
	[Signal] public delegate void CancelledEventHandler();

	[Export] private LineEdit _nameEdit, _composerEdit, _charterEdit;
	[Export] private TextureRect _picTextureRect;

	public override void _Ready()
    {
        // 获取节点引用
        fileDialogManager = GetNode<FileDialogManager>("/root/FileDialogManager");
    }

	public void SetInfo(ChartInfo chartInfo)
	{
		_nameEdit.Text = chartInfo.Name;
		_composerEdit.Text = chartInfo.Composer;
		_charterEdit.Text = chartInfo.Charter;

		//显示曲绘
		Image textureImage = Image.LoadFromFile(chartInfo.PicturePath);
        if(textureImage == null)
        {
            GD.PrintErr($"[ExportPanel] SetInfo() textureImage == null picturePath:{chartInfo.PicturePath}");
        }
		_picTextureRect.Texture = ImageTexture.CreateFromImage(textureImage);

		//设置当前正在导出的铺面ID
		exportingChartID = chartInfo.Id;

	}

	public void OnConfirm()
    {
        // 发出信号，由上层处理
        EmitSignal(SignalName.Confirmed, exportingChartID);
    }

    public void OnCancel() => EmitSignal(SignalName.Cancelled);
}
