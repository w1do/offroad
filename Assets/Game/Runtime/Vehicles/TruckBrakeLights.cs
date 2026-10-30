using UnityEngine;

public sealed class TruckBrakeLights : MonoBehaviour
{
    public ForestKamazController controller;
    public Renderer[] lamps;
    public Light[] lights;
    public bool Illuminated { get; private set; }
    private MaterialPropertyBlock properties;
    private VehiclePhysics physics;
    private void OnEnable(){physics=GetComponent<VehiclePhysics>();}

    private void LateUpdate()
    {
        Illuminated = physics!=null&&physics.enabled ? physics.ServiceBrakeApplied : controller != null && controller.BrakeApplied;
        properties ??= new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", Illuminated ? new Color(1, 0.03f, 0.01f) : new Color(0.22f, 0.01f, 0.005f));
        properties.SetColor("_EmissionColor", Illuminated ? new Color(5, 0.06f, 0.01f) : Color.black);
        foreach (var lamp in lamps) if (lamp != null) lamp.SetPropertyBlock(properties);
        foreach (var light in lights) if (light != null) light.enabled = Illuminated;
    }
}
