using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class TessellateRiverBanks
{
    struct Vertex{public Vector3 p;public Vector2 uv;public Vertex(Vector3 p,Vector2 uv){this.p=p;this.uv=uv;}}
    static RiverCurrent[] rivers;
    static List<Vector3> positions;
    static List<Vector2> uvs;
    static List<int> triangles;
    static Dictionary<(Vector3Int,Vector2Int),int> lookup;
    static MeshFilter current;
    static bool decorative;
    static Vertex Mid(Vertex a,Vertex b){return new Vertex((a.p+b.p)/2,(a.uv+b.uv)/2);}
    static bool Relevant(Vertex a,Vertex b,Vertex c)
    {
        foreach(var river in rivers)
        {
            var x=river.transform.InverseTransformPoint(a.p);var y=river.transform.InverseTransformPoint(b.p);var z=river.transform.InverseTransformPoint(c.p);
            float minX=Mathf.Min(x.x,Mathf.Min(y.x,z.x)),maxX=Mathf.Max(x.x,Mathf.Max(y.x,z.x));
            float minZ=Mathf.Min(x.z,Mathf.Min(y.z,z.z)),maxZ=Mathf.Max(x.z,Mathf.Max(y.z,z.z));
            if(maxX<river.MinimumAlong-2||minX>river.MaximumAlong+2||maxZ< -river.halfWidth-9||minZ>river.halfWidth+9)continue;
            return true;
        }
        return false;
    }
    static Vector3 Carve(Vector3 p)
    {
        foreach(var river in rivers)
        {
            var q=river.transform.InverseTransformPoint(p);float across=Mathf.Abs(q.z-river.ChannelOffset(q.x));
            float width=river.HalfWidthAt(q.x);
            if(q.x<river.MinimumAlong||q.x>river.MaximumAlong||across>width+6)continue;
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(width+6,Mathf.Max(0.02f,width-0.15f),across));
            float end=Mathf.Min(q.x-river.MinimumAlong,river.MaximumAlong-q.x);
            blend*=Mathf.SmoothStep(0,1,Mathf.Clamp01(end/1.5f));
            float depth=Mathf.Lerp(0.62f,0.35f,Mathf.Clamp01(Mathf.Abs(q.x)/18))*Mathf.SmoothStep(0,1,Mathf.Clamp01(end/1.5f));
            float bed=river.SurfaceHeight(q.x)-depth+Mathf.PerlinNoise(p.x*0.5f,p.z*0.5f)*0.015f;
            p.y=Mathf.Lerp(p.y,Mathf.Min(p.y,bed),blend);
        }
        return p;
    }
    static int Add(Vertex v)
    {
        Vector3 p=current.transform.InverseTransformPoint(Carve(v.p));
        var key=(Vector3Int.RoundToInt(p*10000),Vector2Int.RoundToInt(v.uv*10000));
        if(lookup.TryGetValue(key,out int existing))return existing;
        int index=positions.Count;positions.Add(p);uvs.Add(v.uv);lookup[key]=index;return index;
    }
    static void Triangle(Vertex a,Vertex b,Vertex c,int depth)
    {
        bool near=Relevant(a,b,c);
        if(near&&depth<8&&Mathf.Max((a.p-b.p).sqrMagnitude,Mathf.Max((b.p-c.p).sqrMagnitude,(c.p-a.p).sqrMagnitude))>3.2f)
        {
            var ab=Mid(a,b);var bc=Mid(b,c);var ca=Mid(c,a);
            Triangle(a,ab,ca,depth+1);Triangle(ab,b,bc,depth+1);Triangle(ca,bc,c,depth+1);Triangle(ab,bc,ca,depth+1);return;
        }
        if(decorative)
        {
            var center=(a.p+b.p+c.p)/3;
            foreach(var river in rivers){var q=river.transform.InverseTransformPoint(center);if(q.x>river.MinimumAlong+0.5f&&q.x<river.MaximumAlong-0.5f&&Mathf.Abs(q.z-river.ChannelOffset(q.x))<river.HalfWidthAt(q.x)+0.15f)return;}
        }
        triangles.Add(Add(a));triangles.Add(Add(b));triangles.Add(Add(c));
    }
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        rivers=UnityEngine.Object.FindObjectsByType<RiverCurrent>();
        var forest=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name.StartsWith("Forest - imported"));
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Forest/Dirt road scene.fbx");int surfaces=0,total=0;
        foreach(var filter in forest.GetComponentsInChildren<MeshFilter>())
        {
            string name=filter.GetComponent<Renderer>().sharedMaterial.name;
            if(name.Contains("Trunk_")||name.Contains("Tree_Atlas")||name.Contains("Vegetation")||name.Contains("Bush")||name.Contains("Fence")||name.Contains("Wood_Log"))continue;
            current=filter;decorative=name.Contains("Fallen_")||name.Contains("Rock_Decal")||name.Contains("Grass_Close")||name.Contains("Puddle");
            var original=source.GetComponentsInChildren<MeshFilter>().First(f=>f.name==filter.name).sharedMesh;
            var v=original.vertices;var uv=original.uv;var t=original.triangles;
            positions=new List<Vector3>();uvs=new List<Vector2>();triangles=new List<int>();lookup=new Dictionary<(Vector3Int,Vector2Int),int>();
            for(int i=0;i<t.Length;i+=3)Triangle(new Vertex(filter.transform.TransformPoint(v[t[i]]),uv[t[i]]),new Vertex(filter.transform.TransformPoint(v[t[i+1]]),uv[t[i+1]]),new Vertex(filter.transform.TransformPoint(v[t[i+2]]),uv[t[i+2]]),0);
            var generated=new Mesh();generated.name=filter.sharedMesh.name;generated.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;generated.SetVertices(positions);generated.SetUVs(0,uvs);generated.SetTriangles(triangles,0);generated.RecalculateNormals();generated.RecalculateTangents();generated.RecalculateBounds();
            EditorUtility.CopySerialized(generated,filter.sharedMesh);EditorUtility.SetDirty(filter.sharedMesh);UnityEngine.Object.DestroyImmediate(generated);
            if(filter.TryGetComponent<MeshCollider>(out var collider)){collider.sharedMesh=null;collider.sharedMesh=filter.sharedMesh;}
            surfaces++;total+=triangles.Count/3;
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return "Localized river-bank subdivision: "+surfaces+" surfaces, "+total+" total triangles; original FBX untouched.";
    }
}
