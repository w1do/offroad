using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
public static class ValidateFullWidthMudGeometry
{
 static int sections,rays;static float maximumGap,maximumRimGap;
 static void Check(GameObject root,bool main)
 {
  var fields=root.GetComponentsInChildren<MudRoadSection>().Where(s=>s.GetComponent<SoftGroundRuts>().enabled&&
    (!main||(s.GetComponent<SoftGroundRuts>().persistentId??"").StartsWith("main"))).ToArray();
  if(fields.Length==0)return;
  var candidates=main?root.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>()):root.GetComponentsInChildren<Collider>().AsEnumerable();
  var other=candidates.Where(c=>c.enabled&&!c.isTrigger&&
    (c.GetComponent<ForestGroundPatch>()!=null||c.GetComponent<SoftGroundRuts>()!=null||c.GetComponent<ForestDrivingSurface>()!=null)).ToArray();
  foreach(var section in fields)
  {
   if(!section.profile.fullWidthFurrows||section.halfSize.x<3.5f||section.mudHalfWidthMetres<section.halfSize.x-.001f)throw new Exception("Incomplete mud width");
   string token=main?section.GetComponent<SoftGroundRuts>().persistentId.Split('/')[0]:"module "+root.name;
   var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Generated/NaturalMud/Road "+token+".asset");
   if(source==null)throw new Exception("Missing perimeter source: "+token);
   var mesh=section.GetComponent<MeshFilter>().sharedMesh;var before=source.vertices;var after=mesh.vertices;
   if(before.Length!=after.Length)throw new Exception("Changed perimeter topology");
   for(int i=0;i<before.Length;i++)if(section.halfSize.x-Mathf.Abs(before[i].x)<.001f||section.halfSize.y-Mathf.Abs(before[i].z)<.001f)
    maximumRimGap=Mathf.Max(maximumRimGap,section.transform.TransformVector(after[i]-before[i]).magnitude);
   var probe=new GameObject("TEMP independent furrow render mesh");probe.transform.SetPositionAndRotation(section.transform.position,section.transform.rotation);probe.transform.localScale=section.transform.lossyScale;
   try
   {
    var reference=probe.AddComponent<MeshCollider>();reference.sharedMesh=mesh;Physics.SyncTransforms();
    for(int z=0;z<31;z++)for(int x=0;x<21;x++)
    {
     float px=Mathf.Lerp(-section.halfSize.x+.005f,section.halfSize.x-.005f,x/20f),pz=Mathf.Lerp(-section.halfSize.y+.005f,section.halfSize.y-.005f,z/30f);
     var ray=new Ray(section.transform.TransformPoint(new Vector3(px,0,pz))+Vector3.up*15,Vector3.down);
     if(!reference.Raycast(ray,out var visible,30)||!section.GetComponent<MeshCollider>().Raycast(ray,out var physical,30))throw new Exception("Hole in full-width bed: "+token);
     maximumGap=Mathf.Max(maximumGap,Mathf.Abs(visible.point.y-physical.point.y));rays++;
     if(Mathf.Abs(px)>section.halfSize.x-.2f||Mathf.Abs(pz)>section.halfSize.y-.2f)continue;
     foreach(var candidate in other)
     {
      if(candidate==section.GetComponent<MeshCollider>()||!candidate.bounds.IntersectRay(ray))continue;
      if(candidate.Raycast(ray,out var hit,30)&&hit.normal.y>.25f&&hit.point.y>visible.point.y+.004f&&hit.point.y<visible.point.y+.8f)
       throw new Exception("Old support above mud: "+token+" / "+candidate.name+" gap="+(hit.point.y-visible.point.y));
     }
    }
   }
   finally{UnityEngine.Object.DestroyImmediate(probe);}
   sections++;
  }
 }
 public static string Main()
 {
  if(Application.isPlaying)throw new Exception("Requires Edit mode");sections=rays=0;maximumGap=maximumRimGap=0;
  foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())Check(root,true);
  foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Generated/AuthoredLongForest"}))
  {
   string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
   try{Check(root,false);}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  if(sections!=14||maximumGap>.002f||maximumRimGap>.0001f)throw new Exception("Furrow geometry mismatch");
  string json=Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,sections,widthMetres=7.2f,rays,maximumGapMetres=maximumGap,maximumRimGapMetres=maximumRimGap},Newtonsoft.Json.Formatting.Indented);
  File.WriteAllText("docs/reports/full-width-furrow-geometry.json",json);return json;
 }
}
