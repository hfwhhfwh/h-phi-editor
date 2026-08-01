using Godot;
using QuickType;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;


public partial class NoteEditPanel : BaseEditPanel
{
	private enum SpriteType
	{
		Tap, Drag, Flick, HoldHead, HoldBody, HoldEnd
	}
	private readonly SpriteType[] allSpriteTypes = (SpriteType[])Enum.GetValues(typeof(SpriteType));

    private enum SelectMode
    {
        Single, // 单选
        Multi // 多选
    }
    private SelectMode selectMode = SelectMode.Single;

	[Export] private float noteScale = 0.1f;
    /// <summary>选择note时点击位置与实际位置的最大距离</summary>
    [Export] private float distanceThreshold = 40f;
    /// <summary> note被选中时的颜色滤镜 </summary>
    [Export] private Color selectedModulate;
    [Export] private Color deleteHighlightModulate;
    [Export] private Color toAddModulate;
    [Export] private Color boxColor;

	[ExportGroup("音符贴图")]
    [Export] private Texture2D tapTexture;
    [Export] private Texture2D dragTexture;
    [Export] private Texture2D flickTexture;
    [Export] private Texture2D holdHeadTexture;
    [Export] private Texture2D holdBodyTexture;
    [Export] private Texture2D holdEndTexture;

	// ---- Multimesh ---- 
	private Dictionary<SpriteType, MultiMesh> multiMeshes = new();
	private Dictionary<SpriteType, MultiMeshInstance2D> multiMeshInstances = new();
	private Dictionary<SpriteType, int> visibleCounts = new();

    private List<Note> selectedNotes = new();
    private List<Note> notesToDelete = new();

    [Signal] public delegate void OnNoteSelectedEventHandler(int lineId, int noteIndex);
    public event Action<NoteType, Beat, Beat, float> NoteAddRequested;
    public event Action<int, List<Note> > NoteDeleteRequested;

    private InputController _inputController;
    private BoxSelectController _boxSelectController;
    private CoordinateComponent _coordComponent;
    private DragPlaceComponent _dragPlaceComponent;

    /// <summary>
    /// 框选矩形框的起始坐标 坐标系：Control坐标
    /// </summary>
    private Vector2 boxStartPos;
    /// <summary>
    /// 框选矩形框的结束坐标 坐标系：Control坐标
    /// </summary>
    private Vector2 boxEndPos;

    public NoteType PlacingNote { get; set; } // 正在放置的note

    public override void _Ready()
    {
		//设置multiMeshInstance
		foreach(SpriteType type in allSpriteTypes)
		{
			Texture2D texture = type switch
			{
				SpriteType.Tap => tapTexture,
				SpriteType.Drag => dragTexture,
				SpriteType.Flick => flickTexture,
				SpriteType.HoldHead => holdHeadTexture,
				SpriteType.HoldBody => holdBodyTexture,
				SpriteType.HoldEnd => holdEndTexture,
				_ => tapTexture
			};

            //设置Multimesh
			MultiMesh multiMesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
				InstanceCount = 0,
				VisibleInstanceCount = 0,
                UseColors = true, // 用于提示选中
			};
            multiMesh.InstanceCount = 10000;
			multiMeshes[type] = multiMesh;

			MultiMeshInstance2D multiMeshInstance = new MultiMeshInstance2D();
            multiMeshInstance.Texture = texture;
            multiMeshInstance.Multimesh = multiMesh;
			multiMeshInstances[type] = multiMeshInstance;

			// 根据纹理实际尺寸创建 QuadMesh
			var quad = new QuadMesh();
			quad.Size = new Vector2(texture.GetSize().X, -texture.GetSize().Y);   // 保持宽高比，去掉负值
			multiMeshInstance.Multimesh.Mesh = quad;

			AddChild(multiMeshInstance);
			multiMeshInstances[type] = multiMeshInstance;
            multiMeshes[type] = multiMesh;
		}

        // 设置inputController
        _inputController = new InputController();
        _inputController.PointerDown += OnButtonDown;
        _inputController.PointerUp += OnButtonUp;
        _inputController.PointerDrag += OnMotionInput;

        // 设置_boxSelectController
        _boxSelectController = new BoxSelectController();
        _boxSelectController.BoxUpdated += OnBoxUpdated;
        _boxSelectController.BoxEnded += OnBoxEnded;

        // 设置_coordinateConverter
        _coordComponent = new CoordinateComponent();

