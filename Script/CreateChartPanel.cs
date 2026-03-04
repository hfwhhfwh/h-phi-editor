using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;


public partial class CreateChartPanel : PanelContainer
{

	[Signal] public delegate void ChartCreatedEventHandler(ChartInfo chartInfo, string songPath, string picPath);
    [Signal] public delegate void CancelledEventHandler();

    FileDialogManager fileDialogManager;

    // 输入字段
    [Export] private LineEdit _nameEdit, _musicPathEdit, _picPathEdit, _bpmEdit, _composerEdit, _charterEdit;

    public override void _Ready()
    {
        // 获取节点引用
        fileDialogManager = GetNode<FileDialogManager>("/root/FileDialogManager");
    }

    public void OnSelectMusic()
	{
		//调用 FileDialogManager 选择文件，更新输入框
        string[] filters = 
		{
			"*.mp3,*.wav,*.ogg,*.flac,*.aac,*.m4a,*.wma,*.aiff;音频文件;audio/mpeg,audio/x-wav,audio/ogg,audio/flac,audio/aac,audio/mp4,audio/x-ms-wma,audio/aiff"
		};
		fileDialogManager.ShowNativeOpenDialog(
            (path) =>
            {
                _musicPathEdit.Text = path;
            },
            filters
        );
	}
	
    public void OnSelectPicture()
	{
		//调用 FileDialogManager 选择文件，更新输入框
        string[] filters = 
        {
            "*.png,*.jpg,*.jpeg,*.bmp,*.webp;图像文件;image/png,image/jpg,image/jpeg,image/bmp,image/webp"
        };
		fileDialogManager.ShowNativeOpenDialog(
            (path) =>
            {
                _picPathEdit.Text = path;
            },
            filters
        );
	}

    public void OnConfirm()
    {
        string id = Util.GenerateRandomId(14);
        var data = new ChartInfo
        {
            Id = id,
            Name = _nameEdit.Text,
            SongFileName = _musicPathEdit.Text.GetFile(),
            PictureFileName = _picPathEdit.Text.GetFile(),
            Bpm = float.Parse(_bpmEdit.Text),
            Composer = _composerEdit.Text,
            Charter = _charterEdit.Text,
            Duration = Util.GetMusicDuration(_musicPathEdit.Text)
        };
        // 通过服务创建，但这里不直接调用服务，而是发出信号，由上层处理
        EmitSignal(SignalName.ChartCreated, data, _musicPathEdit.Text, _picPathEdit.Text);
    }

    public void OnCancel() => EmitSignal(SignalName.Cancelled);
	
}
