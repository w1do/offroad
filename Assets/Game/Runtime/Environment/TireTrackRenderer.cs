using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(Renderer))]
public sealed class TireTrackRenderer : MonoBehaviour
{
    [Range(64, 512)] public int resolution = 256;
    public string persistentId;
    public float maximumUploadHz = 12;
    static readonly Dictionary<string, Color32[]> retained = new Dictionary<string, Color32[]>();
    Texture2D mask;
    Color32[] pixels;
    Bounds bounds;
    bool dirty;
    float uploadClock;
    Renderer target;
    MaterialPropertyBlock properties;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void NewSession() { retained.Clear(); }
    void OnEnable()
    {
        if (!Application.isPlaying) return;
        target = GetComponent<Renderer>(); bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        if (bounds.size.x < .01f || bounds.size.z < .01f) return;
        resolution = Mathf.Clamp(resolution, 64, 512); pixels = null;
        BindPersistence(persistentId); Upload();
    }
    void EnsureMask()
    {
        if(mask!=null)return;
        pixels=new Color32[resolution*resolution];
        mask=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,true){name="Persistent local tire tracks",wrapMode=TextureWrapMode.Clamp};
    }
    public void BindPersistence(string key)
    {
        persistentId = key;
        if (!string.IsNullOrEmpty(key) && retained.TryGetValue(key, out var saved) && saved.Length == resolution*resolution)
        { EnsureMask(); System.Array.Copy(saved, pixels, pixels.Length); dirty = true; Upload(); }
    }
    public float TrackAt(Vector3 world)
    {
        if(pixels==null)return 0;Vector3 p=transform.InverseTransformPoint(world);
        int x=Mathf.FloorToInt((p.x-bounds.min.x)/bounds.size.x*resolution),z=Mathf.FloorToInt((p.z-bounds.min.z)/bounds.size.z*resolution);
        return x<0||z<0||x>=resolution||z>=resolution?0:pixels[z*resolution+x].r/255f;
    }
    public void Stamp(Vector3 world, Vector3 forward, float width, float length, float phaseMetres, float repeatMetres, float wetness)
    {
        if (target == null) return;
        EnsureMask();
        Vector3 local = transform.InverseTransformPoint(world);
        Vector3 along = Vector3.ProjectOnPlane(forward, Vector3.up).normalized, across = Vector3.Cross(Vector3.up, along);
        if (along.sqrMagnitude < .5f) return;
        float reach = Mathf.Max(width, length);
        Vector3 localReach = transform.InverseTransformVector(Vector3.right * reach), localReachZ = transform.InverseTransformVector(Vector3.forward * reach);
        float rx = Mathf.Abs(localReach.x) + Mathf.Abs(localReachZ.x), rz = Mathf.Abs(localReach.z) + Mathf.Abs(localReachZ.z);
        int x0 = Mathf.Clamp(Mathf.FloorToInt((local.x - rx - bounds.min.x) / bounds.size.x * resolution), 0, resolution - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((local.x + rx - bounds.min.x) / bounds.size.x * resolution), 0, resolution - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((local.z - rz - bounds.min.z) / bounds.size.z * resolution), 0, resolution - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((local.z + rz - bounds.min.z) / bounds.size.z * resolution), 0, resolution - 1);
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
        {
            Vector3 p = transform.TransformPoint(new Vector3(bounds.min.x + (x + .5f) / resolution * bounds.size.x, local.y, bounds.min.z + (z + .5f) / resolution * bounds.size.z));
            Vector3 d = p - world; float side = Vector3.Dot(d, across) / Mathf.Max(.01f, width * .5f), longitudinal = Vector3.Dot(d, along);
            if (Mathf.Abs(side) > 1 || Mathf.Abs(longitudinal) > length * .5f) continue;
            float blocks = Mathf.Repeat((longitudinal + phaseMetres) / Mathf.Max(.02f, repeatMetres) + Mathf.Abs(side) * .65f, 1);
            byte tread = (byte)(blocks < .58f && Mathf.Abs(side) > .12f ? 255 : 45);
            int index = z * resolution + x; var old = pixels[index];
            pixels[index] = new Color32((byte)Mathf.Max(old.r, tread), (byte)Mathf.Max(old.g, Mathf.Clamp01(wetness) * 255), 0, 255); dirty = true;
        }
    }
    void LateUpdate() { uploadClock += Time.deltaTime; if (dirty && uploadClock >= 1 / Mathf.Max(1, maximumUploadHz)) Upload(); }
    void Upload()
    {
        if (mask == null || target == null) return;
        if(properties==null)properties=new MaterialPropertyBlock();
        mask.SetPixels32(pixels); mask.Apply(false, false); dirty = false; uploadClock = 0;
        target.GetPropertyBlock(properties); properties.SetTexture("_SoilTrackMask", mask); properties.SetFloat("_DynamicSoil", 1);
        properties.SetVector("_SoilMaskBounds", new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z)); target.SetPropertyBlock(properties);
    }
    void OnDisable()
    {
        if (pixels != null && !string.IsNullOrEmpty(persistentId)) retained[persistentId] = (Color32[])pixels.Clone();
        if (mask != null) Destroy(mask); mask = null;
        if(target!=null){if(properties==null)properties=new MaterialPropertyBlock();target.GetPropertyBlock(properties);properties.SetFloat("_DynamicSoil",0);target.SetPropertyBlock(properties);}
    }
}