        //设置_dragPlaceComponent
        _dragPlaceComponent = new DragPlaceComponent();

    }

    public override void _ExitTree()
    {
        base._ExitTree();

        // 设置inputController
        _inputController.PointerDown -= OnButtonDown;
        _inputController.PointerUp -= OnButtonUp;
        _inputController.PointerDrag -= OnMotionInput;
        _inputController = null;

        // 设置_boxSelectController
        _boxSelectController.BoxUpdated -= OnBoxUpdated;
        _boxSelectController.BoxEnded -= OnBoxEnded;
        _boxSelectController = null;

        // 设置_coordinateConverter
        _coordComponent = null;
    }



	protected override void UpdateVisuals()
    {
        // 如果没有可用的谱面或判定线，则隐藏所有池节点
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Length)
		{
			HideAllNodes();
			return;
		}

		List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
		if(notes == null)
		{
			HideAllNodes();
			return;
		}

        //同步_coordinateConverter
        _coordComponent.horMargin = horMargin;
        _coordComponent.verMargin = verMargin;
        _coordComponent.subBeatCount = subBeatCount;
        _coordComponent.verLineCount = verLineCount;
        _coordComponent.horOffsetSmoothed = horOffsetSmoothed;
        _coordComponent.horSeparationSmoothed = horSeparationSmoothed;
        _coordComponent.parentSize = Size;
		
		//归零可见数量
		foreach(SpriteType spriteType in allSpriteTypes)
		{
			visibleCounts[spriteType] = 0;
		}

		// ============= 渲染视口范围内的 note ============= 
		for (int i = 0; i < notes.Count; i++)
		{
			Note note = notes[i];

            //选中效果
            Action<MultiMesh, int> renderEffect = null;
            if (selectedNotes.Contains(note))
            {
                renderEffect = SelectedRender;
            }
            //即将删除的高亮效果
            if (notesToDelete.Contains(note))
            {
                renderEffect = AboutToDeleteRender;
            }

            // 渲染note
            MultiMeshRenderNote(
                noteType: (NoteType)note.Type,
                startBeat: new Beat(note.StartTime),
                endBeat: new Beat(note.EndTime),
                chartPosX: note.PositionX,
                renderEffect: renderEffect
            );

        }

        // 额外绘制即将创建的Hold
        if(_dragPlaceComponent.IsDragging){
            float chartPosX = -675 + _dragPlaceComponent.verLineIndex * (1350f / (verLineCount - 1));
            MultiMeshRenderNote(
                noteType: NoteType.Hold,
                startBeat: _dragPlaceComponent.startBeat,
                endBeat: _dragPlaceComponent.endBeat,
                chartPosX: chartPosX,
                renderEffect: NoteToAddRender
            );
        }

        // 更新所有 MultiMesh 的可见实例数量
        foreach (SpriteType type in allSpriteTypes)
        {
            multiMeshes[type].VisibleInstanceCount = visibleCounts[type];
        }

    }

    private void MultiMeshRenderNote(NoteType noteType, Beat startBeat, Beat endBeat, float chartPosX, Action<MultiMesh, int> renderEffect)
    {
        if(noteType == NoteType.Hold)
        {
            MultiMeshRenderHold(startBeat, endBeat, chartPosX, renderEffect);
            return;
        }
        
        // 计算起始拍数
        float startBeatValue = startBeat[0] + startBeat[1] * 1f / startBeat[2];
        Vector2 panelPos = _coordComponent.GetPanelPosition(chartPosX, startBeatValue);
        float panelX = panelPos.X;
        float startY = panelPos.Y;

        // 裁切：超出面板范围则不渲染
        if (panelX < 0 || panelX > Size.X || startY < 0 || startY > Size.Y) return;

        SpriteType type = noteType switch
        {
            NoteType.Tap => SpriteType.Tap,
            NoteType.Flick => SpriteType.Flick,
            NoteType.Drag => SpriteType.Drag,
            _ => SpriteType.Tap
        };

        // 构建变换：位置 + 固定缩放
        Transform2D transform = Transform2D.Identity;
        transform.Origin = new Vector2(panelX, startY);
        transform.X = new Vector2(noteScale, 0);
        transform.Y = new Vector2(0, noteScale);

        multiMeshes[type].SetInstanceTransform2D(visibleCounts[type], transform);
        multiMeshes[type].SetInstanceColor(visibleCounts[type], Colors.White);

        // 渲染效果
        renderEffect?.Invoke(multiMeshes[type], visibleCounts[type]);

        visibleCounts[type]++;
    }

    private void MultiMeshRenderHold(Beat startBeat, Beat endBeat, float chartPosX, Action<MultiMesh, int> renderEffect)
    {
        // 计算起始拍数
        float startBeatValue = startBeat[0] + startBeat[1] * 1f / startBeat[2];
        Vector2 panelPos = _coordComponent.GetPanelPosition(chartPosX, startBeatValue);
        float panelX = panelPos.X;
        float startY = panelPos.Y;

        // 计算结束拍数和结束 Y 坐标
        float endBeatValue = endBeat[0] + endBeat[1] * 1f / endBeat[2];
        float endY = _coordComponent.GetPanelPosY(endBeatValue);

        // 裁切：若头部和尾部都在面板外且不可见，则跳过（但若部分可见仍渲染）
        if (panelX < 0 || panelX > Size.X || startY < 0f || endY > Size.Y) return;

        // ---- 1. 渲染 Hold 头部 ----
        {
            Transform2D transform = Transform2D.Identity;
            transform.Origin = new Vector2(panelX, startY);
            transform.X = new Vector2(noteScale, 0);
            transform.Y = new Vector2(0, noteScale);
            multiMeshes[SpriteType.HoldHead].SetInstanceTransform2D(
                visibleCounts[SpriteType.HoldHead],
                transform
            );
            multiMeshes[SpriteType.HoldHead].SetInstanceColor(visibleCounts[SpriteType.HoldHead], Colors.White);
            
            // 渲染效果
            renderEffect?.Invoke(multiMeshes[SpriteType.HoldHead], visibleCounts[SpriteType.HoldEnd]);
            
            visibleCounts[SpriteType.HoldHead]++;
        }

        // ---- 2. 渲染 Hold 身体（拉伸条） ----
        {
            float bodyLength = startY - endY;   // 正数表示向下延伸
            
            float midY = (startY + endY) / 2f;
            // 计算 Y 方向缩放：长度 / 纹理高度（纹理高度可自定，这里假设为 1900，与原注释一致）
            float scaleY = bodyLength / holdBodyTexture.GetSize().Y;

            Transform2D transform = Transform2D.Identity;
            transform.Origin = new Vector2(panelX, midY);
            transform.X = new Vector2(noteScale, 0);
            transform.Y = new Vector2(0, scaleY);
            multiMeshes[SpriteType.HoldBody].SetInstanceTransform2D(
                visibleCounts[SpriteType.HoldBody], transform
            );
            multiMeshes[SpriteType.HoldBody].SetInstanceColor(visibleCounts[SpriteType.HoldBody], Colors.White);
            
            // 渲染效果
            renderEffect?.Invoke(multiMeshes[SpriteType.HoldBody], visibleCounts[SpriteType.HoldBody]);

            visibleCounts[SpriteType.HoldBody]++;
            
        }

        // ---- 3. 渲染 Hold 尾部 ----
        {
            Transform2D transform = Transform2D.Identity;
            transform.Origin = new Vector2(panelX, endY);
            transform.X = new Vector2(noteScale, 0);
            transform.Y = new Vector2(0, noteScale);
            multiMeshes[SpriteType.HoldEnd].SetInstanceTransform2D(
                visibleCounts[SpriteType.HoldEnd], transform
            );
            multiMeshes[SpriteType.HoldEnd].SetInstanceColor(visibleCounts[SpriteType.HoldEnd], Colors.White);
            
            // 渲染效果
            renderEffect?.Invoke(multiMeshes[SpriteType.HoldEnd], visibleCounts[SpriteType.HoldEnd]);

            visibleCounts[SpriteType.HoldEnd]++;
        }
    }

    public override void _Draw()
    {
        base._Draw();

        // ============= 绘制框选矩形 ============= 
        if(_boxSelectController.IsDragging){
            Vector2 pos = new Vector2(
                Mathf.Min(boxStartPos.X, boxEndPos.X),
                Mathf.Min(boxStartPos.Y, boxEndPos.Y)
            );
            Vector2 size = new Vector2(
                Mathf.Abs(boxStartPos.X - boxEndPos.X),
                Mathf.Abs(boxStartPos.Y - boxEndPos.Y)
            );
            Rect2 rect = new Rect2(pos, size);
            DrawRect(
                rect: rect,
                color: boxColor,
                filled: false,
                width: 5
            );

        }
    }


    private void SelectedRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, selectedModulate);
    }

    private void AboutToDeleteRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, deleteHighlightModulate);
    }

    private void NoteToAddRender(MultiMesh multiMesh, int id)
    {
        multiMesh.SetInstanceColor(id, toAddModulate);
    }

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);

        // 只处理左键和触摸，其余事件（滚轮、中键）忽略
        bool handled = false;
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }
        else if (@event is InputEventMouseMotion mouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }
        else if (@event is InputEventScreenTouch touch)
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }
        else if (@event is InputEventScreenDrag drag)
        {
            _inputController.ProcessEvent(@event);
            handled = true;
        }

        if (handled) AcceptEvent(); // 标记事件已处理，阻止向上冒泡
        
    }
    
    private void OnButtonDown(Vector2 pos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            
        }
        else if(EditModeManager.EditMode == EditModeEnum.PlacingNote)
        {
            if(PlacingNote != NoteType.Hold) // 放置普通note
            {
                float chartX = _coordComponent.GetChartPosX(pos.X);
                float snappedChartX = _coordComponent.SnapChartXToGrid(chartX);

                float beatValue = _coordComponent.GetBeatValue(pos.Y);
                Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

                NoteAddRequested?.Invoke(
                    PlacingNote,
                    snappedBeat,
                    snappedBeat,
                    snappedChartX
                );
            }
            else // 放置Hold
            {
                float chartX = _coordComponent.GetChartPosX(pos.X);
                int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

                float beatValue = _coordComponent.GetBeatValue(pos.Y);
                Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

                _dragPlaceComponent.StartDrag(verLineIndex, snappedBeat);
            }
        }
        else if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            Vector2 dataPos = new Vector2(
                _coordComponent.GetChartPosX(pos.X),
                _coordComponent.GetBeatValue(pos.Y)
            );
            _boxSelectController.StartDrag(dataPos);
        }
    }

    private void OnButtonUp(Vector2 pos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            int noteIndex = FildNearestNoteIndex(pos);
            if(noteIndex == -1) // -1代表没有选中
            {
                DeselectAll();
            }
            else
            {
                OnNoteTaped(noteIndex);
            }
        }
        else if(EditModeManager.EditMode == EditModeEnum.PlacingNote)
        {
            if (PlacingNote == NoteType.Hold)
            {
                float chartX = _coordComponent.GetChartPosX(pos.X);
                int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

                float beatValue = _coordComponent.GetBeatValue(pos.Y);
                Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

                _dragPlaceComponent.EndDrag(verLineIndex, snappedBeat);
            }
        }
        else if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            Vector2 dataPos = new Vector2(
                _coordComponent.GetChartPosX(pos.X),
                _coordComponent.GetBeatValue(pos.Y)
            );
            _boxSelectController.EndDrag(dataPos);
        }
    }

    private void OnMotionInput(Vector2 position, Vector2 relative)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            
        }
        else if(EditModeManager.EditMode == EditModeEnum.PlacingNote)
        {
            if (PlacingNote == NoteType.Hold)
            {
                float chartX = _coordComponent.GetChartPosX(position.X);
                int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

                float beatValue = _coordComponent.GetBeatValue(position.Y);
                Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

                _dragPlaceComponent.Move(verLineIndex, snappedBeat);
            }
        }
        else if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            Vector2 dataPos = new Vector2(
                _coordComponent.GetChartPosX(position.X),
                _coordComponent.GetBeatValue(position.Y)
            );
            _boxSelectController.Move(dataPos);
        }
    }

    public void OnNoteTaped(int noteIndex)
    {
        List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
        Note note = notes[noteIndex];
        
        if(selectMode == SelectMode.Single)
        {
            selectedNotes = [note];
            EmitSignal(SignalName.OnNoteSelected, EditingLineId, noteIndex);
        }
        else if(selectMode == SelectMode.Multi)
        {
            if (selectedNotes.Contains(note))
            {
                selectedNotes.Remove(note);
            }
            else
            {
                selectedNotes.Add(note);
            }
        }
        else
        {
            GD.PrintErr($"[{this.Name}] 未设置的选择模式:{selectMode}");
        }
    }

    /// <summary>
    /// 找到距离点击位置最近的note，若未找到返回null
    /// </summary>
    /// <param name="pos">点击位置</param>
    /// <returns>距离点击位置最近的note</returns>
    private Note FildNearestNote(Vector2 pos)
    {
        List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;

        int index = FildNearestNoteIndex(pos);

        if(index == -1)
        {
            return null;
        }

        return notes[index];
    }

    /// <summary>
    /// 找到距离点击位置最近的note，若未找到返回-1
    /// </summary>
    /// <param name="pos">点击位置</param>
    /// <returns>距离点击位置最近的note的索引</returns>
    private int FildNearestNoteIndex(Vector2 pos)
    {
        List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;

        int nearestNoteIndex = -1;
        float nearestDistSquared = 99999f;

        for (int i = 0; i < notes.Count; i++)
        {
            Note note = notes[i];

            float distSquared;
            if(note.Type != 2)
            {
                //计算note位置
                float beatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
                Vector2 notePos = _coordComponent.GetPanelPosition(note.PositionX, beatValue);

                distSquared = pos.DistanceSquaredTo(notePos);
            }
            else // 特殊处理hold
            {
                float startBeat = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
                float endBeat = note.EndTime[0] + note.EndTime[1] * 1f / note.EndTime[2];
                Vector2 startPos = _coordComponent.GetPanelPosition(note.PositionX, startBeat);
                Vector2 endPos = _coordComponent.GetPanelPosition(note.PositionX, endBeat);

                if(pos.Y < endPos.Y)
                {
                    //计算点击位置和结束点（最上方）的距离
                    distSquared = pos.DistanceSquaredTo(endPos);

                }
                else if(pos.Y > startPos.Y)
                {
                    //计算点击位置和开始点（最下方）的距离
                    distSquared = pos.DistanceSquaredTo(startPos);
                }
                else
                {
                    // 点击位置在hold两侧，计算水平距离
                    distSquared = (float)Math.Pow(pos.X - startPos.X, 2);
                }
            }

            if(distSquared < nearestDistSquared)
            {
                nearestDistSquared = distSquared;
                nearestNoteIndex = i;
            }
        }

        //判断距离是否小于阈值
        float distance = (float)Math.Sqrt(nearestDistSquared);
        if(distance > distanceThreshold)
        {
            GD.Print($"[{this.Name}] 点击位置:{pos}, 未选中, 距离过大:{distance}");
            return -1;
        }

        GD.Print($"[{this.Name}] 点击位置:{pos} 最近的note:{nearestNoteIndex}, 距离:{distance}");
        return nearestNoteIndex;

        
    }

    public Vector2 GetGlobalPosition(float beatValue, float posX)
    {
        Vector2 localPos = _coordComponent.GetPanelPosition(posX, beatValue);

        // 1. 获取视口（Viewport）的屏幕变换
        Transform2D screenTransform = GetViewport().GetScreenTransform();

        // 2. 获取节点自身的全局画布变换
        Transform2D globalCanvasTransform = GetGlobalTransformWithCanvas();

        // 3. 按顺序相乘：屏幕变换 * 全局画布变换 * 局部坐标
        Vector2 screenPos = screenTransform * (globalCanvasTransform * localPos);

        return screenPos;
    }

    public void DeselectAll()
    {
        selectedNotes.Clear();
    }


    private void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos)
    {
        boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
        boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

        if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            //检测范围内的note
            Rect2 rect = RectUtil.TwoPointsToRect(startDataPos, endDataPos); // 坐标系：(ChartPosX, BeatValue)

            List<int> notesIndex = GetNotesInRect(rect);

            notesToDelete.Clear();
            List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
            foreach(int i in notesIndex)
            {
                notesToDelete.Add(notes[i]);
            }

            
        }
    }

    private void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos)
    {
        boxStartPos = _coordComponent.GetPanelPosition(startDataPos.X, startDataPos.Y);
        boxEndPos = _coordComponent.GetPanelPosition(endDataPos.X, endDataPos.Y);

        if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            //检测范围内的note
            Rect2 rect = RectUtil.TwoPointsToRect(startDataPos, endDataPos); // 坐标系：(ChartPosX, BeatValue)
            List<int> notesToDeleteIndex = GetNotesInRect(rect);

            //转换为List<Note>数据格式
            List<Note> notesToDelete = new();
            List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
            foreach(int index in notesToDeleteIndex) notesToDelete.Add(notes[index]);

            //触发事件，请求删除note
            NoteDeleteRequested?.Invoke(EditingLineId, notesToDelete);

            //清除高亮显示
            this.notesToDelete.Clear();

        }
    }

    /// <summary>
    /// 获得矩形内的所有note（坐标系：(ChartPosX, BeatValue)）
    /// </summary>
    /// <param name="rect">矩形（坐标系：(ChartPosX, BeatValue)）</param>
    /// <returns></returns>
    private List<int> GetNotesInRect(Rect2 rect)
    {
        List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
        return RectUtil.GetNotesInRect(notes, rect);
    }

    

}
