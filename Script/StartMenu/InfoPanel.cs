using Godot;
using System;

public partial class InfoPanel : PanelContainer
{
	FileDialogManager fileDialogManager;

	ChartInfo editingInfo;
    string newSongPath;
    string newPicPath;

	[Signal] public delegate void ConfirmedEventHandler(ChartInfo chartInfo, string songPath, string picPath);
	[Signal] public delegate void CancelledEventHandler();

	// 输入字段
    [Export] private LineEdit _nameEdit, _musicPathEdit, _picPathEdit, _composerEdit, _charterEdit;

	public override void _Ready()
    {
        // 获取节点引用
        fileDialogManager = GetNode<FileDialogManager>("/root/FileDialogManager");
    }

	public void SetInfo(ChartInfo chartInfo)
	{
		editingInfo = chartInfo;

		_nameEdit.Text = chartInfo.Name;
        //不显示旧文件实际存储位置，如果用户选择了新的文件，则会显示出新的路径
		_musicPathEdit.Text = "";
		_picPathEdit.Text = "";
		_composerEdit.Text = chartInfo.Composer;
		_charterEdit.Text = chartInfo.Charter;

        //null表示没有修改
        newPicPath = null;
        newSongPath = null;

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
                if(path != null)
                {
                    _musicPathEdit.Text = path;
                    newSongPath = path;
                }
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
                if(path != null)
                {
                    _picPathEdit.Text = path;
                    newPicPath = path;
                }
            },
            filters
        );
	}

	public void OnConfirm()
    {
        ChartInfo newInfo = editingInfo.Duplicate();
        newInfo.Name = _nameEdit.Text;
        newInfo.Composer = _composerEdit.Text;
        newInfo.Charter = _charterEdit.Text;
        newInfo.Duration = newSongPath == null ? editingInfo.Duration : Util.GetMusicDuration(newSongPath);

        if (!string.IsNullOrEmpty(newSongPath))
        {
            newInfo.SongFileName = $"{editingInfo.Id}.{newSongPath.GetExtension()}";
        }
        if (!string.IsNullOrEmpty(newPicPath))
        {
            newInfo.PictureFileName = $"{editingInfo.Id}.{newPicPath.GetExtension()}";
        }
        
        // var data = new ChartInfo
        // {
        //     Id = editingInfo.Id,
        //     Name = _nameEdit.Text,
        //     // 保持原文件名不变，确保 info.txt 中的 Song/Picture 字段不会改变
        //     SongFileName = editingInfo.SongFileName,
        //     PictureFileName = editingInfo.PictureFileName,
        //     Bpm = editingInfo.Bpm,
        //     Composer = _composerEdit.Text,
        //     Charter = _charterEdit.Text,
        //     Duration = newSongPath == null ? editingInfo.Duration : Util.GetMusicDuration(newSongPath)
        // };
        // 发出信号，由上层处理
        EmitSignal(SignalName.Confirmed, newInfo, newSongPath, newPicPath);

        GD.Print($"[InfoPanel] 修改 newSongPath:{newSongPath} newPicPath:{newPicPath}");
    }

    public void OnCancel() => EmitSignal(SignalName.Cancelled);
}
