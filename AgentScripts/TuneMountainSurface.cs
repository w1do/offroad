using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class TuneMountainSurface
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var stage=UnityEngine.Object.FindAnyObjectByType<MountainClimbStage>();int count=0;
        foreach(var s in stage.GetComponentsInChildren<ForestDrivingSurface>())if(s.cover==ForestDrivingSurface.Cover.Ice){s.lateralGrip=0.35f;EditorUtility.SetDirty(s);count++;}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return "Tuned "+count+" ice sections for controllable front-axle lateral grip.";
    }
}
