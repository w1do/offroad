using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(VehiclePhysics))]
public sealed class VehicleWheelEffects : MonoBehaviour
{
    ParticleSystem[] clods, droplets;
    float[] clodCarry, dropletCarry;
    VehicleWheelEffectsProfile settings;
    Rigidbody body;
    readonly List<GameObject> roots = new List<GameObject>();
    readonly List<ParticleCollisionEvent> collisions = new List<ParticleCollisionEvent>();
    Transform[] marks;
    MeshRenderer[] markRenderers;
    float[] markExpiry;
    int nextMark;
    float markTokens;
    MaterialPropertyBlock properties;
    Mesh markMesh;
    public int EmittedParticles { get; private set; }
    public int EmittedClods { get; private set; }
    public int EmittedDroplets { get; private set; }
    public int ImpactMarks { get; private set; }
    public int WheelEmitterCount => clods == null ? 0 : clods.Length;

    void OnEnable()
    {
        if (!Application.isPlaying || clods != null) return;
        properties = new MaterialPropertyBlock();
        var car = GetComponent<VehiclePhysics>();
        if (!car.Ready) car.Initialize();
        settings = car.profile == null ? null : car.profile.wheelEffects;
        if (settings == null || settings.clodMaterial == null || settings.clodMeshes == null || settings.clodMeshes.Length == 0 || settings.splashMaterials == null || settings.splashMaterials.Length == 0) return;
        body = GetComponent<Rigidbody>();
        clods = new ParticleSystem[car.Wheels.Length]; droplets = new ParticleSystem[clods.Length];
        clodCarry = new float[clods.Length]; dropletCarry = new float[clods.Length];
        for (int i = 0; i < clods.Length; i++)
        {
            clods[i] = CreateEmitter("Mud clods " + i, true, i);
            droplets[i] = CreateEmitter("Wet mud spray " + i, false, i);
        }
        markMesh = new Mesh { name = "Mud impact quad" };
        markMesh.vertices = new[] { new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0) };
        markMesh.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up };
        markMesh.triangles = new[] { 0,2,1,0,3,2 }; markMesh.RecalculateNormals(); markMesh.RecalculateBounds();
        marks = new Transform[settings.maximumImpactMarks]; markRenderers = new MeshRenderer[marks.Length]; markExpiry = new float[marks.Length];
        for (int i = 0; i < marks.Length; i++) CreateMark(i);
    }

    void CreateMark(int slot)
    {
        var go = new GameObject("Mud impact " + slot); go.SetActive(false); marks[slot] = go.transform;
        go.AddComponent<MeshFilter>().sharedMesh = markMesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = settings.splashMaterials[slot % settings.splashMaterials.Length];
        renderer.shadowCastingMode = ShadowCastingMode.Off; markRenderers[slot] = renderer;
    }

    ParticleSystem CreateEmitter(string label, bool mesh, int wheel)
    {
        var go = new GameObject(label); go.transform.SetParent(transform, false); roots.Add(go);
        var system = go.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = settings.maximumParticlesPerLayer; main.gravityModifier = 1; main.startSpeed = 0; main.startLifetime = 1;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        var collision = system.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D; collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.bounce = .025f; collision.dampen = .85f; collision.lifetimeLoss = 1; collision.radiusScale = .25f;
        collision.enableDynamicColliders = true; collision.sendCollisionMessages = true;
        collision.colliderForce = 0; // Visual debris must never push the vehicle.
        var relay = go.AddComponent<VehicleMudImpactRelay>(); relay.owner = this; relay.particles = system;
        var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (mesh)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.SetMeshes(settings.clodMeshes);
            renderer.sharedMaterial = settings.clodMaterial;
            var rotation = system.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-5, 5); rotation.y = new ParticleSystem.MinMaxCurve(-8, 8); rotation.z = new ParticleSystem.MinMaxCurve(-4, 4);
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = .01f; renderer.lengthScale = .65f;
            renderer.sharedMaterial = settings.splashMaterials[wheel % settings.splashMaterials.Length];
            var colour = system.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },new[] { new GradientAlphaKey(.95f,0),new GradientAlphaKey(.85f,.7f),new GradientAlphaKey(0,1) }); colour.color = gradient;
        }
        system.Play(); return system;
    }

    public void Spray(int wheel, Vector3 point, Vector3 normal, Vector3 contactForward, float signedSpin, float pedal, float seconds, bool soft)
    {
        if (!isActiveAndEnabled || clods == null || wheel < 0 || wheel >= clods.Length || seconds <= 0) return;
        float spin = Mathf.Abs(signedSpin);
        if (!float.IsFinite(spin) || spin < settings.minimumSlipSpeed || pedal < .1f) return;
        Vector3 tangent = Vector3.ProjectOnPlane(contactForward, normal).normalized;
        if (tangent.sqrMagnitude < .5f) return;
        float intensity = (spin - settings.minimumSlipSpeed) * Mathf.Clamp01(pedal);
        int chunks = Count(ref clodCarry[wheel], Mathf.Min(intensity * settings.clodsPerSlipMetre, settings.maximumClodsPerSecond) * seconds);
        int spray = soft ? Count(ref dropletCarry[wheel], Mathf.Min(intensity * settings.dropletsPerSlipMetre, settings.maximumDropletsPerSecond) * seconds) : 0;
        Vector3 inherited = body == null ? Vector3.zero : body.GetPointVelocity(point);
        Vector3 throwDirection = -tangent * Mathf.Sign(signedSpin);
        float throwSpeed = Mathf.Min(settings.maximumThrowSpeed, .6f + spin * .42f);
        for (int i = 0; i < chunks + spray; i++)
        {
            bool chunk = i < chunks; Vector2 sizes = chunk ? settings.clodSizeMetres : settings.dropletSizeMetres;
            var particle = new ParticleSystem.EmitParams
            {
                position = point + normal * .075f + throwDirection * Random.Range(0, .08f),
                velocity = inherited * .65f + throwDirection * throwSpeed * Random.Range(.6f, 1.1f) + normal * Random.Range(.65f, Mathf.Min(4,1+spin*.16f)) + Vector3.Cross(normal,tangent) * Random.Range(-.7f,.7f),
                rotation3D = Random.insideUnitSphere * 180,
                startSize = Random.Range(sizes.x, sizes.y) * (soft ? 1 : .65f),
                startLifetime = Random.Range(.55f, 1.35f), startColor = soft ? settings.wetColour : settings.dryColour
            };
            (chunk ? clods[wheel] : droplets[wheel]).Emit(particle,1);
        }
        EmittedClods += chunks; EmittedDroplets += spray; EmittedParticles += chunks + spray;
    }

    static int Count(ref float remainder, float amount)
    {
        remainder += amount; int count = Mathf.Min(64, Mathf.FloorToInt(remainder)); remainder -= count; return count;
    }

    public void RecordImpacts(ParticleSystem system, GameObject other)
    {
        if (!isActiveAndEnabled || marks == null || other == null) return;
        int count = system.GetCollisionEvents(other, collisions);
        for (int i = 0; i < count && markTokens >= 1; i++)
        {
            var hit = collisions[i]; if (hit.normal.sqrMagnitude < .5f || hit.velocity.sqrMagnitude < .1f) continue;
            int slot = nextMark++ % marks.Length;
            // A streamed surface can destroy its child mark; recreate only when reused.
            if (marks[slot] == null) CreateMark(slot);
            var mark = marks[slot]; mark.SetParent(other.transform, false);
            mark.SetPositionAndRotation(hit.intersection + hit.normal * .008f, Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0,0,Random.Range(0,360)));
            float size = Random.Range(.045f,.18f); Vector3 scale = other.transform.lossyScale;
            mark.localScale = new Vector3(size/Mathf.Max(.001f,Mathf.Abs(scale.x)),size/Mathf.Max(.001f,Mathf.Abs(scale.y)),size/Mathf.Max(.001f,Mathf.Abs(scale.z)));
            properties.SetColor("_BaseColor",settings.wetColour); markRenderers[slot].SetPropertyBlock(properties);
            markExpiry[slot] = Time.time + settings.markLifetimeSeconds; mark.gameObject.SetActive(true);
            markTokens--; ImpactMarks++;
        }
    }

    void Update()
    {
        if (settings == null || marks == null) return;
        markTokens = Mathf.Min(settings.maximumMarksPerSecond,markTokens + Time.deltaTime * settings.maximumMarksPerSecond);
        for (int i = 0; i < marks.Length; i++)
        {
            if (marks[i] == null || !marks[i].gameObject.activeSelf) continue;
            float remaining = markExpiry[i] - Time.time;
            if (remaining <= 0) { marks[i].gameObject.SetActive(false); continue; }
            Color colour = settings.wetColour; colour.a *= Mathf.Clamp01(remaining / 6);
            properties.SetColor("_BaseColor",colour); markRenderers[i].SetPropertyBlock(properties);
        }
    }

    void OnDisable()
    {
        if (clods == null) return;
        foreach (var system in clods) if (system != null) system.Clear();
        foreach (var system in droplets) if (system != null) system.Clear();
        if (marks != null) foreach (var mark in marks) if (mark != null) mark.gameObject.SetActive(false);
        System.Array.Clear(clodCarry,0,clodCarry.Length); System.Array.Clear(dropletCarry,0,dropletCarry.Length);
    }

    void OnDestroy()
    {
        foreach (var root in roots) if (root != null) Destroy(root);
        if (marks != null) foreach (var mark in marks) if (mark != null) Destroy(mark.gameObject);
        if (markMesh != null) Destroy(markMesh);
    }
}

public sealed class VehicleMudImpactRelay : MonoBehaviour
{
    public VehicleWheelEffects owner;
    public ParticleSystem particles;
    void OnParticleCollision(GameObject other) { if (owner != null) owner.RecordImpacts(particles,other); }
}
