using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class InspectForestTrees
{
    public static string Main()
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Forest/Dirt road scene.fbx");string s="";
        foreach(var f in model.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>().sharedMaterial.name.StartsWith("Trunk_")))
        {
            var mesh=f.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;var p=Enumerable.Range(0,v.Length).ToArray();var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<v.Length;i++){var k=Vector3Int.RoundToInt(v[i]*1000);if(weld.TryGetValue(k,out int j))Union(p,i,j);else weld[k]=i;}
            for(int i=0;i<t.Length;i+=3){Union(p,t[i],t[i+1]);Union(p,t[i],t[i+2]);}
            int count=0;
            foreach(var g in Enumerable.Range(0,v.Length).GroupBy(i=>Find(p,i)))
            {var b=new Bounds(f.transform.TransformPoint(v[g.First()]),Vector3.zero);foreach(int i in g)b.Encapsulate(f.transform.TransformPoint(v[i]));if(b.size.y>9 && b.size.x<10 && b.size.z<10 && count++<12)s+=f.GetComponent<Renderer>().sharedMaterial.name+" "+b+"\n";}
        }
        return s;
    }
    static int Find(int[] p,int a){while(p[a]!=a){p[a]=p[p[a]];a=p[a];}return a;}
    static void Union(int[] p,int a,int b){p[Find(p,a)]=Find(p,b);}
}
