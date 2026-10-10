using Godot;

public interface IShareHelper
{
    public void ShareText(string title, string subject, string content);

    public void ShareImage(string fullPath, string title, string subject, string content);

    public void ShareViewport(Viewport viewport, string title, string subject, string content);

    public void ShareTexture(Texture2D texture, string title, string subject, string content);

    public void ShareFile(string path, string mimeType, string title, string subject, string content);
}
