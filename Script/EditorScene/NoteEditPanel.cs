using Godot;
using QuickType;
using System;
using System.Collections.Generic;


public partial class NoteEditPanel : BaseEditPanel
{
	private enum SpriteType
	{
		Tap, Drag, Flick, HoldHead, HoldBody, HoldEnd
	}
	private readonly SpriteType[] allSpriteTypes = (SpriteType[])Enum.GetValues(typeof(SpriteType));

    

	[Export] private float noteScale = 0.1f;
    
    /// <summary> 被选中时的颜色滤镜 </summary>
    [Export] private Color selectedModulate = new Color(1f, 0.223f, 0.947f, 1f);
    [Export] private Color deleteHighlightModulate = new Color(1f, 0.184f, 0, 1f);
    [Export] private Color toAddModulate = new Color(1f, 1f, 1f, 0.588f);
    

	[ExportGroup("音符贴图")]
    [Export] private Texture2D tapTexture;
    [Export] private Texture2D dragTexture;
    [Export] private Texture2D flickTexture;
    [Export] private Texture2D holdHeadTexture;
    [Export] private Texture2D holdBodyTexture;
    [Export] private Texture2D holdEndTexture;

	// ---- Multimesh ---- 
	// private Dictionary<SpriteType, MultiMesh> multiMeshes = new();
	// private Dictionary<SpriteType, MultiMeshInstance2D> multiMeshInstances = new();
	// private Dictionary<SpriteType, int> visibleCounts = new();

    // private List<Note> selectedNotes = new();
    // private List<Note> notesToDelete = new();

    private readonly HashSet<Note> selectedNotes = new();
    private readonly HashSet<Note> notesToDelete = new();

    /// <summary>
    /// 只读暴露给外部遍历，外部无法修改集合内容
    /// </summary>
    public IReadOnlyCollection<Note> SelectedNotes => selectedNotes;

    [Signal] public delegate void OnNoteSelectedEventHandler(int lineId, int noteIndex, Vector2 clickViewportPos);

    /// <summary>
    /// 用于通知有Note被多选了
    /// </summary>
    public event Action NoteMultiSelected;

    /// <summary>
    /// 请求添加一个note的事件，参数为(音符类型，起始Beat，结束Beat，谱面X坐标)
    /// </summary>
    public event Action<NoteType, Beat, Beat, float> NoteAddRequested;
    /// <summary>
    /// 请求删除note的事件，参数为(判定线编号，要删除的note的列表)
    /// </summary>
    public event Action<int, List<Note> > NoteDeleteRequested;
    /// <summary>
    /// 拖动开始/结束时触发，参数:(判定线编号，note对象)
    /// </summary>
    public event Action<int, Note> NoteDragStarted;
    public event Action<int, Note> NoteDragEnded;
    /// <summary>
    /// 当Note被移动时触发，参数:(判定线编号，note索引，ChartX)
    /// </summary>
    public event Action<int, int, float> NoteMoved;

    /// <summary>
    /// 当note的时间被修改时触发，参数:(判定线编号，note索引，新StartTime，新EndTime)
    /// </summary>
    public event Action<int, int, Beat, Beat> NoteTimeChanged;

    public NoteType PlacingNote { get; set; } // 正在放置的note

    private NoteClipBoard _noteClipBoard;
    private Beat _pasteTargetBeat;
    private Beat _pasteBeatDelta;
    private float _pasteTargetPosX;
    private float _pastePosXDelta;

    public override void _Ready()
    {
        base._Ready();

		//设置multiMesh
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

            string key = type switch
			{
				SpriteType.Tap => "Tap",
				SpriteType.Drag => "Drag",
				SpriteType.Flick => "Flick",
				SpriteType.HoldHead => "HoldHead",
				SpriteType.HoldBody => "HoldBody",
				SpriteType.HoldEnd => "HoldEnd",
				_ => "Tap"
			};

            int zIndex = type switch
			{
				SpriteType.Tap => 3,
				SpriteType.Drag => 2,
				SpriteType.Flick => 4,
				SpriteType.HoldHead => 1,
				SpriteType.HoldBody => 1,
				SpriteType.HoldEnd => 1,
				_ => 999
			};

            RegisterMultiMesh(key, texture, 1024, zIndex);
		}

