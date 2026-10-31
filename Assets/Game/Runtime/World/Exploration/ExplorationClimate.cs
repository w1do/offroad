using UnityEngine;

// Spatial weather fields are independent of the player's camera and of the order tiles are visited.
public static class ExplorationClimate
{
    public static Vector2 Sample(Vector2 position, int adventure, float elapsed)
    {
        float drift=.5f+.5f*Mathf.Sin(elapsed/95+position.x*.0007f+position.y*.0005f);
        float rain=adventure==1 ? Mathf.Lerp(.78f,1,drift) : adventure==3||adventure==6 ? Mathf.Lerp(.35f,.7f,drift) : Mathf.Lerp(0,.22f,drift);
        float wind=adventure==2 ? Mathf.Lerp(1.8f,2.6f,drift) : Mathf.Lerp(.5f,1.25f,rain);
        return new Vector2(rain,wind);
    }
}

