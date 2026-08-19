using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.IO;


public partial class CreateChartPanel : PanelContainer
{

	[Signal] public delegate void ChartCreatedEventHandler(ChartInfo chartInfo, string songPath, Texture2D picTexture);
    [Signal] public delegate void CancelledEventHandler();

    FileDialogManager fileDialogManager;

    // 输入字段
    [Export] private LineEdit _nameEdit, _musicPathEdit, _picPathEdit, _bpmEdit, _composerEdit, _charterEdit;
    [Export] private Button _musicBtn, _picBtn;
    [Export] private TextureRect _picTextureRect;

    private Texture2D _picTexture;

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
		fileDialogManager.ShowOpenDialog(
            (path) =>
            {
                _musicPathEdit.Text = path;
            },
            filters
        );
	}
	
    public void OnSelectPicture()
	{
        if(OS.HasFeature("windows") || OS.HasFeature("Windows") || Engine.IsEditorHint())
        {
            PickImageNative();
        }
        else if(OS.HasFeature("android") || OS.HasFeature("Android"))
        {
            PopupMenuHelper.Instance.ShowPopupMenu(
                this,
                GetGlobalMousePosition() + new Vector2(30, 30),
                new List<PopupMenuItem>
                {
                    new PopupMenuItem{Text = "文件管理器", Callback = PickImageNative},
                    new PopupMenuItem{Text = "相册", Callback = PickImageGalleryAndroid}
                }
            );
        }
        else if(OS.HasFeature("ios"))
        {
            PopupMenuHelper.Instance.ShowPopupMenu(
                this,
                GetGlobalMousePosition() + new Vector2(30, 30),
                new List<PopupMenuItem>
                {
                    new PopupMenuItem{Text = "文件管理器", Callback = PickImageNative},
                    new PopupMenuItem{Text = "相册", Callback = PickImageGalleryIOS}
                }
            );
        }
	}

    // ==================== 选择图片相关方法 ====================
    private void PickImageNative()
    {
        //调用 FileDialogManager 选择文件，更新输入框
        string[] filters = 
        {
            "*.png,*.jpg,*.jpeg,*.bmp,*.webp;图像文件;image/png,image/jpg,image/jpeg,image/bmp,image/webp"
        };
        fileDialogManager.ShowOpenDialog(
            (path) =>
            {
                _picPathEdit.Text = path;
                //显示曲绘
                Image textureImage = Image.LoadFromFile(path);
                if(textureImage == null)
                {
                    // 用户取消选择
                    // GD.PrintErr($"[ExportPanel] SetInfo() textureImage == null picturePath:{path}");
                    return;
                }
                _picTextureRect.Texture = ImageTexture.CreateFromImage(textureImage);
            },
            filters
        );
    }

    private void PickImageGalleryAndroid()
    {
        ImagePicker.Instance.PickImageFromGallery(
            (ImageTexture texture) =>
            {
                if(texture == null)
                {
                    // 用户取消选择
                    // GD.PrintErr($"[ExportPanel] SetInfo() texture == null");
                    return;
                }
                _picTexture = texture;
                _picTextureRect.Texture = texture;
            }
        );
    }

    private void PickImageGalleryIOS()
    {
        IOSPhotoPicker.Instance.PickImageFromGallery(
            (Image image) =>
            {
                if(image == null)
                {
                    // 用户取消选择
                    return;
                }

                ImageTexture texture = ImageTexture.CreateFromImage(image);
                if(texture == null)
                {
                    GD.PrintErr($"[{Name}] 从Image转换为ImageTexture失败");
                    return;
                }

                _picTexture = texture;
                _picTextureRect.Texture = texture;
            }
        );
    }

    public void OnConfirm()
    {
        string id = Util.GenerateRandomNumId(14);
        ChartInfo data = new ChartInfo
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
        EmitSignal(SignalName.ChartCreated, data, _musicPathEdit.Text, _picTexture);
    }

    public void OnCancel() => EmitSignal(SignalName.Cancelled);
	
}
