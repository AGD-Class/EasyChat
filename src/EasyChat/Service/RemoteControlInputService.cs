using System.Drawing;
using System.Runtime.InteropServices;
using EasyChat.Models;

namespace EasyChat.Service;

public static class RemoteControlInputService
{
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void Apply(MsgModel msgModel)
    {
        switch (msgModel.screenControlEvent)
        {
            case MqttContent.SCREEN_CONTROL_MOUSE_MOVE:
                MoveCursor(msgModel.screenControlX, msgModel.screenControlY);
                break;
            case MqttContent.SCREEN_CONTROL_MOUSE_DOWN:
                MoveCursor(msgModel.screenControlX, msgModel.screenControlY);
                mouse_event(GetMouseFlag(msgModel.screenControlMouseButton, true), 0, 0, 0, UIntPtr.Zero);
                break;
            case MqttContent.SCREEN_CONTROL_MOUSE_UP:
                MoveCursor(msgModel.screenControlX, msgModel.screenControlY);
                mouse_event(GetMouseFlag(msgModel.screenControlMouseButton, false), 0, 0, 0, UIntPtr.Zero);
                break;
            case MqttContent.SCREEN_CONTROL_MOUSE_WHEEL:
                MoveCursor(msgModel.screenControlX, msgModel.screenControlY);
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)msgModel.screenControlDelta), UIntPtr.Zero);
                break;
            case MqttContent.SCREEN_CONTROL_KEY_DOWN:
                if (msgModel.screenControlKey is > 0 and <= byte.MaxValue)
                {
                    keybd_event((byte)msgModel.screenControlKey, 0, 0, UIntPtr.Zero);
                }
                break;
            case MqttContent.SCREEN_CONTROL_KEY_UP:
                if (msgModel.screenControlKey is > 0 and <= byte.MaxValue)
                {
                    keybd_event((byte)msgModel.screenControlKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                break;
        }
    }

    private static void MoveCursor(double normalizedX, double normalizedY)
    {
        var bounds = GetVirtualScreenBounds();
        var x = bounds.Left + (int)(Math.Clamp(normalizedX, 0, 1) * Math.Max(1, bounds.Width - 1));
        var y = bounds.Top + (int)(Math.Clamp(normalizedY, 0, 1) * Math.Max(1, bounds.Height - 1));
        SetCursorPos(x, y);
    }

    private static uint GetMouseFlag(int button, bool isDown)
    {
        return button switch
        {
            1 => isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            2 => isDown ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP
        };
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        return new Rectangle(
            GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_CYVIRTUALSCREEN));
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
