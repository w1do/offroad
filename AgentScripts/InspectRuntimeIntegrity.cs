using System;
using System.Linq;
using UnityEngine;
public static class InspectRuntimeIntegrity {
public static object Main(){var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var objects=Resources.FindObjectsOfTypeAll<GameObject>().Where(g=>g.scene==scene).ToArray();
var missing=objects.Where(g=>g.GetComponents<MonoBehaviour>().Any(c=>c==null)).Select(g=>g.name).Take(20).ToArray();
var vehicles=UnityEngine.Object.FindObjectsByType<VehiclePhysics>().Where(v=>v.enabled).Select(v=>new {v.name,v.Ready,axles=v.axles==null?0:v.axles.Length,wheels=v.Wheels.Length,error=v.profile==null?"Missing profile":v.profile.ValidationError(v.axles==null?0:v.axles.Length),competingLegacy=v.GetComponent<ForestKamazController>()!=null&&v.GetComponent<ForestKamazController>().enabled}).ToArray();
var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();var duplicates=route.GetComponentsInChildren<AuthoredForestBlockInstance>().GroupBy(x=>new{x.arm,x.block}).Where(g=>g.Count()>1).Select(g=>new {g.Key,count=g.Count()}).ToArray();
var result=new {passed=missing.Length==0&&vehicles.All(v=>v.Ready&&v.error==null&&!v.competingLegacy)&&duplicates.Length==0,scene=scene.name,sceneObjects=objects.Length,missingScripts=missing,vehicles,duplicateBlocks=duplicates,loadedBlocks=route.LoadedBlocks};System.IO.File.WriteAllText("docs/reports/runtime-integrity.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
}}
