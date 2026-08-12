using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public partial class ResourcePackOverview : Control
{
	// private static readonly string[] RequiredNoteTextures =
	// {
	// 	"click", "click_mh", "drag", "drag_mh", "flick", "flick_mh", 
	// 	"hold_head", "hold_head_mh", "hold_end", "hold_end_mh"
	// };
	[Export] private Control leftControl, rightControl;
	// [Export] private Godot.Collections.Array<Sprite2D> sprites;

	// private List<ValueTuple<Vector2, Texture2D>> layout;

	//[Export] private Godot.Collections.Dictionary<string, Sprite2D> sprites;
	[Export] private Sprite2D clickSprite, clickMhSprite, dragSprite, dragMhSprite, flickSprite, flickMhSprite;
	

	[Export] private Sprite2D holdHeadSprite, holdEndSprite;
	[Export] private Sprite2D holdHeadSpriteMh, holdEndSpriteMh;
	[Export] private float widthScale = 0.15f;
	private Dictionary<string, Sprite2D> normalNotesSprites;

	private Sprite2D holdBodySprite, holdBodySpriteMh;

	private ResourcePack _resourcePack;
	

    public override void _Ready()
    {
        base._Ready();

		normalNotesSprites = new (){
			{"click", clickSprite},
			{"click_mh", clickMhSprite},
			{"drag", dragSprite},
			{"drag_mh", dragMhSprite},
			{"flick", flickSprite},
			{"flick_mh", flickMhSprite},
		};
		
		holdBodySprite = new Sprite2D();
		holdBodySpriteMh = new Sprite2D();
		leftControl.AddChild(holdBodySprite);
		leftControl.AddChild(holdBodySpriteMh);

		holdHeadSprite.ItemRectChanged += ResetBodyPosition;
		holdEndSprite.ItemRectChanged += ResetBodyPosition;
		holdHeadSpriteMh.ItemRectChanged += ResetBodyPosition;
		holdEndSpriteMh.ItemRectChanged += ResetBodyPosition;
    }


	public void Show(ResourcePack pack)
	{
		_resourcePack = pack;
		// 1. 设置左边的note预览
		foreach(string key in normalNotesSprites.Keys)
		{
			Sprite2D sprite = normalNotesSprites[key];
			sprite.Texture = pack.textureDic[key];
			sprite.Scale = new Vector2(widthScale, widthScale);
		}

		holdHeadSprite.Texture = pack.holdHeadTexture;
		holdBodySprite.Texture = pack.holdBodyTexture;
		holdEndSprite.Texture = pack.holdEndTexture;
		holdHeadSpriteMh.Texture = pack.holdHeadTextureMh;
		holdBodySpriteMh.Texture = pack.holdBodyTextureMh;
		holdEndSpriteMh.Texture = pack.holdEndTextureMh;

		//设置偏移
		if(pack.holdHeadTexture == null)
		{
			GD.PrintErr("holdHeadTexture为null");
		}
		holdHeadSprite.Offset = new Vector2(0, pack.holdHeadTexture.GetSize().Y / 2f);
		holdHeadSpriteMh.Offset = new Vector2(0, pack.holdHeadTextureMh.GetSize().Y / 2f);
		holdEndSprite.Offset = new Vector2(0, -pack.holdEndTexture.GetSize().Y / 2f);
		holdEndSpriteMh.Offset = new Vector2(0, -pack.holdEndTextureMh.GetSize().Y / 2f);

		CallDeferred(MethodName.ResetBodyPosition);
		// ResetBodyPosition();

	}

	private void ResetBodyPosition()
	{
		// 设置body位置
		holdBodySprite.GlobalPosition = (holdHeadSprite.GlobalPosition + holdEndSprite.GlobalPosition) / 2f;
		holdBodySpriteMh.GlobalPosition = (holdHeadSpriteMh.GlobalPosition + holdEndSpriteMh.GlobalPosition) / 2f;

		// 设置body尺寸
		float scaleY = holdHeadSprite.GlobalPosition.DistanceTo(holdEndSprite.GlobalPosition) / _resourcePack.holdBodyTexture.GetSize().Y;
		holdBodySprite.Scale = new Vector2(widthScale, scaleY);

		float scaleYMh = holdHeadSpriteMh.GlobalPosition.DistanceTo(holdEndSpriteMh.GlobalPosition) / _resourcePack.holdBodyTextureMh.GetSize().Y;
		holdBodySpriteMh.Scale = new Vector2(widthScale, scaleYMh);

		// GD.Print($"holdHeadSprite.GlobalPosition:{holdHeadSprite.GlobalPosition}, holdEndSprite.GlobalPosition:{holdEndSprite.GlobalPosition}, holdBodySprite.GlobalPosition:{holdBodySprite.GlobalPosition}");
		// GD.Print($"sizeY:{_resourcePack.holdBodyTexture.GetSize().Y}");
	}


	private void PutImage(Control parent, Vector2 pos, Texture2D texture, float scale)
	{
		Sprite2D sprite = new Sprite2D();
		sprite.Texture = texture;
		sprite.Position = pos;
		sprite.Scale = new Vector2(scale, scale);

		parent.AddChild(sprite);

	}
}
