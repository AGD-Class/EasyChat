namespace EasyChat.Models;

public sealed class ScreenShareResolutionOption
{
    public ScreenShareResolutionOption(string name, int width, int height)
    {
        Name = name;
        Width = width;
        Height = height;
    }

    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    public override string ToString()
    {
        return Name;
    }
}
