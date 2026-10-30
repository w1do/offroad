using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class InspectTruckFaults
{
    public static string Main()
    {
        string s="";
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Kamaz/KAMAZ.fbx");
        foreach(var f in model.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.EndsWith("_Tires") || f.name.EndsWith("_Wheels")))
        {
            var mesh=f.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;var parents=Enumerable.Range(0,v.Length).ToArray();
            var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<v.Length;i++) {var k=Vector3Int.RoundToInt(v[i]*10000);if(weld.TryGetValue(k,out int j)) Union(parents,i,j);else weld[k]=i;}
            for(int i=0;i<t.Length;i+=3){Union(parents,t[i],t[i+1]);Union(parents,t[i],t[i+2]);}
            var groups=Enumerable.Range(0,v.Length).GroupBy(i=>Find(parents,i));
            foreach(var g in groups.OrderByDescending(g=>g.Count()))
            {var b=new Bounds(f.transform.TransformPoint(v[g.First()]),Vector3.zero);foreach(int i in g)b.Encapsulate(f.transform.TransformPoint(v[i]));if(b.size.x>0.4f)s+=f.name+" count="+g.Count()+" "+b+"\n";}
        }
        foreach(var f in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var r=f.GetComponent<Renderer>();if(r==null || !r.sharedMaterial.name.StartsWith("Forest_"))continue;
            var m=f.sharedMesh;var v=m.vertices;var t=m.triangles;int up=0,down=0;
            for(int i=0;i<t.Length;i+=3){float y=Vector3.Cross(f.transform.TransformPoint(v[t[i+1]])-f.transform.TransformPoint(v[t[i]]),f.transform.TransformPoint(v[t[i+2]])-f.transform.TransformPoint(v[t[i]])).y;if(y>0)up++;else down++;}
            s+=r.sharedMaterial.name+" collider="+(f.GetComponent<Collider>()!=null)+" up="+up+" down="+down+"\n";
        }
        return s;
    }
    public static int Find(int[] p,int a){while(p[a]!=a){p[a]=p[p[a]];a=p[a];}return a;}
    public static void Union(int[] p,int a,int b){p[Find(p,a)]=Find(p,b);}
}
