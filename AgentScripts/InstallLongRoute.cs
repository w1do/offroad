using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class InstallLongRoute
{
 const string Folder="Assets/Game/Generated/LongRoute";
 static readonly float[] Across={-180,-130,-90,-60,-40,-25,-14,-8,-5,0,5,8,14,25,40,60,90,130,180};
 static GameObject[] Trees(GameObject forest)
 {
  var filters=forest.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.EndsWith(".001")||f.name.EndsWith(".004")||f.name.EndsWith(".006")).ToArray();
  var trunks=filters.First(f=>f.name.EndsWith(".006"));var anchors=new List<Vector4>();trunks.sharedMesh.GetUVs(2,anchors);
  var roots=anchors.Distinct().Where(a=>a.z>10&&a.z<42).Take(70).ToArray();var templates=new List<GameObject>();
  foreach(var root in roots)
  {
   var positions=new List<Vector3>();var uv=new List<Vector2>();var normals=new List<Vector3>();var indices=new List<int>[filters.Length];int foliage=0,trunkCount=0;
   for(int k=0;k<filters.Length;k++)
   {
    indices[k]=new List<int>();var f=filters[k];var mesh=f.sharedMesh;var p=mesh.vertices;var n=mesh.normals;var tex=mesh.uv;var a=new List<Vector4>();mesh.GetUVs(2,a);var t=mesh.triangles;var map=new Dictionary<int,int>();
    for(int j=0;j<t.Length;j+=3)
    {
     if((new Vector2(a[t[j]].x-root.x,a[t[j]].y-root.y)).sqrMagnitude>.01f||Mathf.Abs(a[t[j]].z-root.z)>.1f)continue;
     if(k==0)foliage++;else trunkCount++;
     for(int h=0;h<3;h++){int old=t[j+h];if(!map.TryGetValue(old,out int id)){id=positions.Count;map[old]=id;positions.Add(f.transform.TransformPoint(p[old])-new Vector3(root.x,root.z,root.y));uv.Add(tex[old]);normals.Add(f.transform.TransformDirection(n[old]));}indices[k].Add(id);}
    }
   }
   if(foliage<20||trunkCount<12||positions.Count<20)continue;
   var bounds=new Bounds(positions[0],Vector3.zero);foreach(var p in positions)bounds.Encapsulate(p);if(bounds.size.y<7||bounds.size.y>32||bounds.size.x>18||bounds.size.z>18)continue;
   int number=templates.Count;var materials=new List<Material>();foreach(var f in filters){var m=new Material(f.GetComponent<Renderer>().sharedMaterial);m.SetFloat("_LocalWindAnchor",1);AssetDatabase.CreateAsset(m,Folder+"/Tree "+number+" "+materials.Count+".mat");materials.Add(m);}
   var result=new Mesh{name="Route tree "+number,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};result.SetVertices(positions);result.SetNormals(normals);result.SetUVs(0,uv);result.SetUVs(2,Enumerable.Repeat(new Vector4(0,0,0,1),positions.Count).ToList());result.subMeshCount=filters.Length;for(int k=0;k<filters.Length;k++)result.SetTriangles(indices[k],k);result.RecalculateTangents();result.RecalculateBounds();AssetDatabase.CreateAsset(result,Folder+"/Tree "+number+".asset");
   var go=new GameObject("Route tree "+number);go.AddComponent<MeshFilter>().sharedMesh=result;go.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();var collider=go.AddComponent<CapsuleCollider>();collider.height=Mathf.Max(5,bounds.size.y*.75f);collider.radius=.22f;collider.center=new Vector3(0,collider.height*.5f,0);go.SetActive(false);templates.Add(PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Tree "+number+".prefab"));UnityEngine.Object.DestroyImmediate(go);if(templates.Count==4)break;
  }
  if(templates.Count==0)throw new Exception("No complete tree templates found");return templates.ToArray();
 }
 public static string Main()
 {
  if(Application.isPlaying)throw new Exception("Stop Play first");if(UnityEngine.Object.FindAnyObjectByType<LongRouteStream>()!=null)throw new Exception("Route already installed");
  var scene=EditorSceneManager.GetActiveScene();string backup="Assets/Game/Scenes/BeforeLongRoute.unity";if(File.Exists(backup))throw new Exception("Backup exists; inspect before repeating");EditorSceneManager.SaveScene(scene,backup,true);Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  var forest=GameObject.Find("Forest - imported dirt road environment");var terrain=GameObject.Find("Continuous forest ground");var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();
  var go=new GameObject("10 000 km - streamed taiga route");var route=go.AddComponent<LongRouteStream>();route.truck=car.transform;route.initialForward=new Vector3(-1,0,-.15f).normalized;
  var hit=Physics.RaycastAll(new Vector3(-94,150,28),Vector3.down,300,~0,QueryTriggerInteraction.Ignore).Where(h=>h.transform.IsChildOf(terrain.transform)).OrderBy(h=>h.distance).First();route.entrance=hit.point+Vector3.up*.04f;
  var sourceRoad=forest.GetComponentsInChildren<MeshRenderer>().First(r=>r.sharedMaterial.name.Contains("Dirt_Road_LightSnow"));route.roadMaterial=new Material(sourceRoad.sharedMaterial);route.roadMaterial.name="Long route gravel";route.roadMaterial.SetFloat("_UseWorldTextures",1);route.roadMaterial.SetFloat("_SnowAmount",.22f);AssetDatabase.CreateAsset(route.roadMaterial,Folder+"/Gravel.mat");
  var sourceGround=forest.GetComponentsInChildren<MeshRenderer>().First(r=>r.sharedMaterial.name.Contains("Aerial_Grass"));route.groundMaterial=new Material(sourceGround.sharedMaterial);route.groundMaterial.name="Long route forest soil";route.groundMaterial.SetFloat("_UseWorldTextures",1);AssetDatabase.CreateAsset(route.groundMaterial,Folder+"/Forest soil.mat");
  route.treeTemplates=Trees(forest);route.EnsurePath();route.joinHeights=new float[route.joinRows*LongRouteStream.AcrossCount];
  for(int i=0;i<route.joinRows;i++)for(int j=0;j<Across.Length;j++){double s=i*10;Vector3 p=route.Point(s)+Vector3.Cross(Vector3.up,route.Tangent(s)).normalized*Across[j];var hits=Physics.RaycastAll(new Vector3(p.x,150,p.z),Vector3.down,300,~0,QueryTriggerInteraction.Ignore).Where(h=>h.transform.IsChildOf(terrain.transform)).OrderBy(h=>h.distance).ToArray();route.joinHeights[i*Across.Length+j]=hits.Length>0?hits[0].point.y:route.Point(s).y;}
  // Cut a matching entrance into the existing ground, including its enclosing ridge.
  var centers=Enumerable.Range(0,81).Select(i=>route.Point(i*10)).ToArray();int edited=0;
  foreach(var mf in terrain.GetComponentsInChildren<MeshFilter>())
  {
   var b=mf.GetComponent<Renderer>().bounds;if(!centers.Any(p=>b.SqrDistance(p)<35*35))continue;var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);var p=mesh.vertices;bool changed=false;
   for(int i=0;i<p.Length;i++)
   {
    var world=mf.transform.TransformPoint(p[i]);float nearest=20*20;double sBest=-1;float cross=0;
    for(int j=0;j<80;j++){var a=centers[j];var d=centers[j+1]-a;d.y=0;var q=world-a;q.y=0;float t=Mathf.Clamp01(Vector3.Dot(q,d)/d.sqrMagnitude);var delta=q-d*t;float distance=delta.sqrMagnitude;if(distance<nearest){nearest=distance;sBest=(j+t)*10;cross=Vector3.Dot(delta,Vector3.Cross(Vector3.up,d.normalized));}}
    if(sBest<0)continue;float target=route.CrossHeight(sBest,cross)-.06f;float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,20,Mathf.Abs(cross)));world.y=Mathf.Lerp(world.y,target,blend);p[i]=mf.transform.InverseTransformPoint(world);changed=true;
   }
   if(!changed){UnityEngine.Object.DestroyImmediate(mesh);continue;}mesh.vertices=p;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Entrance "+mf.name+".asset");mf.sharedMesh=mesh;mf.GetComponent<MeshCollider>().sharedMesh=mesh;edited++;
  }
  route.editorPreview=new GameObject("Route preview - disabled automatically during Play");route.editorPreview.transform.SetParent(go.transform,false);
  for(int i=0;i<5;i++){var chunk=route.BuildChunk(i);var mesh=chunk.GetComponent<MeshFilter>().sharedMesh;AssetDatabase.CreateAsset(mesh,Folder+"/Preview "+i+".asset");chunk.transform.SetParent(route.editorPreview.transform,true);}
  var sign=new GameObject("Route extension entrance sign");sign.transform.position=route.entrance+new Vector3(0,2,-7);var text=sign.AddComponent<TextMesh>();text.text="10 000 KM\nTAIGA / MOUNTAIN ROAD\nLOW RANGE ON STEEP CLIMBS";text.characterSize=.15f;text.fontSize=48;text.anchor=TextAnchor.MiddleCenter;sign.transform.rotation=Quaternion.LookRotation(-route.initialForward);
  EditorUtility.SetDirty(route);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  return "Installed 10,000 km route; 50,000 streamed 200m chunks; 4 forest tree variants="+route.treeTemplates.Length+"; entrance ground chunks blended="+edited+"; first kilometre preview saved. Entrance="+route.entrance;
 }
}
