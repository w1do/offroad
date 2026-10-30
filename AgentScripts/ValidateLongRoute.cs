using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateLongRoute
{
 static void Capture(LongRouteStream route,double s,string name)
 {
  var camera=Camera.main;var pos=camera.transform.position;var rotation=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);
  try{camera.transform.position=route.Point(s)+Vector3.up*16-route.Tangent(s)*35;camera.transform.LookAt(route.Point(s+75)+Vector3.up*2);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory("work");File.WriteAllBytes("work/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);}
  finally{camera.targetTexture=target;RenderTexture.active=active;camera.transform.SetPositionAndRotation(pos,rotation);RenderTexture.ReleaseTemporary(rt);}
 }
 public static string Main()
 {
  if(!Application.isPlaying)throw new Exception("Requires Play");var route=UnityEngine.Object.FindAnyObjectByType<LongRouteStream>();route.EnsurePath();var car=route.truck.GetComponent<ForestKamazController>();var rb=car.GetComponent<Rigidbody>();var oldMode=Physics.simulationMode;bool paused=EditorApplication.isPaused;Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;
  int roadHits=0;float maxSeam=0,maxGrade=0;int uphill=0,downhill=0,left=0,right=0;string drive="";
  try
  {
   foreach(double centre in new[]{0.0,1200.0,100000.0,5000000.0,9999400.0})
   {
    Vector3 delta=route.Point(centre);rb.linearVelocity=new Vector3(1,0,2);var beforeVelocity=rb.linearVelocity;route.ShiftOrigin(delta);if((rb.linearVelocity-beforeVelocity).magnitude>.001f)throw new Exception("Origin shift changed velocity");
    int current=(int)(centre/route.chunkMetres);route.EnsureWindow(current,true);Physics.SyncTransforms();if(route.LoadedChunks>route.ahead+route.behind+3)throw new Exception("Unbounded chunk count");
    for(double s=centre;s<Math.Min(route.lengthMetres,centre+390);s+=5){var p=route.Point(s);var hits=Physics.RaycastAll(p+Vector3.up*20,Vector3.down,60,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.parent==route.transform).OrderBy(h=>h.distance).ToArray();if(hits.Length==0||Mathf.Abs(hits[0].point.y-p.y)>.3f)throw new Exception("Road missing/mismatch at "+s);roadHits++;}
    var a=route.transform.Cast<Transform>().First(t=>t.name=="Route km "+(current*.2).ToString("F1"));var b=route.transform.Cast<Transform>().First(t=>t.name=="Route km "+((current+1)*.2).ToString("F1"));var va=a.GetComponent<MeshFilter>().sharedMesh.vertices;var vb=b.GetComponent<MeshFilter>().sharedMesh.vertices;
    for(int j=0;j<5;j++)maxSeam=Mathf.Max(maxSeam,Vector3.Distance(a.TransformPoint(va[va.Length-5+j]),b.TransformPoint(vb[j])));if(maxSeam>.01f)throw new Exception("Chunk seam="+maxSeam);
    if(centre==1200)Capture(route,centre,"LongRouteForest");if(centre==9999400)Capture(route,centre,"LongRouteFarEnd");
   }
   // Check vertical/turn variety throughout the requested distance, with a local origin for each window.
   for(double s=1000;s<route.lengthMetres-100;s+=1000){route.ShiftOrigin(route.Point(s));var d=route.Point(s+20)-route.Point(s);float grade=d.y/new Vector2(d.x,d.z).magnitude;maxGrade=Mathf.Max(maxGrade,Mathf.Abs(grade));if(grade>.04f)uphill++;if(grade<-.04f)downhill++;float turn=Vector3.SignedAngle(route.Tangent(s),route.Tangent(s+40),Vector3.up);if(turn>1)right++;if(turn< -1)left++;}
   if(uphill<100||downhill<100||left<100||right<100||maxGrade>.185f)throw new Exception("Invalid route variety/grade");
   route.ShiftOrigin(route.Point(700));route.EnsureWindow(3,true);rb.position=route.Point(700)+Vector3.up*.45f;rb.rotation=Quaternion.LookRotation(route.Tangent(700));car.transform.SetPositionAndRotation(rb.position,rb.rotation);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();car.testControl=true;car.testBrake=true;car.testThrottle=0;car.testSteer=0;car.lowRange=true;
   var chains=car.GetComponent<TruckChains>();car.SendMessage("Update");chains.RequestToggle();chains.Advance(5);
   double progress=700;int contacts=0;
   for(int i=0;i<6000;i++){
    double nearest=progress;float best=float.MaxValue;for(double s=Math.Max(680,progress-8);s<=progress+15;s+=1){float d=(route.Point(s)-rb.position).sqrMagnitude;if(d<best){best=d;nearest=s;}}progress=nearest;
    var target=car.transform.InverseTransformPoint(route.Point(progress+10));car.testSteer=Mathf.Clamp(Mathf.Atan2(target.x,Mathf.Max(1,target.z))*Mathf.Rad2Deg/24,-1,1);car.testBrake=false;car.testThrottle=car.SpeedKmh<22?.8f:0;car.SendMessage("Update");car.SendMessage("FixedUpdate");Physics.Simulate(.02f);car.SendMessage("LateUpdate");if(car.wheels.Any(w=>w.isGrounded))contacts++;
    if(rb.position.y<route.Point(progress).y-2)throw new Exception("Truck below streamed road at "+progress);if(progress>950)break;
   }
   if(progress<950||contacts<100)throw new Exception("Actual route drive failed at "+progress+" speed="+car.SpeedKmh);
   drive="actual truck drove "+(progress-700)+"m over generated hills and turns";
   return "PASS length=10,000 km; "+roadHits+" physical road samples at start/1.2km/100km/5000km/9999.4km; seam="+maxSeam+"m; maximum grade="+maxGrade+"; sampled uphill/downhill="+uphill+"/"+downhill+", left/right="+left+"/"+right+"; chunks bounded="+route.LoadedChunks+"; origin shifts preserve velocity; "+drive;
  }
  finally{car.testControl=false;car.testThrottle=0;car.testSteer=0;car.testBrake=false;Physics.simulationMode=oldMode;EditorApplication.isPaused=paused;}
 }
}

