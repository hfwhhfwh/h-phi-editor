using Godot;
using QuickType;
using System;
using System.Collections.Generic;

public abstract partial class BaseEditPanel : Panel
{
    // ---- 网格布局 ----
    [ExportGroup("网格布局设置")]
    [Export] protected float horMargin = 50;
    [Export] protected float verMargin = 100;
    [Export] protected int subBeatCount = 4;
    [Export] protected int verLineCount = 5; // 子类可重写默认值

    // ---- 网格样式 ----
    [ExportGroup("网格样式设置")]
    [Export] protected Color horColor = Colors.Red;
    [Export] protected float horWidth = 1;
    [Export] protected Color verColor = Colors.Green;
    [Export] protected float verWidth = 1;
    [Export] protected Color horSubColor = Colors.Yellow;
    [Export] protected float horSubWidth = 1;

    // ---- 滚动/缩放 ----
    public float horOffsetSmoothed;
    public float horSeparationSmoothed;

    // ---- 数据 ----
    public Chart editingChart;
    protected int editingLineId;

    // ---- 对象池 ----
    protected List<Node2D> nodePool = new();
    protected int poolSize = 0;

    // ---- 字体 ----
    protected Font font = ThemeDB.FallbackFont;

    // ---- 初始化池（子类调用） ----
    protected void InitializeNodePool(int capacity, Func<Node2D> createNodeFunc)
    {
        for (int i = 0; i < capacity; i++)
        {
            var node = createNodeFunc();
            node.Visible = false;
            node.ZIndex = 10;
            AddChild(node);
            nodePool.Add(node);
        }
        poolSize = capacity;
    }

    protected void ExpandNodePool(int additional, Func<Node2D> createNodeFunc)
    {
        for (int i = 0; i < additional; i++)
        {
            var node = createNodeFunc();
            node.Visible = false;
            node.ZIndex = 10;
            AddChild(node);
            nodePool.Add(node);
        }
        poolSize += additional;
    }

    protected void HideAllNodes()
    {
        foreach (var node in nodePool)
            node.Visible = false;
    }

    // ---- 绘制网格（基类统一实现） ----
    public override void _Draw()
    {
        DrawMainBeats();
        DrawSubBeats();
        DrawVerticalLines();
    }

    private void DrawMainBeats()
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

    }

    private void DrawSubBeats()
    {
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

    private void DrawVerticalLines()
    {
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

    }

    // ---- 刷新框架 ----
    public override void _Process(double delta)
    {
        UpdateVisuals();      // 子类实现具体对象位置/纹理更新
        QueueRedraw();        // 触发网格重绘
    }

    protected abstract void UpdateVisuals();
}
