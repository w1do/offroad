using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
public static class ValidateOffroadEditState
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit mode");
        var roots=SceneManager.GetActiveScene().GetRootGameObjects();
        int missing=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        int helpers=roots.Sum(r=>r.GetComponentsInChildren<WheelContactEnvelope>(true).Length);
        if(missing!=0||helpers!=0)throw new Exception("Unexpected persisted runtime state: missing="+missing+", envelopes="+helpers);
        var vehicle=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        if(vehicle==null||vehicle.profile==null||!vehicle.profile.rutContact.shoulderSupport||!vehicle.profile.rutContact.longitudinalSupport)throw new Exception("Vehicle profile/support missing");
        return "Edit mode verified; missing scripts=0; persisted wheel envelopes=0; shoulder and longitudinal support enabled; scene="+SceneManager.GetActiveScene().path;
    }
}
