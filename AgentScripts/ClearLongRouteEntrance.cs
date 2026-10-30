using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ClearLongRouteEntrance
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play");var route=UnityEngine.Object.FindAnyObjectByType<LongRouteStream>();var forest=GameObject.Find("Forest - imported dirt road environment");var points=Enumerable.Range(0,81).Select(i=>route.Point(i*10)).ToArray();var cache=new Dictionary<Vector4,bool>();int removed=0;
 foreach(var f in forest.GetComponentsInChildren<MeshFilter>()){if(!f.GetComponent<Renderer>().enabled||f.sharedMesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord2)!=4)continue;var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);var a=new List<Vector4>();mesh.GetUVs(2,a);var t=mesh.triangles;var keep=new List<int>();int count=0;
 for(int i=0;i<t.Length;i+=3){var anchor=a[t[i]];if(!cache.TryGetValue(anchor,out bool cut)){var p=new Vector3(anchor.x,anchor.z,anchor.y);cut=false;for(int j=0;j<80;j++){var d=points[j+1]-points[j];d.y=0;var q=p-points[j];q.y=0;float u=Mathf.Clamp01(Vector3.Dot(q,d)/d.sqrMagnitude);if((q-u*d).sqrMagnitude<100){cut=true;break;}}cache[anchor]=cut;}if(cut)count++;else keep.AddRange(new[]{t[i],t[i+1],t[i+2]});}
 if(count==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}mesh.SetTriangles(keep,0);mesh.RecalculateBounds();string path="Assets/Game/Generated/VisibleSerpentine/Entrance clear "+f.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);f.sharedMesh=mesh;if(f.TryGetComponent<MeshCollider>(out var mc))mc.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(f);if(mc!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(mc);removed+=count;
 }AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "Removed "+removed+" tree/plant triangles and matching trunk collision from entrance road corridor";
 }
}

