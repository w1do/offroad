using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
public static class ValidateWholeRoadSoil
{
 public static string Main()
 {
  if(!Application.isPlaying)throw new Exception("Requires Play mode");
  var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
  var dry=UnityEngine.Object.FindObjectsByType<SoftGroundRuts>().Where(s=>s.profile!=null&&s.profile.soil!=null&&s.profile.soil.soilType==SoilType.DryDirt&&s.GetComponent<Renderer>().enabled).ToArray();
  if(dry.Length==0)throw new Exception("No continuous dry road");
  foreach(var soil in dry){var m=soil.GetComponent<Renderer>().sharedMaterial;if(!AssetDatabase.GetAssetPath(m.GetTexture("_RoadMap")).Contains("dry_mud_field_001"))throw new Exception("Wrong dry road material");if(soil.GetComponent<TireTrackRenderer>()==null)throw new Exception("No track renderer");}
  var mud=UnityEngine.Object.FindObjectsByType<MudRoadSection>().First(s=>s.GetComponent<SoftGroundRuts>().persistentId.StartsWith("main0"));
  Vector3 entry=mud.transform.position-mud.transform.forward*10;
  var entryHit=Physics.RaycastAll(entry+Vector3.up*10,Vector3.down,30,~0,QueryTriggerInteraction.Ignore).Where(h=>dry.Contains(h.collider.GetComponent<SoftGroundRuts>())).OrderBy(h=>h.distance).First();
  var tile=entryHit.collider.GetComponent<SoftGroundRuts>();
  var scene=SceneManager.CreateScene("Isolated full road test "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
  GameObject ground=null,car=null;
  try
  {
   ground=new GameObject("Test copy of actual road tile");ground.SetActive(false);SceneManager.MoveGameObjectToScene(ground,scene);
   ground.transform.SetPositionAndRotation(tile.transform.position,tile.transform.rotation);ground.transform.localScale=tile.transform.lossyScale;
   var mesh=tile.GetComponent<MeshFilter>().sharedMesh;ground.AddComponent<MeshFilter>().sharedMesh=mesh;ground.AddComponent<MeshRenderer>().sharedMaterial=tile.GetComponent<Renderer>().sharedMaterial;
   ground.AddComponent<MeshCollider>().sharedMesh=mesh;var ruts=ground.AddComponent<SoftGroundRuts>();ruts.profile=tile.profile;
   var cover=ground.AddComponent<ForestDrivingSurface>();cover.cover=ForestDrivingSurface.Cover.Gravel;cover.forwardGrip=.65f;cover.lateralGrip=.7f;cover.rollingResistance=.025f;
   var tracks=ground.AddComponent<TireTrackRenderer>();tracks.persistentId="dry-road-validation/"+Guid.NewGuid();ruts.persistentId=tracks.persistentId;ground.SetActive(true);
   foreach(var neighbour in dry.Where(s=>s!=tile&&s.GetComponent<Renderer>().bounds.SqrDistance(entry)<20*20))
   {
    var g=new GameObject("Neighbouring road tile");g.SetActive(false);SceneManager.MoveGameObjectToScene(g,scene);g.transform.SetPositionAndRotation(neighbour.transform.position,neighbour.transform.rotation);g.transform.localScale=neighbour.transform.lossyScale;
    var m=neighbour.GetComponent<MeshFilter>().sharedMesh;g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=neighbour.GetComponent<Renderer>().sharedMaterial;g.AddComponent<MeshCollider>().sharedMesh=m;
    var s=g.AddComponent<SoftGroundRuts>();s.profile=neighbour.profile;var c=g.AddComponent<ForestDrivingSurface>();c.cover=ForestDrivingSurface.Cover.Gravel;c.forwardGrip=.65f;c.lateralGrip=.7f;
    g.AddComponent<TireTrackRenderer>();g.SetActive(true);
   }
   var verts=mesh.vertices;var tris=mesh.triangles;Vector3 centre=mesh.bounds.center;int best=0;float error=float.MaxValue;
   for(int i=0;i<tris.Length;i+=3){var p=(verts[tris[i]]+verts[tris[i+1]]+verts[tris[i+2]])/3;float d=(p-centre).sqrMagnitude;if(d<error){best=i;error=d;}}
   Vector3 start=entryHit.point;
   float xx=0,zz=0,xz=0;foreach(var p in verts){var d=ground.transform.TransformVector(p-centre);xx+=d.x*d.x;zz+=d.z*d.z;xz+=d.x*d.z;}
   var direction=Vector3.ProjectOnPlane(mud.transform.forward,Vector3.up).normalized;
   car=UnityEngine.Object.Instantiate(source.gameObject,start+Vector3.up*.65f,Quaternion.LookRotation(direction));SceneManager.MoveGameObjectToScene(car,scene);
   var v=car.GetComponent<VehiclePhysics>();v.automaticSimulation=false;v.externalControl=true;v.Initialize();v.SetFourWheelDrive(true);v.SetTransmission(false,true);
   var rb=car.GetComponent<Rigidbody>();rb.isKinematic=false;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
   foreach(var input in car.GetComponents<VehiclePlayerInput>())input.enabled=false;
   v.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();
   for(int i=0;i<150;i++){v.Simulate(.01f);physics.Simulate(.01f);}
   if(v.Wheels.Count(w=>w.isGrounded)<3)throw new Exception("Dry road fixture did not settle on its tile");
   Vector3 settled=rb.position;v.SetInput(new VehicleControlInput{throttle=1});
   float peakDepth=0,peakTrack=0;int grounded=0;
   for(int i=0;i<250;i++)
   {
    v.Simulate(.01f);physics.Simulate(.01f);peakDepth=Mathf.Max(peakDepth,ruts.DugDepth);
    foreach(var w in v.Wheels)if(w.GetGroundHit(out var hit)){grounded++;peakTrack=Mathf.Max(peakTrack,tracks.TrackAt(hit.point));}
   }
   ruts.Commit();float travel=Vector3.Distance(settled,rb.position);
   if(grounded<100||peakTrack<.1f||peakDepth<.0002f||peakDepth>.076f||travel<.2f)throw new Exception("Dry drive result: contacts="+grounded+" track="+peakTrack+" depth="+peakDepth+" travel="+travel);
   var points=ground.GetComponent<MeshFilter>().sharedMesh.vertices;var physical=ground.GetComponent<MeshCollider>();int probes=0;float gap=0;
   var probe=new GameObject("Independent render mesh probe");SceneManager.MoveGameObjectToScene(probe,scene);probe.transform.SetPositionAndRotation(ground.transform.position,ground.transform.rotation);probe.transform.localScale=ground.transform.lossyScale;
   var rendered=probe.AddComponent<MeshCollider>();rendered.sharedMesh=ground.GetComponent<MeshFilter>().sharedMesh;
   for(int i=0;i<points.Length;i+=Mathf.Max(1,points.Length/100))
   {
    var ray=new Ray(ground.transform.TransformPoint(points[i])+Vector3.up,Vector3.down);
    if(rendered.Raycast(ray,out var a,2)&&physical.Raycast(ray,out var b,2)){gap=Mathf.Max(gap,Mathf.Abs(a.point.y-b.point.y));probes++;}
   }
   if(probes<10||gap>.004f)throw new Exception("Road collider mismatch "+gap);
   Vector3 testPoint=start;tracks.Stamp(testPoint,car.transform.forward,.5f,.5f,0,.16f,.4f);float mark=tracks.TrackAt(testPoint);
   tracks.enabled=false;tracks.enabled=true;
   if(Mathf.Abs(tracks.TrackAt(testPoint)-mark)>.001f)throw new Exception("Tracks lost on re-enable");
   // Capture the actual physical trial while hiding only the overlapping source road copies.
   var hidden=dry.Where(s=>s.GetComponent<Renderer>().bounds.SqrDistance(entry)<20*20).Select(s=>s.GetComponent<Renderer>()).ToArray();
   var sourceRenderers=source.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
   var camera=Camera.main;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;float field=camera.fieldOfView;var target=camera.targetTexture;var active=RenderTexture.active;
   var rt=RenderTexture.GetTemporary(1440,900,24);
   try{
    foreach(var renderer in hidden)renderer.enabled=false;
    foreach(var renderer in sourceRenderers)renderer.enabled=false;
    v.SendMessage("LateUpdate");
    Vector3 look=(settled+rb.position)*.5f;camera.transform.position=look-direction*5+Vector3.Cross(Vector3.up,direction)*4+Vector3.up*3.4f;camera.transform.LookAt(look);camera.fieldOfView=58;
    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes("work/DryRoadActualWheelTracks.png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
   }finally{foreach(var renderer in hidden)if(renderer!=null)renderer.enabled=true;foreach(var renderer in sourceRenderers)if(renderer!=null)renderer.enabled=true;camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);camera.fieldOfView=field;camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
   string report="PASS continuous dry road tiles="+dry.Length+"; actual tire contacts="+grounded+"; physical travel="+travel.ToString("F2")+"m; rut="+peakDepth.ToString("F4")+"m; tread="+peakTrack.ToString("F2")+"; mesh probes="+probes+"; collision gap="+gap.ToString("F5")+"m; retained tread="+mark;
   File.WriteAllText("work/whole-road-validation.txt",report);return report;
  }
  finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);}
 }
}

