using UnityEngine;

// Invoked by VehiclePhysics: no independent FixedUpdate and no second engine controller.
[RequireComponent(typeof(VehiclePhysics), typeof(Rigidbody))]
public sealed class VehicleWaterInteraction : MonoBehaviour
{
    public VehicleWaterProfile profile;
    public bool IntakeFlooded { get; private set; }
    public float IntakeExposureSeconds { get; private set; }
    public float MaximumDepthMetres { get; private set; }
    public Vector3 LastForceNewtons { get; private set; }
    Rigidbody body;
    void OnEnable() { body = GetComponent<Rigidbody>(); }
    public static Vector3 SampleForce(Vector3 velocity, Vector3 flow, float depth,
        WaterBodySample sample, float density, float dragCoefficient, float gravity, float dt, float supportedMass, float maximumForce)
    {
        float wet = Mathf.Clamp01(depth / Mathf.Max(.01f, sample.fullSubmersionMetres));
        Vector3 relative = flow - velocity;
        Vector3 drag = .5f * Mathf.Max(0, density) * Mathf.Max(0, dragCoefficient)
            * Mathf.Max(0, sample.dragAreaSquareMetres) * wet * relative.magnitude * relative;
        drag = Vector3.ClampMagnitude(drag, Mathf.Max(0, supportedMass) * relative.magnitude / Mathf.Max(.001f, dt));
        Vector3 buoyancy = Vector3.up * Mathf.Max(0, density) * Mathf.Abs(gravity)
            * Mathf.Max(0, sample.displacedVolumeCubicMetres) * wet;
        return Vector3.ClampMagnitude(drag + buoyancy, Mathf.Max(0, maximumForce));
    }
    public void Simulate(float dt)
    {
        LastForceNewtons = Vector3.zero; MaximumDepthMetres = 0;
        if (profile == null || body == null || body.isKinematic || dt <= 0) return;
        int count = profile.bodySamples == null ? 0 : profile.bodySamples.Length;
        for (int i = 0; i < count; i++)
        {
            var sample = profile.bodySamples[i]; Vector3 point = transform.TransformPoint(sample.localPosition);
            if (!WaterVolume.Sample(point, out var water, out float depth)) continue;
            MaximumDepthMetres = Mathf.Max(MaximumDepthMetres, depth);
            Vector3 force = SampleForce(body.GetPointVelocity(point), water.Flow, depth, sample,
                water.densityKgPerCubicMetre, water.dragCoefficient, Physics.gravity.magnitude, dt,
                body.mass / Mathf.Max(1, count), profile.maximumForcePerSampleNewtons);
            body.AddForceAtPosition(force, point); LastForceNewtons += force;
        }
        if (WaterVolume.Sample(transform.TransformPoint(profile.intakeLocalPosition), out _, out _))
            IntakeExposureSeconds += dt;
        else IntakeExposureSeconds = Mathf.Max(0, IntakeExposureSeconds - dt);
        if (IntakeExposureSeconds >= Mathf.Max(.01f, profile.intakeFloodSeconds)) IntakeFlooded = true;
    }
    public void ResetAfterRecovery() { IntakeFlooded = false; IntakeExposureSeconds = 0; }
}
