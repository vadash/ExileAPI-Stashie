using System;
using System.Runtime.InteropServices;
using SharpDX;
using Kalon;

namespace Stashie;

public class Mouse
{
    private enum MouseEvents
    {
        LeftDown = 0x00000002,
        LeftUp = 0x00000004,
        MiddleDown = 0x00000020,
        MiddleUp = 0x00000040,
        Move = 0x00000001,
        Absolute = 0x00008000,
        RightDown = 0x00000008,
        RightUp = 0x00000010
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point cursorPosition);

    public static Point GetCursorPosition()
    {
        GetCursorPos(out var cursorPosition);

        return cursorPosition;
    }
    
    private static float NormalizeDistance(float distance, float maxDistance)
    {
        return Math.Min(distance / maxDistance, 1.0f);
    }
    
    private static float Lerp(float startValue, float endValue, float interpolationFactor)
    {
        return (1 - interpolationFactor) * startValue + interpolationFactor * endValue;
    }
    
    public static void MoveMouse(Vector2 targetPosition, int maxInterpolationDistance = 700, int minInterpolationDelay = 0, int maxInterpolationDelay = 300)
    {
        Point currentPosition = GetCursorPosition();
        
        float distance = Vector2.Distance(currentPosition, targetPosition);
        float normalizedDistance = NormalizeDistance(distance, maxInterpolationDistance);
        
        float interpolatedValue = Lerp(minInterpolationDelay, maxInterpolationDelay, normalizedDistance);

        TimeSpan mouseSpeed = TimeSpan.FromMilliseconds(interpolatedValue + Random.Shared.Next(25, 100));
        
        CursorMover.MoveCursor((int)targetPosition.X, (int)targetPosition.Y, mouseSpeed);
    }
    
    [DllImport("user32.dll")]
    private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
    
    private static void LeftDown()
    {
        mouse_event((int)MouseEvents.LeftDown, 0, 0, 0, 0);
    }

    private static void LeftUp()
    {
        mouse_event((int)MouseEvents.LeftUp, 0, 0, 0, 0);
    }

    public static void LeftClick()
    {
        LeftDown();
        int delay = Random.Shared.Next(50, 150);
        LeftUp();
    }
}