using System;
using UnityEngine;
using UnityEditor;
public static class ValidateSharedDrift
{
 public static string Main(){if(!Application.isPlaying)throw new Exception("Requires Play");var v=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var rb=v.GetComponent<Rigidbody>();var p=rb.position;var q=rb.rotation;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused,auto=v.automaticSimulation,external=v.externalControl;var floor=new GameObject("TEMP controlled drift floor");floor.transform.position=new Vector3(20000,20,20000);var collider=floor.AddComponent<BoxCollider>();collider.size=new Vector3(300,1,300);collider.center=Vector3.down*.5f;
 try{Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;v.automaticSimulation=false;v.externalControl=true;rb.position=new Vector3(20000,20.6f,20000);rb.rotation=Quaternion.identity;v.transform.SetPositionAndRotation(rb.position,rb.rotation);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;v.ResetState();v.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();for(int i=0;i<200;i++)Step(v);v.SetTransmission(false,false);v.Shift(1);rb.linearVelocity=Vector3.forward*11.5f;float peak=0,roll=0;
 for(int i=0;i<120;i++){v.SetInput(new VehicleControlInput{throttle=.40f,steering=.45f,handbrake=i<15});Step(v);peak=Mathf.Max(peak,Mathf.Abs(v.SlipAngle));roll=Mathf.Max(roll,Vector3.Angle(v.transform.up,Vector3.up));if(peak>18)break;}float before=Mathf.Abs(v.SlipAngle);
 for(int i=0;i<300;i++){v.SetInput(new VehicleControlInput{throttle=.45f,steering=Mathf.Clamp(v.SlipAngle/25,-.8f,.8f)});Step(v);}float after=Mathf.Abs(v.SlipAngle);if(peak<7||roll>55||after>Mathf.Max(9,before*.8f)||v.SpeedKmh<5)throw new Exception("Drift/control failed peak="+peak+" before="+before+" after="+after+" roll="+roll+" finalSpeed="+v.SpeedKmh);return "PASS shared-core drift and active countersteer recovery: peak slip="+peak.ToString("F1")+"deg, recovery="+before.ToString("F1")+" -> "+after.ToString("F1")+"deg while moving at "+v.SpeedKmh.ToString("F1")+"km/h, maximum roll="+roll.ToString("F1")+"deg; no scripted yaw/velocity override.";
 }finally{UnityEngine.Object.DestroyImmediate(floor);rb.position=p;rb.rotation=q;v.transform.SetPositionAndRotation(p,q);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;v.SetInput(default);v.ResetState();v.automaticSimulation=auto;v.externalControl=external;Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
 static void Step(VehiclePhysics v){v.Simulate(.01f);Physics.Simulate(.01f);v.SendMessage("LateUpdate");}
}



