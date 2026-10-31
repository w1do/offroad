using System;
using System.Linq;
using UnityEngine;
public static class ValidateRouteReloadLifecycle {
public static object Main(){
 var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();int loaded=route.LoadedBlocks,children=route.transform.childCount,arm=route.ActiveArm;double progress=route.Distance;
 var roots=route.GetComponentsInChildren<AuthoredForestBlockInstance>().Select(x=>x.GetEntityId().ToString()).OrderBy(x=>x).ToArray();
 float depths=route.GetComponentsInChildren<SoftGroundRuts>().Sum(s=>s.DugDepth);
 var field=typeof(AuthoredForestRoute).GetField("loaded",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 for(int repeat=0;repeat<3;repeat++){
  route.enabled=false;((System.Collections.IDictionary)field.GetValue(route)).Clear();
  foreach(var a in route.arms){a.records=null;a.descriptions=null;a.current=0;a.progress=0;}
  route.enabled=true;
  var after=route.GetComponentsInChildren<AuthoredForestBlockInstance>().Select(x=>x.GetEntityId().ToString()).OrderBy(x=>x).ToArray();
  if(route.LoadedBlocks!=loaded||route.transform.childCount!=children||!roots.SequenceEqual(after))throw new Exception("Reload duplicated or replaced loaded route objects");
  if(route.ActiveArm!=arm||Math.Abs(route.Distance-progress)>.000001)throw new Exception("Reload changed route progress");
  if(Mathf.Abs(depths-route.GetComponentsInChildren<SoftGroundRuts>().Sum(s=>s.DugDepth))>.00001f)throw new Exception("Reload lost ruts");
 }
 var result=new {passed=true,cycles=3,loadedBlocks=loaded,children,instances=roots.Length,progressPreserved=true,rutsPreserved=true,sameInstances=true};
 System.IO.File.WriteAllText("docs/reports/route-reload-lifecycle.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
}}
