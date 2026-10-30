using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class WheelRutContactSettings
{
    public bool enabled = true;
    public bool shoulderSupport = true;
    public bool longitudinalSupport;
    [Range(3,33)] public int longitudinalSamples = 9;
    public bool adaptiveLongitudinalSampling = true;
    [Range(0,8)] public int arcRefinementSteps = 5;
    [Min(.001f)] public float arcRefinementHeightMetres = .01f;
    [Range(15,75)] public float maximumLongitudinalArcDegrees = 65;
    [Min(0)] public float maximumLongitudinalReactionNewtons = 14000;
    [Range(3,9)] public int widthSamples = 5;
    [Min(0)] public float shoulderCrownDropMetres = .012f;
    [Min(0)] public float supportSwitchHysteresisMetres = .003f;
    [Min(0)] public float minimumSupportGainMetres = .002f;
    [Range(15, 75)] public float minimumWallSlopeDegrees = 28;
    [Min(0)] public float sidewallSpringNewtonsPerMetre = 60000;
    [Min(0)] public float sidewallDamperNsPerMetre = 1800;
    [Min(0)] public float maximumForcePerWheelNewtons = 9000;
    [Min(.001f)] public float maximumCompressionMetres = .07f;
    [Min(0)] public float contactSkinMetres = .003f;
    [Range(15, 75)] public float shoulderArcDegrees = 45;
    [Range(.1f, 1)] public float deflatedStiffnessMultiplier = .7f;
}

public struct WheelRutContact
{
    public Vector3 point, force;
    public float compressionMetres;
    public bool Active => force.sqrMagnitude > .01f;

    // Only the shoulder's lateral reaction is modelled here. The existing
    // WheelCollider suspension remains the sole source of vertical support.
    public static WheelRutContact Evaluate(Vector3 centre, Vector3 axle, Vector3 forward,
        Vector3 up, float radius, float width, int side, Scene scene, Rigidbody body,
        WheelRutContactSettings settings, float stiffnessMultiplier)
    {
        var best = new WheelRutContact();
        Vector3 lateral = Vector3.ProjectOnPlane(axle, Vector3.up).normalized;
        if (lateral.sqrMagnitude < .5f) return best;
        float minimumWallNormal = Mathf.Sin(settings.minimumWallSlopeDegrees * Mathf.Deg2Rad);
        for (int i = -1; i <= 1; i++)
        {
            float angle = i * settings.shoulderArcDegrees * Mathf.Deg2Rad;
            Vector3 shoulder = centre + axle * (side * width * .5f)
                + (forward * Mathf.Sin(angle) - up * Mathf.Cos(angle)) * Mathf.Max(.01f, radius - settings.contactSkinMetres);
            if (!SoftGroundRuts.TrySampleSurface(shoulder, scene, out var surface)) continue;
            float normalAcross = Vector3.Dot(surface.normal, lateral);
            if (normalAcross * side > -minimumWallNormal || surface.normal.y < .08f) continue;
            float compression = (surface.point.y - shoulder.y) * surface.normal.y / Mathf.Max(.1f, Mathf.Abs(normalAcross));
            if (compression <= settings.contactSkinMetres) continue;
            compression = Mathf.Min(compression - settings.contactSkinMetres, settings.maximumCompressionMetres);
            if (compression <= best.compressionMetres) continue;
            Vector3 direction = lateral * Mathf.Sign(normalAcross);
            float normalSpeed = Vector3.Dot(body.GetPointVelocity(shoulder), direction);
            float magnitude = Mathf.Clamp(settings.sidewallSpringNewtonsPerMetre * stiffnessMultiplier * compression
                - settings.sidewallDamperNsPerMetre * normalSpeed, 0, settings.maximumForcePerWheelNewtons);
            best = new WheelRutContact { point = shoulder, force = direction * magnitude, compressionMetres = compression };
        }
        return best;
    }
}
