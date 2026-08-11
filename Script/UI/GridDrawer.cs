using Godot;
using System;

public partial class GridDrawer : Control
{
    public Control Parent { get; set; }

    // ==== 网格布局 ====
    [ExportGroup("网格布局设置")]
    [Export] public float HorMargin { get; set; } = 20;
    [Export] public int SubBeatCount { get; set; } = 4;
    [Export] public float VerMargin { get; set; } = 40;
    [Export] public int VerLineCount { get; set; } = 21;

    // ==== 网格样式 ====
    [ExportGroup("网格样式设置")]
    [Export] public Color HorColor { get; set; } = new Color(1f, 0, 0, 0.686f);
    [Export] public float HorWidth { get; set; } = 1;
    [Export] public Color HorSubColor { get; set; } = new Color(1f, 1f, 0, 0.588f);
    [Export] public float HorSubWidth { get; set; } = 1;
    [Export] public Color VerColor { get; set; } = new Color(0, 1f, 0, 0.588f);
    [Export] public float VerWidth { get; set; } = 1;
	[Export] public Color GroundLineColor { get; set; } = new Color(0.7f, 0.7f, 0.7f, 0.7f);
	[Export] public float GroundLineWidth { get; set; } = 3;
    [ExportGroup("")]

    // ==== 字体 ====
    [Export] public Font Font { get; set; }

    // ==== 滚动/缩放 ====
    public float HorOffset { get; set; }
    public float HorSeparation { get; set; }

	public float GroundY { get; set; }

    public override void _Ready()
    {
        base._Ready();

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // 放置拦截点击
    }


    public override void _Draw()
    {
        base._Draw();

		DrawGroundLine();
        DrawMainBeats();
        DrawSubBeats();
        DrawVerticalLines();

    }

	private void DrawGroundLine()
	{
		Vector2 from = new Vector2(HorMargin, GroundY);
		Vector2 to = new Vector2(Parent.Size.X - HorMargin, GroundY);
		DrawLine(from, to, GroundLineColor, GroundLineWidth, true);
	}

    private void DrawMainBeats()
    {
        //画横线
		//先画上半部分
		{
			float horOffsetBeat = HorOffset / HorSeparation;
			float num = Mathf.Ceil(horOffsetBeat);
			float y = GroundY - (Mathf.Ceil(horOffsetBeat) - horOffsetBeat) * HorSeparation;
			for(int i=0;i<=100 && y>=0;i++)
			{
				Vector2 from = new Vector2(HorMargin,y);
				Vector2 to = new Vector2(Parent.Size.X - HorMargin, y);
				DrawLine(from, to, HorColor, HorWidth, true);

				Vector2 charPos = new Vector2(HorMargin / 2f, y);
				DrawString(Font, charPos, $"{num}", HorizontalAlignment.Center, modulate:Colors.White, fontSize:20);

				y -= HorSeparation;   //逐步向上移动
				num++;
			}
		}

		//下半部分同理，注意不能绘制0以下
		{
			float horOffsetBeat = HorOffset / HorSeparation;
			float num = Mathf.Floor(horOffsetBeat);
			float y = GroundY + (horOffsetBeat - Mathf.Floor(horOffsetBeat)) * HorSeparation;
			for(int i=0;i<=100 && y<=Parent.Size.Y;i++)
			{
				Vector2 from = new Vector2(HorMargin,y);
				Vector2 to = new Vector2(Parent.Size.X - HorMargin, y);
				DrawLine(from, to, HorColor, HorWidth, true);

				Vector2 charPos = new Vector2(HorMargin / 2f, y);
				DrawString(Font, charPos, $"{num}", HorizontalAlignment.Center, modulate:Colors.White, fontSize:20);

				y += HorSeparation;   //逐步向上移动
				num--;
				if(num < 0) break;
			}
		}

    }

    private void DrawSubBeats()
    {
        //画小横线
		//先画上半部分
		{
			float horOffsetBeat = HorOffset / HorSeparation;
			float num = Mathf.Ceil(horOffsetBeat);
			float y = GroundY - (Mathf.Ceil(horOffsetBeat) - horOffsetBeat) * HorSeparation;
			for(int i=0;i<=100 && y>=0;i++)
			{
				//找到基准节拍线，向上画subBeatCount-1条横线
				for(int j = 1; j <= SubBeatCount - 1; j++)
				{
					float subY = y - (HorSeparation / SubBeatCount * j);
					//不让横线超出边界
					if(subY < 0) break;
					Vector2 from = new Vector2(HorMargin,subY);
					Vector2 to = new Vector2(Parent.Size.X - HorMargin, subY);
					DrawLine(from, to, HorSubColor, HorSubWidth, true);
				}
				y -= HorSeparation;   //逐步向上移动
				num++;
			}
		}
		//下半部分同理
		{
			float horOffsetBeat = HorOffset / HorSeparation;
			float num = Mathf.Floor(horOffsetBeat);
			float y = GroundY + (horOffsetBeat - Mathf.Floor(horOffsetBeat)) * HorSeparation;
			for(int i=0;i<=100 && y<=Parent.Size.Y + HorSeparation;i++) // Parent.Size.Y + horSeparationSmoothed防止最底部因为节拍线不显示导致小横线也不显示
			{
				//找到基准节拍线，向上画subBeatCount-1条横线
				for(int j = 1; j <= SubBeatCount - 1; j++)
				{
					float subY = y - (HorSeparation / SubBeatCount * j);
					//不让横线超出边界
					if(subY < 0) break;
					Vector2 from = new Vector2(HorMargin,subY);
					Vector2 to = new Vector2(Parent.Size.X - HorMargin, subY);
					DrawLine(from, to, HorSubColor, HorSubWidth, true);
				}
				y += HorSeparation;   //逐步向上移动
				num--;
				if(num < 0) break;
			}
		}
		
    }

    private void DrawVerticalLines()
    {
        //画竖线
		{
			float verSeparation = (Parent.Size.X - 2*VerMargin) / (VerLineCount - 1);
			for(int i = 0; i < VerLineCount; i++)
			{
				float x = VerMargin + i*verSeparation;
				Vector2 from = new Vector2(x,0);
				Vector2 to = new Vector2(x,Parent.Size.Y);
				DrawLine(from, to, VerColor, VerWidth, true);
			}
		}

    }

}
