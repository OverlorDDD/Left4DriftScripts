using UnityEngine;

public static class SplitScreenLayout
{
    public static Rect GetViewport(int playerIndex, int totalPlayers)
    {
        switch (totalPlayers)
        {
            case 1:
                return new Rect(0, 0, 1, 1);
            case 2:
                return playerIndex == 0
                    ? new Rect(0f,   0f, 0.5f, 1f)
                    : new Rect(0.5f, 0f, 0.5f, 1f);
            case 3:
                if (playerIndex == 0) return new Rect(0f,    0.5f, 0.5f,  0.5f);
                if (playerIndex == 1) return new Rect(0.5f,  0.5f, 0.5f,  0.5f);
                return                       new Rect(0.25f, 0f,   0.5f,  0.5f);
            case 4:
                float x = (playerIndex % 2) * 0.5f;
                float y = (playerIndex < 2) ? 0.5f : 0f;
                return new Rect(x, y, 0.5f, 0.5f);
            default:
                return new Rect(0, 0, 1, 1);
        }
    }
}