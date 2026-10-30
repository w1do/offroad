using System.Linq;
using UnityEngine;
public static class InspectExpandedModule
{
 public static string Main(){var route=Object.FindAnyObjectByType<AuthoredForestRoute>();var block=route.CreateBlock(0,100);try{return Newtonsoft.Json.JsonConvert.SerializeObject(block.GetComponentsInChildren<SoftGroundRuts>().Where(s=>s.enabled&&s.profile!=null).Select(s=>new{s.name,id=s.persistentId,parent=s.transform.parent.name,position=s.transform.position.ToString(),origin=s.transform.parent.position.ToString(),enabled=s.GetComponent<MeshCollider>().enabled,mesh=s.GetComponent<MeshFilter>().sharedMesh.name,hit=s.GetComponent<MeshCollider>().Raycast(new Ray(s.transform.parent.position+Vector3.up*5,Vector3.down),out var h,30),point=h.point.ToString()}),Newtonsoft.Json.Formatting.Indented);}finally{Object.DestroyImmediate(block);}}
}
