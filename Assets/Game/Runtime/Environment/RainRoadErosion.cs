using UnityEngine;

// Downpour runoff cuts shallow drainage gullies through the same visible and physical road mesh.
[RequireComponent(typeof(RainSoakedRoad), typeof(SoftGroundRuts))]
public sealed class RainRoadErosion : MonoBehaviour
{
    public Vector3[] runoffPoints;
    public float maximumWashoutDepth = .07f;
    public float erosionIntensity = .055f;
    public float WashedDepth { get; private set; }
    private RainSoakedRoad road;
    private SoftGroundRuts ground;
    private float clock;
    private void OnEnable() { road = GetComponent<RainSoakedRoad>(); ground = GetComponent<SoftGroundRuts>(); }
    private void FixedUpdate()
    {
        clock += Time.fixedDeltaTime;
        if (clock < .5f) return;
        Advance(clock); clock = 0;
    }
    public void Advance(float seconds)
    {
        if (road == null || road.weather == null || runoffPoints == null) return;
        float runoff = Mathf.InverseLerp(.65f, 1, road.RainIntensity) * Mathf.InverseLerp(.6f, 1, road.Saturation);
        if (runoff <= 0) return;
        foreach (var point in runoffPoints)
        {
            float depth = ground.DepthAt(point);
            float remaining = Mathf.Max(0, maximumWashoutDepth - depth);
            if (remaining <= 0) continue;
            float dt = Mathf.Min(Mathf.Max(0, seconds), remaining / Mathf.Max(.0001f, ground.profile.diggingMetresPerSecond * erosionIntensity * runoff));
            ground.Dig(point, erosionIntensity * runoff, dt);
            WashedDepth = Mathf.Max(WashedDepth, ground.DepthAt(point));
        }
    }
}

