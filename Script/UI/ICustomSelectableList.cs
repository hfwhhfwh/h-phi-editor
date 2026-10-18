using Godot;
using System;
using System.Collections.Generic;

public interface ICustomSelectableList
{
    public bool MultiSelectMode { get; set; }
    public void AddItem(Control control);
    public void AddItem(Control control, string id);

    public void Remove(Control control);

    public void Remove(string id);

    public void RemoveAt(int index);

    public void RemoveAll();

    public Control GetItem(int index);
    public Control GetItem(string id);
    public IEnumerable<Control> GetAllItems();

    // ---- 多选 ----
    public void SelectItem(Control control);
    public void SelectItem(string id);
    public void SelectAt(int index);
    public void SelectAll();
    public void DeselectItem(Control control);
    public void DeselectItem(string id);
    public void DeselectAt(int index);
    public void DeselectAll();

    public bool IsSelected(Control control);
    public bool IsSelected(string id);
    public bool IsSelectedAt(int index);

    IEnumerable<Control> SelectedItems { get; }
    IEnumerable<string> SelectedIds { get; }
    IEnumerable<int> SelectedIndices { get; }

    // ---- 事件 ----
    public event Action<Control> ItemSelected;
    public event Action<Control> ItemDeselected;

}
