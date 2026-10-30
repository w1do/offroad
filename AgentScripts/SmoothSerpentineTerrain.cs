using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SmoothSerpentineTerrain
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play");var r=UnityEngine.Object.FindAnyObjectByType<LongRouteStream>();var ground=GameObject.Find("Continuous forest ground");var colliders=ground.GetComponentsInChildren<MeshCollider>();var b=colliders[0].bounds;foreach(var c in colliders)b.Encapsulate(c.bounds);r.originalGroundMinimum=new Vector2(b.min.x,b.min.z);r.originalGroundWidth=Mathf.FloorToInt(b.size.x/8)+1;r.originalGroundDepth=Mathf.FloorToInt(b.size.z/8)+1;r.originalGroundHeights=new float[r.originalGroundWidth*r.originalGroundDepth];Physics.SyncTransforms();
 for(int z=0;z<r.originalGroundDepth;z++)for(int x=0;x<r.originalGroundWidth;x++){var hits=Physics.RaycastAll(new Vector3(b.min.x+x*8+.01f,200,b.min.z+z*8+.01f),Vector3.down,400,~0,QueryTriggerInteraction.Ignore).Where(h=>h.transform.IsChildOf(ground.transform)).OrderBy(h=>h.distance).ToArray();r.originalGroundHeights[z*r.originalGroundWidth+x]=hits.Length>0?hits[0].point.y:0;}
 int changed=0;foreach(var f in r.editorPreview.GetComponentsInChildren<MeshFilter>()){GameObject fresh=null;if(f.name.StartsWith("Terrain tile ")){var p=f.name.Split(' ');fresh=r.BuildTerrainTile(new Vector2Int(int.Parse(p[2]),int.Parse(p[3])));}else if(f.name.StartsWith("Route km ")){int index=(int)Math.Round(double.Parse(f.name.Substring(9))*5);fresh=r.BuildChunk(index);}if(fresh==null)continue;var mesh=fresh.GetComponent<MeshFilter>().sharedMesh;EditorUtility.CopySerialized(mesh,f.sharedMesh);EditorUtility.SetDirty(f.sharedMesh);f.GetComponent<MeshCollider>().sharedMesh=f.sharedMesh;UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(fresh);changed++;}
 foreach(var collider in r.editorPreview.GetComponentsInChildren<CapsuleCollider>()){var p=collider.transform.position;p.y=r.TerrainHeight(p.x,p.z);collider.transform.position=p;}
 EditorUtility.SetDirty(r);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "Smoothed "+changed+" road/terrain meshes with weighted slope interpolation between serpentine legs; original ground sampled for forest join";
 }
}
