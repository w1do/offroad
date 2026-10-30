using UnityEngine;

public sealed class MountainRoute : MonoBehaviour
{
    public Vector3[] points;
    public float length;

    public int Nearest(Vector3 position)
    {
        int best = 0;
        float distance = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float candidate = (points[i] - position).sqrMagnitude;
            if (candidate < distance) { best = i; distance = candidate; }
        }
        return best;
    }
}
