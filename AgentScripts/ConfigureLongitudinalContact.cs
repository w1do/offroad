using System;
using UnityEngine;
using UnityEditor;
public static class ConfigureLongitudinalContact
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Save configuration in Edit mode");
        var vehicle=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        if(vehicle==null||vehicle.profile==null)throw new Exception("Active vehicle missing");
        var settings=vehicle.profile.rutContact;
        settings.longitudinalSupport=true;
        settings.longitudinalSamples=9;
        settings.adaptiveLongitudinalSampling=true;
        settings.arcRefinementSteps=5;
        settings.arcRefinementHeightMetres=.01f;
        settings.maximumLongitudinalArcDegrees=65;
        settings.maximumLongitudinalReactionNewtons=14000;
        EditorUtility.SetDirty(vehicle.profile);AssetDatabase.SaveAssetIfDirty(vehicle.profile);
        return AssetDatabase.GetAssetPath(vehicle.profile)+": longitudinal support enabled";
    }
}
