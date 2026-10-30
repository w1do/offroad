using System.Linq;
using UnityEngine;
using UnityEditor;
public static class InspectVisiblePuddles
{
 public static string Main(){
 var car=Object.FindAnyObjectByType<VehiclePhysics>();
 var rows=Object.FindObjectsByType<Renderer>().Where(r=>r.enabled&&(r.name.Contains("Natural")||r.name.Contains("Basin")||r.sharedMaterials.Any(m=>m!=null&&m.name.Contains("Dirt_Road")))).OrderBy(r=>(r.bounds.center-car.transform.position).sqrMagnitude).Take(22).Select(r=>new {r.name,pos=r.bounds.center.ToString(),distance=Vector3.Distance(r.bounds.center,car.transform.position), mats=r.sharedMaterials.Select(m=>new{m.name,shader=m.shader.name,baseMap=m.HasProperty("_BaseMap")?AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")):"",mud=m.HasProperty("_MudBlend")?m.GetFloat("_MudBlend"):0,mudMap=m.HasProperty("_MudMap")?AssetDatabase.GetAssetPath(m.GetTexture("_MudMap")):""})});
 return Newtonsoft.Json.JsonConvert.SerializeObject(new{playing=Application.isPlaying,car=car.transform.position.ToString(),rows},Newtonsoft.Json.Formatting.Indented);
 }
}
