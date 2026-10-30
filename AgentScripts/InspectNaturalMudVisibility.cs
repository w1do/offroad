using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class InspectNaturalMudVisibility
{
    public static string Main()
    {
        var section=Object.FindObjectsByType<MudRoadSection>().First(s=>s.GetComponent<SoftGroundRuts>().persistentId=="main0/natural");var point=section.transform.position;var hits=new List<string>();
        foreach(var f in Object.FindObjectsByType<MeshFilter>())
        {
            var renderer=f.GetComponent<Renderer>();if(renderer==null||!renderer.enabled||f.sharedMesh==null)continue;
            var b=renderer.bounds;if(point.x<b.min.x||point.x>b.max.x||point.z<b.min.z||point.z>b.max.z||b.min.y>point.y+1)continue;
            var probe=new GameObject("TEMP render ray");probe.transform.SetPositionAndRotation(f.transform.position,f.transform.rotation);probe.transform.localScale=f.transform.lossyScale;
            try{var c=probe.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;if(c.Raycast(new Ray(point+Vector3.up*10,Vector3.down),out var hit,20))hits.Add(hit.point.y.ToString("F4")+" "+f.name+" mats="+string.Join(",",renderer.sharedMaterials.Select(m=>m.name)));}
            finally{Object.DestroyImmediate(probe);}
        }
        return string.Join("\n",hits.OrderByDescending(h=>h));
    }
}
