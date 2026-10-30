using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ValidateAllRoadSources
{
    public static string Main()
    {
        var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();var counts=new Dictionary<string,int>();int rays=0;
        foreach(var prefab in route.arms.SelectMany(a=>a.modules).Distinct())
        {
            var instance=UnityEngine.Object.Instantiate(prefab,new Vector3(0,300,0),Quaternion.identity);
            try
            {
                foreach(var section in instance.GetComponentsInChildren<MudRoadSection>())
                {
                    if(string.IsNullOrEmpty(section.sourceArchive))throw new Exception("Unassigned scan");counts.TryGetValue(section.sourceArchive,out int count);counts[section.sourceArchive]=count+1;
                    var surface=section.GetComponent<ForestDrivingSurface>();bool gravel=section.sourceArchive.Contains("gravel-road-2");
                    if(gravel&&(surface.cover!=ForestDrivingSurface.Cover.Gravel||section.GetComponent<SoftGroundRuts>().profile.maximumDepthMetres>.02f))throw new Exception("Gravel configured as bog");
                    var mesh=section.GetComponent<MeshFilter>().sharedMesh;var probe=new GameObject("TEMP visible scan probe");probe.transform.SetPositionAndRotation(section.transform.position,section.transform.rotation);
                    try{var c=probe.AddComponent<MeshCollider>();c.sharedMesh=mesh;for(int z=-3;z<=3;z++)for(int x=-2;x<=2;x++){var ray=new Ray(section.transform.TransformPoint(new Vector3(x*.7f,10,z)),Vector3.down);if(!c.Raycast(ray,out var a,20)||!section.GetComponent<MeshCollider>().Raycast(ray,out var b,20)||Mathf.Abs(a.point.y-b.point.y)>.003f)throw new Exception("Scan collision mismatch "+section.sourceArchive);rays++;}}
                    finally{UnityEngine.Object.DestroyImmediate(probe);}
                    var material=section.GetComponent<Renderer>().sharedMaterial;if(material.GetTexture("_MudMap")==null)throw new Exception("Missing scan texture");
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
        }
        if(counts.Count!=5)throw new Exception("Expected all 5 archives, found "+counts.Count);
        return "PASS all 5 road archives used by active route prefabs; "+rays+" render/collision rays; gravel distinct from wet mud. "+string.Join("; ",counts.Select(p=>p.Key+" x"+p.Value));
    }
}
