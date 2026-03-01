using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;


public partial class CreateChartPanel : PanelContainer
{

	[Signal] public delegate void ChartCreatedEventHandler(ChartInfo chartInfo);
    [Signal] public delegate void CancelledEventHandler();

    // 输入字段
    private LineEdit _nameEdit, _musicPathEdit, _picPathEdit, _bpmEdit, _composerEdit, _charterEdit;

    public override void _Ready()
    {
        // 获取节点引用
    }

    public void OnSelectMusic()
	{
		//调用 FileDialogManager 选择文件，更新输入框
		//TODO
	}
	
    public void OnSelectPicture()
	{
		//TODO
	}

    public void OnConfirm()
    {
        var data = new ChartInfo
        {
            Name = _nameEdit.Text,
            SongFileName = _musicPathEdit.Text.GetFile(),
            PictureFileName = _picPathEdit.Text.GetFile(),
            Bpm = float.Parse(_bpmEdit.Text),
            Composer = _composerEdit.Text,
            Charter = _charterEdit.Text,
            Duration = Util.GetMusicDuration(_musicPathEdit.Text)
        };
        // 通过服务创建，但这里不直接调用服务，而是发出信号，由上层处理
        EmitSignal(SignalName.ChartCreated, data);
    }

    public void OnCancel() => EmitSignal(SignalName.Cancelled);
	
}
