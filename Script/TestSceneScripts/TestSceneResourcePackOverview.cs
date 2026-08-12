using Godot;
using System;

public partial class TestSceneResourcePackOverview : Node
{
    [Export] private ResourcePackOverview overview;

    [Export] private string packPath;

    public override void _Ready()
    {
        base._Ready();

        ResourcePack pack = ResourcePackLoader.LoadFromZip(packPath);

        overview.Show(pack);
    }

}
