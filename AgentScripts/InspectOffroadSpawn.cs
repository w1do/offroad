using System;
using System.Linq;
using UnityEngine;
public static class InspectOffroadSpawn
{
    public static string Main()
    {
        var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var soil=UnityEngine.Object.FindObjectsByType<SoftGroundRuts>().First(s=>s.persistentId=="main0/natural");
        var root=soil.transform.parent;Vector3 entry=root.position-root.forward*2;
        soil.GetComponent<MeshCollider>().Raycast(new Ray(entry+Vector3.up*10,Vector3.down),out var hit,20);
        GameObject clone=UnityEngine.Object.Instantiate(source.gameObject,hit.point+Vector3.up*.65f,Quaternion.LookRotation(root.forward));
        try
        {
            Physics.SyncTransforms();var rows=new System.Collections.Generic.List<object>();
            foreach(var c in clone.GetComponentsInChildren<Collider>().Where(c=>c is BoxCollider||c is CapsuleCollider||c is SphereCollider))
                foreach(var other in Physics.OverlapBox(c.bounds.center,c.bounds.extents))
                {
                    if(other.transform.IsChildOf(clone.transform)||other.isTrigger)continue;
                    if(Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out var direction,out var depth))
                        rows.Add(new{body=c.name,obstacle=other.name,depth,direction=direction.ToString(),bounds=c.bounds.ToString()});
                }
            return Newtonsoft.Json.JsonConvert.SerializeObject(new{spawn=clone.transform.position.ToString(),overlaps=rows},Newtonsoft.Json.Formatting.Indented);
        }
        finally{UnityEngine.Object.DestroyImmediate(clone);}
    }
}
