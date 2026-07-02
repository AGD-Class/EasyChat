namespace EasyChat.Models;

public sealed class ScreenControlInputEvent
{
    public string EventType { get; set; } = "";

    public double X { get; set; }

    public double Y { get; set; }

    public int MouseButton { get; set; }

    public int Key { get; set; }

    public int Delta { get; set; }
}
