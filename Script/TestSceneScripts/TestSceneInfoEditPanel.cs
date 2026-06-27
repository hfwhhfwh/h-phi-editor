using Godot;
using System;

public partial class TestSceneInfoEditPanel : Node
{
    [Export] private InfoEditPanel infoEditPanel;

    public override void _Ready()
    {
        base._Ready();

        NoteType noteType = NoteType.Hold;

        InfoEditPanel.Data data = new InfoEditPanel.Data();
        data.Name = "Note_1234";
        data.Properties["StringTest"] = "string1234";
        data.Properties["IntTest"] = 123;
        data.Properties["FloatTest"] = 1.234f;
        data.Properties["BoolTest"] = true;
        data.Properties["EnumTest"] = noteType;

        infoEditPanel.ShowInfos(data);

        infoEditPanel.PropertyChanged += (string key, object value) =>
        {
            Type type = value.GetType();
            if (type.IsEnum)
            {
                GD.Print($"[{this.Name}] 用户修改了属性{key}:{Enum.ToObject(type, (int)value)}");
            }
            else
            {
                GD.Print($"[{this.Name}] 用户修改了属性{key}:{value}");
            }
        };
        
        infoEditPanel.OnConfirmed += () =>
        {
            GD.Print($"[{this.Name}] 用户按下了确定");
        };

    }

}
