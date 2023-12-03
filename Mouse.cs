using System;
using System.Runtime.InteropServices;
using SharpDX;
using Kalon;
using Vector2 = System.Numerics.Vector2;

namespace Stashie;

internal class Mouse
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point cursorPosition);

    private static Vector2 GetCursorPosition()
    {
        GetCursorPos(out var cursorPosition);

        return new Vector2(cursorPosition.X, cursorPosition.Y);
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
        Vector2 currentPosition = GetCursorPosition();
        
        float distance = Vector2.Distance(currentPosition, targetPosition);
        float normalizedDistance = NormalizeDistance(distance, maxInterpolationDistance);
        
        float interpolatedValue = Lerp(minInterpolationDelay, maxInterpolationDelay, normalizedDistance);

        TimeSpan mouseSpeed = TimeSpan.FromMilliseconds(interpolatedValue + Random.Shared.Next(25, 100));
        
        CursorMover.MoveCursor((int)targetPosition.X, (int)targetPosition.Y, mouseSpeed);
    }
}