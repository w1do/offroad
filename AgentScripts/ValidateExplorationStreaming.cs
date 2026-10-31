using System;using System.Linq;using UnityEngine;using UnityEditor;
public static class ValidateExplorationStreaming {
 public static object Main(){if(!Application.isPlaying)throw new Exception("Requires Play Mode");var world=UnityEngine.Object.FindAnyObjectByType<ExplorationWorld>();if(world.Map==null)throw new Exception("World not running");var player=world.vehicle;var body=player.GetComponent<Rigidbody>();bool wasKinematic=body.isKinematic,paused=EditorApplication.isPaused;var velocity=body.linearVelocity;var angular=body.angularVelocity;var probe=new GameObject("Streaming diagnostic observer");
 try{EditorApplication.isPaused=true;body.isKinematic=true;probe.transform.position=player.position;world.vehicle=probe.transform;
 var soil=world.GetComponentsInChildren<SoftGroundRuts>().OrderBy(s=>(s.transform.position-player.position).sqrMagnitude).First();var mesh=soil.GetComponent<MeshFilter>().sharedMesh;var point=soil.transform.TransformPoint(mesh.vertices[mesh.vertexCount/2]);soil.Dig(point,1,.5f);soil.Commit();var tracks=soil.GetComponent<TireTrackRenderer>();tracks.Stamp(point,Vector3.forward,.7f,1,.1f,.2f,1);float before=soil.DepthAt(point);float treadBefore=tracks.TrackAt(point);string key=soil.persistentId;
 if(before<.001f||treadBefore<=0)throw new Exception("No diagnostic trace");
 probe.transform.position=new Vector3(world.Map.LakeCentre.x+world.Map.LakeRadii.x*1.08f,5,world.Map.LakeCentre.y);world.EnsureWindow(true);int atLake=world.LoadedTiles;
 if(world.GetComponentsInChildren<SoftGroundRuts>().Any(s=>s.persistentId==key))throw new Exception("Old tile retained active");
 var centre=new Vector3(world.Map.LakeCentre.x,ExplorationMap.LakeLevel-1,world.Map.LakeCentre.y);if(!WaterVolume.Sample(centre,out _,out var waterDepth)||waterDepth<.99f)throw new Exception("Lake has no water");
 var corner=centre+new Vector3(world.Map.LakeRadii.x*.9f,0,world.Map.LakeRadii.y*.9f);if(WaterVolume.Sample(corner,out _,out _))throw new Exception("Invisible lake water outside shore");
 int rays=0;float maxError=0;foreach(var filter in world.GetComponentsInChildren<MeshFilter>()){
 var physical=filter.GetComponent<MeshCollider>();if(physical==null||!(filter.name.StartsWith("Road adventure")||filter.name=="Forest ground"||filter.name=="Rocky lake shore"))continue;
 var independent=new GameObject("Independent terrain render probe");independent.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);var visible=independent.AddComponent<MeshCollider>();visible.sharedMesh=filter.sharedMesh;try{var v=filter.sharedMesh.vertices;for(int i=0;i<v.Length;i+=Mathf.Max(1,v.Length/8)){var ray=new Ray(filter.transform.TransformPoint(v[i])+Vector3.up*5,Vector3.down);if(visible.Raycast(ray,out var a,10)&&physical.Raycast(ray,out var b,10)){maxError=Mathf.Max(maxError,Mathf.Abs(a.point.y-b.point.y));rays++;}}}finally{UnityEngine.Object.DestroyImmediate(independent);}}
 probe.transform.position=player.position;world.EnsureWindow(true);var restored=world.GetComponentsInChildren<SoftGroundRuts>().First(s=>s.persistentId==key);float after=restored.DepthAt(point),treadAfter=restored.GetComponent<TireTrackRenderer>().TrackAt(point);
 if(Mathf.Abs(before-after)>.001f||treadAfter<=0||maxError>.002f||rays<20)throw new Exception("Persistence/contact failure before="+before+" after="+after+" tread="+treadAfter+" error="+maxError);
 return new{success=true,tilesAtLake=atLake,lakeDepth=waterDepth,independentRays=rays,maxMeshMismatch=maxError,rutBefore=before,rutRestored=after,treadBefore,treadRestored=treadAfter};
 }finally{world.vehicle=player;world.EnsureWindow(true);body.isKinematic=wasKinematic;if(!wasKinematic){body.linearVelocity=velocity;body.angularVelocity=angular;}EditorApplication.isPaused=paused;UnityEngine.Object.DestroyImmediate(probe);}
 }
}

