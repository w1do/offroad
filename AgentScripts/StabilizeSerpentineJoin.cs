using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class StabilizeSerpentineJoin
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play");var scene=SceneManager.GetActiveScene();var route=UnityEngine.Object.FindAnyObjectByType<LongRouteStream>();var root=GameObject.Find("Continuous forest ground");var reference=EditorSceneManager.OpenScene("Assets/Game/Scenes/BeforeLongRoute.unity",OpenSceneMode.Additive);int changed=0;
 try{var source=reference.GetRootGameObjects().First(g=>g.name==root.name).GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.name);foreach(var f in root.GetComponentsInChildren<MeshFilter>()){var original=source[f.name].sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);var p=mesh.vertices;bool edit=false;for(int i=0;i<p.Length;i++){var world=f.transform.TransformPoint(p[i]);if(route.DistanceToRoad(world.x,world.z)>120)continue;world.y=Mathf.Min(world.y,route.TerrainHeight(world.x,world.z)-.10f);p[i]=f.transform.InverseTransformPoint(world);edit=true;}if(edit){mesh.vertices=p;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();string path="Assets/Game/Generated/VisibleSerpentine/Stable "+f.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}f.sharedMesh=mesh;changed++;}else{f.sharedMesh=original;UnityEngine.Object.DestroyImmediate(mesh);}f.GetComponent<MeshCollider>().sharedMesh=f.sharedMesh;}}
 finally{EditorSceneManager.CloseScene(reference,true);SceneManager.SetActiveScene(scene);}AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Restored original lower forest terrain and safely recessed it below streamed road/terrain; no narrow raised earth walls. Updated="+changed;
 }
}
