using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stashie;

public class Keyboard
{
    private const int KeyEventFExtendedKey = 0x0001;
    private const int KeyEventFKeyUp = 0x0002;
    
    [DllImport("user32.dll")]
    private static extern uint keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    public static void KeyDown(Keys key)
    {
        keybd_event((byte)key, 0, KeyEventFExtendedKey | 0, 0);
    }

    public static void KeyUp(Keys key)
    {
        keybd_event((byte)key, 0, KeyEventFExtendedKey | KeyEventFKeyUp, 0);
    }
}