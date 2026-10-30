using UnityEngine;
using UnityEngine.Rendering;

// Visual response only. VehicleWaterInteraction remains the owner of water forces.
[RequireComponent(typeof(VehiclePhysics), typeof(Rigidbody))]
public sealed class VehiclePuddleSplash : MonoBehaviour
{
    public PuddleEffectsProfile profile;
    public int EmittedDroplets { get; private set; }
    public int WetWheels { get; private set; }
    VehiclePhysics vehicle;
    Rigidbody body;
    ParticleSystem[] emitters;
    float[] carry;
    bool[] wet;

    void Start()
    {
        vehicle = GetComponent<VehiclePhysics>(); body = GetComponent<Rigidbody>();
        if (!vehicle.Ready) vehicle.Initialize();
        if (profile == null || profile.dropletMaterial == null) return;
        emitters = new ParticleSystem[vehicle.Wheels.Length];
        carry = new float[emitters.Length]; wet = new bool[emitters.Length];
        for (int i = 0; i < emitters.Length; i++)
        {
            var root = new GameObject("Puddle spray wheel " + i);
            root.transform.SetParent(transform, false);
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = profile.maximumParticlesPerWheel; main.startSpeed = 0; main.gravityModifier = 1;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.65f, .65f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var collision = ps.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.World;
            collision.quality = ParticleSystemCollisionQuality.Low; collision.lifetimeLoss = 1; collision.colliderForce = 0;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = profile.dropletMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = .045f; renderer.lengthScale = 1.4f;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            ps.Play(); emitters[i] = ps;
        }
    }

    void FixedUpdate()
    {
        WetWheels = 0;
        if (emitters == null || !vehicle.Ready) return;
        for (int i = 0; i < emitters.Length; i++)
        {
            vehicle.GetWheelPose(i, out var centre, out _);
            Vector3 bottom = centre - Vector3.up * vehicle.Wheels[i].radius;
            if (!WaterVolume.Sample(bottom, out var water, out float depth)) { wet[i] = false; carry[i] = 0; continue; }
            WetWheels++;
            Vector3 velocity = body.GetPointVelocity(centre) - water.Flow;
            Vector3 tangent = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float speed = tangent.magnitude;
            if (speed < profile.minimumSpeedMetresPerSecond) { wet[i] = true; carry[i] = 0; continue; }
            float immersion = Mathf.Clamp01(depth / Mathf.Max(.01f, profile.fullSprayDepthMetres));
            carry[i] += Mathf.Min(profile.maximumParticlesPerSecond, speed * profile.particlesPerMetre) * immersion * Time.fixedDeltaTime;
            int count = Mathf.Min(32, Mathf.FloorToInt(carry[i])); carry[i] -= count;
            if (!wet[i]) count += Mathf.CeilToInt(8 * immersion); // Bow splash on entry, including coasting.
            wet[i] = true;
            Vector3 forward = tangent / speed, side = Vector3.Cross(Vector3.up, forward);
            float throwSpeed = Mathf.Min(profile.maximumThrowSpeedMetresPerSecond, speed * .65f);
            Vector3 surface = bottom + Vector3.up * (depth + .025f);
            for (int n = 0; n < count; n++)
            {
                // Two outward sheets plus scattered droplets, with ballistic gravity.
                float sign = (n & 1) == 0 ? -1 : 1;
                var particle = new ParticleSystem.EmitParams {
                    position = surface + side * Random.Range(-.12f, .12f),
                    velocity = velocity * .3f + side * sign * throwSpeed * Random.Range(.35f, .95f)
                        + forward * throwSpeed * Random.Range(-.4f, .35f)
                        + Vector3.up * Mathf.Min(4.5f, .4f + throwSpeed * .55f) * Random.Range(.45f, 1),
                    startSize = Random.Range(profile.dropletSizeMetres.x, profile.dropletSizeMetres.y),
                    startLifetime = Random.Range(.35f, .95f), startColor = profile.waterColour
                };
                emitters[i].Emit(particle, 1); EmittedDroplets++;
            }
        }
    }
    void OnDisable()
    {
        if (emitters == null) return;
        foreach (var emitter in emitters) if (emitter != null) emitter.Clear();
        System.Array.Clear(wet, 0, wet.Length); System.Array.Clear(carry, 0, carry.Length);
    }
    void OnDestroy()
    {
        if (emitters != null) foreach (var emitter in emitters) if (emitter != null) Destroy(emitter.gameObject);
    }
}
