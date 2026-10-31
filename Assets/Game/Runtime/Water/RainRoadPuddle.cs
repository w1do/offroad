using UnityEngine;

// A shallow puddle over an authored physical depression; never a second solid ground collider.
[RequireComponent(typeof(WaterVolume))]
public sealed class RainRoadPuddle : MonoBehaviour
{
    public RainSoakedRoad road;
    public Transform surface;
    public Vector2 basinRadii = new Vector2(1.35f, 3);
    public float basinDepth = .08f;
    private WaterVolume water;
    private BoxCollider trigger;
    private Renderer visual;
    private Vector3 originalPosition;
    private float clock;
    private void OnEnable()
    {
        water = GetComponent<WaterVolume>(); trigger = GetComponent<BoxCollider>();
        visual = surface.GetComponent<Renderer>(); originalPosition = transform.localPosition;
        Refresh();
    }
    private void Update() { clock += Time.deltaTime; if (clock >= .5f) { clock = 0; Refresh(); } }
    public void Refresh()
    {
        if (water == null || road == null) return;
        float fill = Mathf.Clamp01(road.Saturation);
        bool visible = fill > .025f;
        visual.enabled = visible; trigger.enabled = visible;
        float height = -basinDepth * (1 - fill * .92f);
        transform.localPosition = originalPosition + Vector3.up * height;
        float radius = Mathf.Sqrt(fill * .92f);
        surface.localScale = new Vector3(basinRadii.x * radius, 1, basinRadii.y * radius);
        water.localSurfaceHeight = 0;
        water.BuildSurface();
    }
    private void OnDisable()
    {
        if (visual != null) visual.enabled = false;
        if (trigger != null) trigger.enabled = false;
        transform.localPosition = originalPosition;
    }
}
