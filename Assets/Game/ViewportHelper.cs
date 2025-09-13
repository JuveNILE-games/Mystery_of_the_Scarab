using UnityEngine;

public static class ViewportHelper
{
    public static Rect GetViewportRect(int index, int totalPlayers)
    {
        if (totalPlayers <= 1) return new Rect(0, 0, 1, 1);
        if (totalPlayers == 2)
        {
            if (index == 0) return new Rect(0f, 0f, 0.5f, 1f);
            else return new Rect(0.5f, 0f, 0.5f, 1f);
        }
        if (totalPlayers == 3)
        {
            if (index == 0) return new Rect(0f, 0.5f, 0.5f, 0.5f);
            if (index == 1) return new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            return new Rect(0f, 0f, 1f, 0.5f);
        }
        int row = index / 2;
        int col = index % 2;
        float w = 0.5f, h = 0.5f;
        return new Rect(col * w, 1f - (row + 1) * h, w, h);
    }
}