        // ---- 订阅拖动事件 ----
        _dragMoveComponent.Moved += OnNoteDragMoved;
        _dragMoveComponent.Started += OnNoteDragStarted;
        _dragMoveComponent.Ended += OnNoteDragEnded;

    }

    public override void _ExitTree()
    {
        _dragMoveComponent.Moved -= OnNoteDragMoved;
        _dragMoveComponent.Started -= OnNoteDragStarted;
        _dragMoveComponent.Ended -= OnNoteDragEnded;

        base._ExitTree();
    }

    // ================ 公开方法 ================
    #region 公开方法
    public void DeselectAll()
    {
        selectedNotes.Clear();
    }
    
    public void StartPaste(NoteClipBoard noteClipBoard)
    {
        _isPasteMode = true;

        _noteClipBoard = noteClipBoard;

        GD.Print($"[{Name}] 正在粘贴Note: Line{noteClipBoard.SourceLineId} Beat:{noteClipBoard.SourceStartBeat}");
    }

    public void CancelPaste()
    {
        if(_isPasteMode == false) return;

        _isPasteMode = false;

        GD.Print($"[{Name}] 用户取消了粘贴");
    }

    #endregion

    // ================ 私有方法 ================

    protected override void RenderContent()
    {
        // 如果没有可用的谱面或判定线，则隐藏所有池节点
		if (editingChart == null || 
			editingChart.JudgeLineList == null || 
			editingLineId < 0 || 
			editingLineId >= editingChart.JudgeLineList.Count)
		{
			//HideAllNodes();
			return;
		}

		List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;

        

		// ============= 2. 渲染视口范围内的 note ============= 
        GetVisibleBeatRange(out float minBeat, out float maxBeat);

        // 额外绘制即将创建的Note
        if(_dragPlaceComponent.IsDragging){
            float chartPosX = -675 + _dragPlaceComponent.verLineIndex * (1350f / (VerLineCount - 1));
            if(PlacingNote == NoteType.Hold)
            {
                MultiMeshRenderNote(
                    noteType: NoteType.Hold,
                    startBeat: _dragPlaceComponent.StartBeat,
                    endBeat: _dragPlaceComponent.EndBeat,
                    chartPosX: chartPosX,
                    renderEffect: NoteToAddRender
                );
            }
            else // Tap Drag Flick
            {
                MultiMeshRenderNote(
                    noteType: PlacingNote,
                    startBeat: _dragPlaceComponent.EndBeat,
                    endBeat: _dragPlaceComponent.EndBeat,
                    chartPosX: chartPosX,
                    renderEffect: NoteToAddRender
                );
            }
        }

        // 绘制即将粘贴的note
        if(_isPasteMode && _noteClipBoard != null && _noteClipBoard.Notes != null && _noteClipBoard.Notes.Count != 0)
        {
            foreach(NoteSnapshot snapshot in _noteClipBoard.Notes)
            {
                if(_pasteBeatDelta == null) break;

                MultiMeshRenderNote(
                    noteType: (NoteType)snapshot.Type,
                    startBeat: new Beat(snapshot.StartTime) + _pasteBeatDelta,
                    endBeat: new Beat(snapshot.EndTime) + _pasteBeatDelta,
                    chartPosX: snapshot.PositionX + _pastePosXDelta,
                    renderEffect: NoteToAddRender
                );
            }
        }

        //绘制谱面中的note 
        if(notes != null)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                Note note = notes[i];

                // ---- 快速视口裁剪：利用预计算的 startSec 或 beat 值 ----
                float startBeat = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
                if (startBeat > maxBeat) continue; // 还在屏幕上方（更大 beat 在更上方）

                // 对于 Hold，需要判断尾部；对于非 Hold，若 startBeat < minBeat 则已掠过屏幕
                if (note.Type != 2 && startBeat < minBeat) continue;

                // Hold 的额外判断：若尾部也小于 minBeat，则完全不可见
                if (note.Type == 2)
                {
                    float endBeat = note.EndTime[0] + note.EndTime[1] * 1f / note.EndTime[2];
                    if (endBeat < minBeat) continue;
                }

                Action<MultiMesh, int> renderEffect = null;
                //选中效果
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
        }
		
    }

    private void MultiMeshRenderNote(NoteType noteType, Beat startBeat, Beat endBeat, float chartPosX, Action<MultiMesh, int> renderEffect)
    {
        if(noteType == NoteType.Hold)
        {
            MultiMeshRenderHold(startBeat, endBeat, chartPosX, renderEffect);
            return;
        }

        float localX = _coordComponent.GetPanelPosX(chartPosX);

        string key = noteType switch
        {
            NoteType.Tap => "Tap",
            NoteType.Drag => "Drag",
            NoteType.Flick => "Flick",
            _ => "Tap"
        };

        RenderObject(
            key: key,
            localX: localX,
            beat: startBeat,
            offset: Vector2.Zero,
            scale: noteScale,
            renderEffect: renderEffect
        );
    }

    private void MultiMeshRenderHold(Beat startBeat, Beat endBeat, float chartPosX, Action<MultiMesh, int> renderEffect)
    {

        float localX = _coordComponent.GetPanelPosX(chartPosX);

        // ---- 1. 渲染 Hold 头部 ----
        RenderObject(
            key: "HoldHead",
            localX: localX,
            beat: startBeat,
            offset: new Vector2(0, holdHeadTexture.GetSize().Y / 2f),
            scale: noteScale,
            renderEffect: renderEffect
        );

        // ---- 2. 渲染 Hold 身体（拉伸条） ----
        RenderLongObject(
            key: "HoldBody",
            localX: localX,
            startBeat: startBeat,
            endBeat: endBeat,
            offset: Vector2.Zero,
            scale: noteScale,
            renderEffect: renderEffect
        );

        // ---- 3. 渲染 Hold 尾部 ----
        RenderObject(
            key: "HoldEnd",
            localX: localX,
            beat: endBeat,
            offset: new Vector2(0, -holdHeadTexture.GetSize().Y / 2f),
            scale: noteScale,
            renderEffect: renderEffect
        );
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
    
    protected override void OnButtonDown(Vector2 pos)
    {
        if (_isPasteMode)
        {
            // 将点击位置吸附到网格点
            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);
            _pasteTargetBeat = snappedBeat;
            _pasteBeatDelta = _pasteTargetBeat - _noteClipBoard.SourceStartBeat;

            float chartX = _coordComponent.GetChartPosX(pos.X);
            float snappedChartX = _coordComponent.SnapChartXToGrid(chartX);
            _pasteTargetPosX = snappedChartX;
            _pastePosXDelta = _pasteTargetPosX - _noteClipBoard.SourcePosX;

            return;
        }


        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {
            int noteIndex = FindNearestNoteIndex(pos);
            if (noteIndex == -1)
            {
                // 没有命中，由 OnButtonUp 负责 DeselectAll
                return;
            }

            List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
            Note note = notes[noteIndex];

            // 只有已选中的对象才能拖动
            // if (!selectedNotes.Contains(note)) return;

            // ---- 判断拖动部位 ----
            DragMoveComponent.DragMode mode = DragMoveComponent.DragMode.Body;
            float initialChartX = note.PositionX;
            Beat initialBeat = new Beat(note.StartTime);
            
            if(note.Type == 2)
            {
                // hold音符，需要判断是否点击到了头/尾
                float headBeatValue = note.StartTime[0] + note.StartTime[1] * 1f / note.StartTime[2];
                float endBeatValue = note.EndTime[0] + note.EndTime[1] * 1f / note.EndTime[2];

                float headY = _coordComponent.GetPanelPosY(headBeatValue);
                float endY = _coordComponent.GetPanelPosY(endBeatValue);

                float panelX = _coordComponent.GetPanelPosX(note.PositionX);

                float distToHeadSquared = pos.DistanceSquaredTo(new Vector2(panelX, headY));
                float distToEndSquared = pos.DistanceSquaredTo(new Vector2(panelX, endY));

                if(distToHeadSquared < distanceThreshold * distanceThreshold || 
                    distToEndSquared < distanceThreshold * distanceThreshold)
                {
                    // // 成功开始拖动

                    bool dragTail = distToEndSquared < distToHeadSquared;
                    mode = dragTail ? DragMoveComponent.DragMode.Tail
                                    : DragMoveComponent.DragMode.Head;
                    initialBeat = dragTail ? new Beat(note.EndTime) : new Beat(note.StartTime);

                    // GD.Print($"开始滑动Hold音符:{noteIndex}, 拖动尾部:{_draggingHoldEnd}");
                }
                else
                {
                    // // 未点到头尾，不开始拖动
                    // _isDraggingNote = false;
                    // _draggingNoteIndex = -1;

                    // 没点头尾，不启动拖动
                    //GD.Print($"未选中Hold头尾, 不滑动Hold音符:{noteIndex}");
                    return;
                }
            }

            _dragMoveComponent.Start(note, mode, initialChartX, initialBeat);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {

            float chartX = _coordComponent.GetChartPosX(pos.X);
            int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            _dragPlaceComponent.StartDrag(verLineIndex, snappedBeat);

            _dragPlaceComponent.Mode = PlacingNote switch
            {
                NoteType.Hold => DragPlaceComponent.PlaceMode.LongStraight,
                _ => DragPlaceComponent.PlaceMode.Point,
            };

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

    protected override void OnMotionInput(Vector2 pos, Vector2 relative)
    {
        if (_isPasteMode)
        {
            // 将点击位置吸附到网格点
            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            if(snappedBeat != _pasteTargetBeat)
            {
                _pasteTargetBeat = snappedBeat;
                _pasteBeatDelta = _pasteTargetBeat - _noteClipBoard.SourceStartBeat;
            }

            float chartX = _coordComponent.GetChartPosX(pos.X);
            float snappedChartX = _coordComponent.SnapChartXToGrid(chartX);

            if(snappedChartX != _pasteTargetPosX)
            {
                _pasteTargetPosX = snappedChartX;
                _pastePosXDelta = _pasteTargetPosX - _noteClipBoard.SourcePosX;
            }

            return;
        }

        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {

            // InputController 负责阈值判断；超过阈值后 IsDragging 才为 true
            if (!_inputController.IsDragging || !_dragMoveComponent.IsDragging)
                return;

            // Note 允许 X + Y 同时移动
            _dragMoveComponent.Update(pos, _coordComponent, allowX: true, allowY: true);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            float chartX = _coordComponent.GetChartPosX(pos.X);
            int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            _dragPlaceComponent.Move(verLineIndex, snappedBeat);
            
        }
        else if(EditModeManager.EditMode == EditModeEnum.Delete)
        {
            Vector2 dataPos = new Vector2(
                _coordComponent.GetChartPosX(pos.X),
                _coordComponent.GetBeatValue(pos.Y)
            );
            _boxSelectController.Move(dataPos);
        }
    }

    protected override void OnButtonUp(Vector2 pos)
    {
        if(EditModeManager.EditMode == EditModeEnum.Normal)
        {

            if (_inputController.IsDragging)
            {
                _dragMoveComponent.End();
                return; // 拖动结束，不触发点击选择
            }

            // 点击选择逻辑
            int noteIndex = FindNearestNoteIndex(pos);
            if (noteIndex == -1) DeselectAll();
            else OnNoteTapped(noteIndex, pos);
        }
        else if(EditModeManager.EditMode == EditModeEnum.Place)
        {
            float chartX = _coordComponent.GetChartPosX(pos.X);
            int verLineIndex = _coordComponent.SnapChartXToVerLine(chartX);

            float beatValue = _coordComponent.GetBeatValue(pos.Y);
            Beat snappedBeat = _coordComponent.SnapBeatValueToGrid(beatValue);

            _dragPlaceComponent.EndDrag(verLineIndex, snappedBeat);
            
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

    public void OnNoteTapped(int noteIndex, Vector2 localPos)
    {
        List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
        Note note = notes[noteIndex];
        
        if(SelectMode == SelectModeEnum.Single)
        {
            selectedNotes.Clear();
            selectedNotes.Add(note);

            //坐标转换 本地坐标 -> viewport坐标
            Vector2 viewportPos = GetGlobalTransformWithCanvas() * localPos;
		    Vector2 popupPos = viewportPos + new Vector2(30, 30);
            EmitSignal(SignalName.OnNoteSelected, EditingLineId, noteIndex, popupPos);
        }
        else if(SelectMode == SelectModeEnum.Multi)
        {
            if (selectedNotes.Contains(note))
            {
                selectedNotes.Remove(note);
            }
            else
            {
                selectedNotes.Add(note);
            }

            NoteMultiSelected?.Invoke();
        }
        else
        {
            GD.PrintErr($"[{this.Name}] 未设置的选择模式:{SelectMode}");
        }
    }

    private void OnNoteDragStarted(object targetId, DragMoveComponent.DragMode mode)
    {
        if (targetId is not Note note) return;
        NoteDragStarted?.Invoke(editingLineId, note);
    }

    private void OnNoteDragEnded(object targetId, DragMoveComponent.DragMode mode)
    {
        if (targetId is not Note note) return;
        NoteDragEnded?.Invoke(editingLineId, note);
    }

    // -------- 拖动响应 --------
    private void OnNoteDragMoved(object targetId, DragMoveComponent.DragMode mode,
                                 float newChartX, Beat newBeat)
    {
        Note note = (Note)targetId;
        List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;
        int noteIndex = notes.IndexOf(note);

        // X 变化
        if (!Mathf.IsEqualApprox(newChartX, note.PositionX))
        {
            NoteMoved?.Invoke(editingLineId, noteIndex, newChartX);
        }

        // 时间变化
        if (note.Type == 2) // Hold
        {
            if (mode == DragMoveComponent.DragMode.Head)
                NoteTimeChanged?.Invoke(editingLineId, noteIndex, newBeat, new Beat(note.EndTime));
            else if (mode == DragMoveComponent.DragMode.Tail)
                NoteTimeChanged?.Invoke(editingLineId, noteIndex, new Beat(note.StartTime), newBeat);
            else // Body：整体偏移，保持时长不变（如需此功能可在此扩展）
                NoteTimeChanged?.Invoke(editingLineId, noteIndex, newBeat,
                    newBeat + (new Beat(note.EndTime) - new Beat(note.StartTime)));
        }
        else
        {
            NoteTimeChanged?.Invoke(editingLineId, noteIndex, newBeat, newBeat);
        }
    }

    /// <summary>
    /// 找到距离点击位置最近的note，若未找到返回-1
    /// </summary>
    /// <param name="pos">点击位置，坐标系：Control本地坐标</param>
    /// <returns>距离点击位置最近的note的索引</returns>
    private int FindNearestNoteIndex(Vector2 pos)
    {
        List<Note> notes = editingChart.JudgeLineList[editingLineId].Notes;

        int nearestNoteIndex = -1;
        float nearestDistSquared = 99999f;

        if(notes == null || notes.Count == 0) return -1;

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
            // GD.Print($"[{this.Name}] 点击位置:{pos}, 未选中, 距离过大:{distance}");
            return -1;
        }

        // GD.Print($"[{this.Name}] 点击位置:{pos} 最近的note:{nearestNoteIndex}, 距离:{distance}");
        return nearestNoteIndex;

        
    }


    protected override void OnBoxUpdated(Vector2 startDataPos, Vector2 endDataPos)
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

    protected override void OnBoxEnded(Vector2 startDataPos, Vector2 endDataPos)
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

    protected override void OnDragEnded(int verLineIndex, Beat startBeat, Beat endBeat)
    {
        float chartPosX = -675 + verLineIndex * (1350f / (VerLineCount - 1));

        if(PlacingNote == NoteType.Hold)
        {
            NoteAddRequested?.Invoke(NoteType.Hold, startBeat, endBeat, chartPosX);
        }
        else
        {
            NoteAddRequested?.Invoke(PlacingNote, endBeat, endBeat, chartPosX);
        }
        
    }

}
