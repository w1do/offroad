using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class SaveTruckRepair
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var scene=SceneManager.GetActiveScene();string path=scene.path;
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed.");
        EditorSceneManager.OpenScene(path);
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();
        var part=car.transform.Find("Fixed axle details");
        if(part==null || part.parent!=car.transform)throw new Exception("Axle attachment missing after reload.");
        foreach(var pivot in car.visuals)if(part.IsChildOf(pivot))throw new Exception("Axle inside wheel.");
        return "Saved and reloaded "+path+"; axle attached to chassis, outside all wheel pivots; mud areas="+UnityEngine.Object.FindObjectsByType<ForestDrivingSurface>().Length;
    }
}
