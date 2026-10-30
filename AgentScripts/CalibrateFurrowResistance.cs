using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class CalibrateFurrowResistance
{
 public static string Main()
 {
  if(!Application.isPlaying)throw new Exception("Requires Play");
  var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var section=UnityEngine.Object.FindObjectsByType<MudRoadSection>().First(s=>s.GetComponent<SoftGroundRuts>().persistentId=="main0/natural");
  var body=source.GetComponent<Rigidbody>();bool kinematic=body.isKinematic;var velocity=body.linearVelocity;var angular=body.angularVelocity;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused;
  float original=section.profile.buriedRollingResistance;var method=typeof(ValidateMudMomentum).GetMethod("Trial",BindingFlags.NonPublic|BindingFlags.Static);var rows=new List<object>();
  try
  {
   Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;body.isKinematic=true;
   foreach(float coefficient in new[]{.32f,.38f,.44f})
   {
    section.profile.buriedRollingResistance=coefficient;
    foreach(float offset in new[]{-2.2f,0,2.2f})foreach(float speed in new[]{7f,0f})
    {
     object[] args={source,section,speed,0f,0f,offset,true};string description=(string)method.Invoke(null,args);
     rows.Add(new{coefficient,offset,speed,travel=(float)args[3],depth=(float)args[4],description});
    }
   }
   string json=Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented);System.IO.File.WriteAllText("docs/reports/furrow-resistance-calibration.json",json);return json;
  }
  finally{section.profile.buriedRollingResistance=original;body.isKinematic=kinematic;if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angular;}Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
}
