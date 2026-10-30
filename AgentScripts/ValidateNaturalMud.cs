using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ValidateNaturalMud
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play");
        var sections=UnityEngine.Object.FindObjectsByType<MudRoadSection>().Where(s=>s.GetComponent<SoftGroundRuts>().persistentId.StartsWith("main")).ToArray();
        int rays=0;float mismatch=0;float firm=0,soft=0;
        foreach(var section in sections)
        {
            var probe=new GameObject("TEMP independent visible road mesh");probe.transform.SetPositionAndRotation(section.transform.position,section.transform.rotation);probe.transform.localScale=section.transform.lossyScale;
            try
            {
                var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=section.GetComponent<MeshFilter>().sharedMesh;
                for(int z=0;z<15;z++)for(int x=0;x<11;x++)
                {
                    var p=section.transform.TransformPoint(new Vector3(Mathf.Lerp(-section.halfSize.x+.03f,section.halfSize.x-.03f,(float)x/10),10,Mathf.Lerp(-section.halfSize.y+.03f,section.halfSize.y-.03f,(float)z/14)));
                    var ray=new Ray(p,Vector3.down);
                    if(!collider.Raycast(ray,out var visible,25)||!section.GetComponent<MeshCollider>().Raycast(ray,out var physical,25))throw new Exception("Missing physical/visible road");
                    mismatch=Mathf.Max(mismatch,Mathf.Abs(visible.point.y-physical.point.y));rays++;
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(probe);}
            var wet=section.transform.TransformPoint(new Vector3(section.laneCentreMetres+.8f,0,0));var edge=section.transform.TransformPoint(new Vector3(-section.halfSize.x+.08f,0,0));
            firm=section.GripAt(edge);soft=section.GripAt(wet);if(firm<=soft+.2f)throw new Exception("No useful trajectory grip difference");
        }
        if(sections.Length!=2||mismatch>.002f)throw new Exception("Road collision mismatch "+mismatch);
        if(ShaderUtil.GetShaderMessages(Shader.Find("Game/Living Forest")).Any(m=>m.severity.ToString()=="Error"))throw new Exception("Forest shader compile errors");
        return "PASS 2 main sections, "+rays+" independent render/collision rays, maximum difference="+mismatch.ToString("F5")+"m; firm/soft grip multiplier="+firm.ToString("F2")+"/"+soft.ToString("F2")+"; shader compiles.";
    }
}
