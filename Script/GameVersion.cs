using Godot;

public static class GameVersion
{
    public static string Version => 
        (string)ProjectSettings.GetSetting("application/config/version");
    
    public static string FullInfo => 
        $"v{Version} (Godot {Engine.GetVersionInfo()["string"]})";
}