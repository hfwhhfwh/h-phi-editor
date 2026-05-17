using Godot;
using System;

public partial class EventEditPanel : Panel
{
	[ExportGroup("网格布局设置")]
	[Export] private float horMargin = 50; // 横线的左右留空
	[Export] private int verLineCount = 5;
	[Export] private float verMargin = 100; // 竖线的左右留空
	[Export] private int subBeatCount = 4; // 每个Beat被分割为多少个音符

	[ExportGroup("网格样式设置")]
	//线条参数
	[Export] private Color horColor = Colors.Red;
	[Export] private float horWidth = 1;
	[Export] private Color verColor = Colors.Green;
	[Export] private float verWidth = 1;
	[Export] private Color horSubColor = Colors.Yellow;
	[Export] private float horSubWidth = 1;

	//字体
	Font font = ThemeDB.FallbackFont;

	public float horOffsetSmoothed; // 用于使竖直滚动更平滑
	public float horSeparationSmoothed; // 用于使竖直缩放更平滑
    public override void _Draw()
    {
        //画横线
		//先画上半部分
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Ceil(horOffsetBeat);
			float y = Size.Y/2 - (Mathf.Ceil(horOffsetBeat) - horOffsetBeat) * horSeparationSmoothed;
			for(int i=0;i<=100 && y>=0;i++)
			{
				Vector2 from = new Vector2(horMargin,y);
				Vector2 to = new Vector2(Size.X - horMargin, y);
				DrawLine(from, to, horColor, horWidth, true);

				Vector2 charPos = new Vector2(horMargin / 2f, y);
				DrawString(font, charPos, $"{num}", HorizontalAlignment.Center, modulate:Colors.White, fontSize:20);

				y -= horSeparationSmoothed;   //逐步向上移动
				num++;
			}
		}

		//下半部分同理，注意不能绘制0以下
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Floor(horOffsetBeat);
			float y = Size.Y/2 + (horOffsetBeat - Mathf.Floor(horOffsetBeat)) * horSeparationSmoothed;
			for(int i=0;i<=100 && y<=Size.Y;i++)
			{
				Vector2 from = new Vector2(horMargin,y);
				Vector2 to = new Vector2(Size.X - horMargin, y);
				DrawLine(from, to, horColor, horWidth, true);

				Vector2 charPos = new Vector2(horMargin / 2f, y);
				DrawString(font, charPos, $"{num}", HorizontalAlignment.Center, modulate:Colors.White, fontSize:20);

				y += horSeparationSmoothed;   //逐步向上移动
				num--;
				if(num < 0) break;
			}
		}

		//画竖线
		{
			float verSeparation = (Size.X - 2*verMargin) / (verLineCount - 1);
			for(int i = 0; i < verLineCount; i++)
			{
				float x = verMargin + i*verSeparation;
				Vector2 from = new Vector2(x,0);
				Vector2 to = new Vector2(x,Size.Y);
				DrawLine(from, to, verColor, verWidth, true);
			}
		}

		//画小横线
		//先画上半部分
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Ceil(horOffsetBeat);
			float y = Size.Y/2 - (Mathf.Ceil(horOffsetBeat) - horOffsetBeat) * horSeparationSmoothed;
			for(int i=0;i<=100 && y>=0;i++)
			{
				//找到基准节拍线，向上画subBeatCount-1条横线
				for(int j = 1; j <= subBeatCount - 1; j++)
				{
					float subY = y - (horSeparationSmoothed / subBeatCount * j);
					//不让横线超出边界
					if(subY < 0) break;
					Vector2 from = new Vector2(horMargin,subY);
					Vector2 to = new Vector2(Size.X - horMargin, subY);
					DrawLine(from, to, horSubColor, horSubWidth, true);
				}
				y -= horSeparationSmoothed;   //逐步向上移动
				num++;
			}
		}
		//下半部分同理
		{
			float horOffsetBeat = horOffsetSmoothed / horSeparationSmoothed;
			float num = Mathf.Floor(horOffsetBeat);
			float y = Size.Y/2 + (horOffsetBeat - Mathf.Floor(horOffsetBeat)) * horSeparationSmoothed;
			for(int i=0;i<=100 && y<=Size.Y + horSeparationSmoothed;i++) // Size.Y + horSeparationSmoothed防止最底部因为节拍线不显示导致小横线也不显示
			{
				//找到基准节拍线，向上画subBeatCount-1条横线
				for(int j = 1; j <= subBeatCount - 1; j++)
				{
					float subY = y - (horSeparationSmoothed / subBeatCount * j);
					//不让横线超出边界
					if(subY < 0) break;
					Vector2 from = new Vector2(horMargin,subY);
					Vector2 to = new Vector2(Size.X - horMargin, subY);
					DrawLine(from, to, horSubColor, horSubWidth, true);
				}
				y += horSeparationSmoothed;   //逐步向上移动
				num--;
				if(num < 0) break;
			}
		}
    }

}
