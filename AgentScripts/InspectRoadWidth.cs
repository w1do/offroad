using System.Linq;
using UnityEngine;
public static class InspectRoadWidth
{
 static string MaterialAt(RaycastHit hit){var f=hit.collider.GetComponent<MeshFilter>();var r=hit.collider.GetComponent<Renderer>();if(f==null||r==null)return "?";int n=hit.triangleIndex*3;for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++){int count=(int)f.sharedMesh.GetIndexCount(sub);if(n<count)return r.sharedMaterials[sub].name;n-=count;}return "?";}
 public static string Main(){Physics.SyncTransforms();var s=Object.FindObjectsByType<MudRoadSection>().First(x=>x.GetComponent<SoftGroundRuts>().persistentId=="main0/natural");return string.Join("\n",Enumerable.Range(-16,33).Select(i=>{float x=i*.5f;var ray=new Ray(s.transform.TransformPoint(new Vector3(x,0,0))+Vector3.up*10,Vector3.down);var hits=Physics.RaycastAll(ray,30).Where(h=>h.collider.GetComponent<ForestGroundPatch>()!=null||h.collider.GetComponent<MudRoadSection>()!=null).OrderBy(h=>h.distance).ToArray();return x+": "+(hits.Length==0?"none":hits[0].collider.name+" material="+MaterialAt(hits[0])+" cover="+(hits[0].collider.GetComponent<ForestGroundPatch>()?.CoverAt(hits[0].point)??-1));}));}
}
