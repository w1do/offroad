using System;
using UnityEngine;
using UnityEditor;
public static class ValidateLongRouteEntrance
{
 public static string Main(){if(!Application.isPlaying)throw new Exception("Requires Play");var route=UnityEngine.Object.FindAnyObjectByType<LongRouteStream>();var car=route.truck.GetComponent<ForestKamazController>();var rb=car.GetComponent<Rigidbody>();var pos=rb.position;var rot=rb.rotation;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused;double progress=0;
 try{Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;route.EnsureWindow(0,true);rb.position=route.Point(0)+Vector3.up*.5f;rb.rotation=Quaternion.LookRotation(route.Tangent(0));car.transform.SetPositionAndRotation(rb.position,rb.rotation);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;car.testControl=true;car.lowRange=true;Physics.SyncTransforms();
 for(int i=0;i<7000;i++){float best=float.MaxValue;double nearest=progress;for(double s=Math.Max(0,progress-5);s<=progress+12;s+=1){float d=(route.Point(s)-rb.position).sqrMagnitude;if(d<best){best=d;nearest=s;}}progress=nearest;var target=car.transform.InverseTransformPoint(route.Point(progress+9));car.testSteer=Mathf.Clamp(Mathf.Atan2(target.x,Mathf.Max(1,target.z))*Mathf.Rad2Deg/24,-1,1);car.testBrake=false;car.testThrottle=car.SpeedKmh<18?.8f:0;car.SendMessage("Update");car.SendMessage("FixedUpdate");Physics.Simulate(.02f);car.SendMessage("LateUpdate");if(rb.position.y<route.Point(progress).y-2)throw new Exception("Below entrance road");if(progress>250)return "PASS actual KAMAZ drove from original forest entrance through first 250m, including join and first chunk seam";}
 throw new Exception("Entrance obstructed at "+progress+"m speed="+car.SpeedKmh+" position="+rb.position);
 }finally{car.testControl=false;car.testThrottle=0;car.testBrake=false;car.testSteer=0;car.lowRange=false;rb.position=pos;rb.rotation=rot;car.transform.SetPositionAndRotation(pos,rot);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
}
