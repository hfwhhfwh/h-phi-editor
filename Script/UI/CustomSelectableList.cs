using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CustomSelectableList : VBoxContainer, ICustomSelectableList
{
    public event Action<Control> ItemSelected;
    public event Action<Control> ItemDeselected;

    // ===== 内部数据结构 =====
    private sealed class Entry
    {
        public Control Content;      // 外部传入的控件
        public string Id;            // 唯一 id
        public HBoxContainer Row;   // 包装行（背景 + 命中区域）
        public CheckBox CheckBox;    // 多选方框
        public bool Selected;        // 唯一状态源
    }

    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, Entry> _byId = new();
    private readonly Dictionary<Control, Entry> _byContent = new();

    private bool _multiSelectMode;
    private int _autoIdCounter;

    private readonly StyleBoxFlat _selectedStyle = new()
    {
        BgColor = new Color(0.30f, 0.55f, 0.90f, 0.35f),
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    /// <summary>是否自动把加入的 Content 及子节点设为鼠标穿透，使整行点击都生效。</summary>
    [Export] public bool ContentClickThrough { get; set; } = true;

    /// <summary>移除条目时是否顺便释放 Content。默认 true。</summary>
    [Export] public bool FreeRemovedContent { get; set; } = true;

    // ===== 属性 =====
    public bool MultiSelectMode
    {
        get => _multiSelectMode;
        set
        {
            if (_multiSelectMode == value) return;
            _multiSelectMode = value;

            if (!value)
            {
                // 切到单选：若有多项选中，保留第一个，其余取消
                var selected = new List<Entry>();
                foreach (var e in _entries) if (e.Selected) selected.Add(e);
                for (int i = 1; i < selected.Count; i++)
                {
                    selected[i].Selected = false;
                    UpdateVisual(selected[i]);
                    ItemDeselected?.Invoke(selected[i].Content);
                }
            }

            foreach (var e in _entries)
            {
                e.CheckBox.Visible = value;
                e.CheckBox.SetPressedNoSignal(e.Selected);
            }
        }
    }

    public IEnumerable<Control> SelectedItems
    {
        get
        {
            var result = new List<Control>();
            foreach (var e in _entries) if (e.Selected) result.Add(e.Content);
            return result;
        }
    }

    public IEnumerable<string> SelectedIds
    {
        get
        {
            var result = new List<string>();
            foreach (var e in _entries) if (e.Selected) result.Add(e.Id);
            return result;
        }
    }

    public IEnumerable<int> SelectedIndices
    {
        get
        {
            var result = new List<int>();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected) result.Add(i);
            return result;
        }
    }

    // ===== 增删 =====
    public void AddItem(Control control) => AddItem(control, GenerateAutoId());

    public void AddItem(Control control, string id)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (id != null && _byId.ContainsKey(id))
            throw new ArgumentException($"已存在相同 id: {id}", nameof(id));

        control.GetParent()?.RemoveChild(control);

        var hbox = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        hbox.AddThemeConstantOverride("separation", 4);

        var checkBox = new CheckBox
        {
            Visible = _multiSelectMode,
            SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin
        };
        hbox.AddChild(checkBox);

        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(control);

        AddChild(hbox);

        var entry = new Entry
        {
            Content = control,
            Id = id,
            Row = hbox,
            CheckBox = checkBox,
        };

        hbox.GuiInput += evt =>
        {
            if (evt is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                if (_multiSelectMode) ToggleEntry(entry);
                else SelectEntry(entry);
                hbox.AcceptEvent();
            }
        };

        checkBox.Toggled += pressed => OnCheckBoxToggled(entry, pressed);

        if (ContentClickThrough) MakeContentClickThrough(control);

        _entries.Add(entry);
        if (id != null) _byId[id] = entry;
        _byContent[control] = entry;
    }

    public void Remove(Control control)
    {
        if (control != null && _byContent.TryGetValue(control, out var entry))
            RemoveEntry(entry);
    }

    public void Remove(string id)
    {
        if (id != null && _byId.TryGetValue(id, out var entry))
            RemoveEntry(entry);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        RemoveEntry(_entries[index]);
    }

    public void RemoveAll()
    {
        foreach (var entry in _entries)
        {
            DetachContentIfNeeded(entry);
            RemoveChild(entry.Row);
            entry.Row.QueueFree();
            if (entry.Selected) ItemDeselected?.Invoke(entry.Content);
        }
        _entries.Clear();
        _byId.Clear();
        _byContent.Clear();
    }

    // ===== 查询 =====
    public Control GetItem(int index)
    {
        if (index < 0 || index >= _entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _entries[index].Content;
    }

    public Control GetItem(string id)
        => id != null && _byId.TryGetValue(id, out var entry) ? entry.Content : null;

    public IEnumerable<Control> GetAllItems()
    {
        var result = new List<Control>(_entries.Count);
        foreach (var e in _entries) result.Add(e.Content);
        return result;
    }

    // ===== 选择操作 =====
    public void SelectItem(Control control)
    {
        if (control != null && _byContent.TryGetValue(control, out var entry))
            SelectEntry(entry);
    }

    public void SelectItem(string id)
    {
        if (id != null && _byId.TryGetValue(id, out var entry))
            SelectEntry(entry);
    }

    public void SelectAt(int index)
    {
        if (index < 0 || index >= _entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        SelectEntry(_entries[index]);
    }

    public void SelectAll()
    {
        if (_entries.Count == 0) return;
        if (_multiSelectMode)
        {
            foreach (var e in _entries) SelectEntry(e);
        }
        else
        {
            SelectEntry(_entries[0]); // 单选模式下等价于选中第一项
        }
    }

    public void DeselectItem(Control control)
    {
        if (control != null && _byContent.TryGetValue(control, out var entry))
            DeselectEntry(entry);
    }

    public void DeselectItem(string id)
    {
        if (id != null && _byId.TryGetValue(id, out var entry))
            DeselectEntry(entry);
    }

    public void DeselectAt(int index)
    {
        if (index < 0 || index >= _entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        DeselectEntry(_entries[index]);
    }

    public void DeselectAll()
    {
        foreach (var e in _entries)
        {
            if (!e.Selected) continue;
            e.Selected = false;
            UpdateVisual(e);
            ItemDeselected?.Invoke(e.Content);
        }
    }

    public bool IsSelected(Control control)
        => control != null && _byContent.TryGetValue(control, out var entry) && entry.Selected;

    public bool IsSelected(string id)
        => id != null && _byId.TryGetValue(id, out var entry) && entry.Selected;

    public bool IsSelectedAt(int index)
    {
        if (index < 0 || index >= _entries.Count) return false;
        return _entries[index].Selected;
    }

    // ===== 内部辅助 =====
    private void SelectEntry(Entry entry)
    {
        if (entry.Selected) return;

        if (!_multiSelectMode)
        {
            foreach (var e in _entries)
            {
                if (e == entry || !e.Selected) continue;
                e.Selected = false;
                UpdateVisual(e);
                ItemDeselected?.Invoke(e.Content);
            }
        }

        entry.Selected = true;
        UpdateVisual(entry);
        ItemSelected?.Invoke(entry.Content);
    }

    private void DeselectEntry(Entry entry)
    {
        if (!entry.Selected) return;
        entry.Selected = false;
        UpdateVisual(entry);
        ItemDeselected?.Invoke(entry.Content);
    }

    private void ToggleEntry(Entry entry)
    {
        if (entry.Selected) DeselectEntry(entry);
        else SelectEntry(entry);
    }

    private void OnCheckBoxToggled(Entry entry, bool pressed)
    {
        // 用户点击复选框；SetPressedNoSignal 在 UpdateVisual 中同步，不会递归
        if (pressed) SelectEntry(entry);
        else DeselectEntry(entry);
    }

    private void UpdateVisual(Entry entry)
    {
        entry.CheckBox.SetPressedNoSignal(entry.Selected);
        ApplySelectionVisual(entry.Row, entry.Content, entry.Selected);
    }

    /// <summary>选中视觉反馈，可子类重写。默认给 Row 加一个半透明高亮背景。</summary>
    protected virtual void ApplySelectionVisual(HBoxContainer row, Control content, bool selected)
    {
        if (selected) row.AddThemeStyleboxOverride("panel", _selectedStyle);
        else row.RemoveThemeStyleboxOverride("panel");
    }

    private void RemoveEntry(Entry entry)
    {
        bool wasSelected = entry.Selected;

        DetachContentIfNeeded(entry);
        RemoveChild(entry.Row);
        entry.Row.QueueFree();

        _entries.Remove(entry);
        _byContent.Remove(entry.Content);
        if (entry.Id != null) _byId.Remove(entry.Id);

        if (wasSelected) ItemDeselected?.Invoke(entry.Content);
    }

    private void DetachContentIfNeeded(Entry entry)
    {
        if (!FreeRemovedContent)
            entry.Content.GetParent()?.RemoveChild(entry.Content);
    }

    private static void MakeContentClickThrough(Node node)
    {
        if (node is Control c) c.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren()) MakeContentClickThrough(child);
    }

    private string GenerateAutoId() => $"__auto_{_autoIdCounter++}";
}