using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FixAuthoredForestCollision
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play first");var forest=GameObject.Find("Forest - imported dirt road environment");var originals=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Forest/Dirt road scene.fbx").GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.name);int count=0;
 foreach(var f in forest.GetComponentsInChildren<MeshFilter>()){var source=originals[f.name].sharedMesh;var anchors=new List<Vector4>();f.sharedMesh.GetUVs(2,anchors);if(AssetDatabase.GetAssetPath(f.sharedMesh).Contains("AuthoredForestPajero")&&anchors.Count==source.vertexCount){var mesh=f.sharedMesh;EditorUtility.CopySerialized(source,mesh);mesh.SetUVs(2,anchors);mesh.SetTriangles(source.triangles,0);EditorUtility.SetDirty(mesh);}else f.sharedMesh=source;
 if(f.TryGetComponent<MeshCollider>(out var c)){c.sharedMesh=source;string material=originals[f.name].GetComponent<Renderer>().sharedMaterial.name;c.enabled=!(material.Contains("Leaves")||material.Contains("Vegetation")||material.Contains("Puddle")||material.Contains("Trails")||material.Contains("Road_Edge")||material.Contains("Grass_Close"));PrefabUtility.RecordPrefabInstancePropertyModifications(c);}PrefabUtility.RecordPrefabInstancePropertyModifications(f);count++;}
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "Restored exact original index/vertex buffers on "+count+" forest meshes. Removed collision from grass/decal overlays; physical terrain/road/tree colliders retained.";
 }
}
