using System.Linq;
using UnityEngine;
public static class InspectFullWidthMud
{
 public static string Main()=>Newtonsoft.Json.JsonConvert.SerializeObject(Object.FindObjectsByType<MudRoadSection>().Select(s=>new{s.name,id=s.GetComponent<SoftGroundRuts>().persistentId,half=s.halfSize.ToString(),width=s.mudHalfWidthMetres,source=s.sourceArchive,mesh=UnityEditor.AssetDatabase.GetAssetPath(s.GetComponent<MeshFilter>().sharedMesh),children=s.transform.Cast<Transform>().Select(c=>new{c.name,active=c.gameObject.activeSelf})}),Newtonsoft.Json.Formatting.Indented);
}
