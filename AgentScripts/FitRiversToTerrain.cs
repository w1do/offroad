using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class FitRiversToTerrain
{
    const string Root="Assets/Game/Generated/RiverTerrainFit";
    struct Tri{public Vector3 a,b,c;public Tri(Vector3 a,Vector3 b,Vector3 c){this.a=a;this.b=b;this.c=c;}}
    static Dictionary<Vector2Int,List<Tri>> grid=new Dictionary<Vector2Int,List<Tri>>();
    static RiverCurrent[] rivers;
    static Vector2Int Cell(Vector3 p){return new Vector2Int(Mathf.FloorToInt(p.x/8),Mathf.FloorToInt(p.z/8));}
    static void IndexOriginalGround(GameObject forest,GameObject source)
    {
        var sources=source.GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.name);
        foreach(var f in forest.GetComponentsInChildren<MeshFilter>())
        {
            string name=f.GetComponent<Renderer>().sharedMaterial.name;
            if(!new[]{"Aerial_Grass","Terrain_Far","Ground_Dirt","Dirt_Road_LightSnow","Dirt_Road_Bare","Sloped_Rock","Tall_Cliff","Cobblestone"}.Any(name.Contains))continue;
            var mesh=sources[f.name].sharedMesh;var v=mesh.vertices;var t=mesh.triangles;
            for(int i=0;i<t.Length;i+=3)
            {
                var tri=new Tri(f.transform.TransformPoint(v[t[i]]),f.transform.TransformPoint(v[t[i+1]]),f.transform.TransformPoint(v[t[i+2]]));
                if(Vector3.Cross(tri.b-tri.a,tri.c-tri.a).y<0.0001f)continue;
                var min=Cell(Vector3.Min(tri.a,Vector3.Min(tri.b,tri.c)));var max=Cell(Vector3.Max(tri.a,Vector3.Max(tri.b,tri.c)));
                for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++)
                {var key=new Vector2Int(x,z);if(!grid.TryGetValue(key,out var list)){list=new List<Tri>();grid[key]=list;}list.Add(tri);}
            }
        }
    }
    static float Height(Vector3 p,float fallback)
    {
        float best=float.NegativeInfinity;
        if(grid.TryGetValue(Cell(p),out var list))foreach(var tri in list)
        {
            float ax=tri.b.x-tri.a.x,az=tri.b.z-tri.a.z,bx=tri.c.x-tri.a.x,bz=tri.c.z-tri.a.z;
            float determinant=ax*bz-az*bx;if(Mathf.Abs(determinant)<0.000001f)continue;
            float px=p.x-tri.a.x,pz=p.z-tri.a.z;float u=(px*bz-pz*bx)/determinant,w=(ax*pz-az*px)/determinant;
            if(u< -0.0001f||w< -0.0001f||u+w>1.0001f)continue;
            best=Mathf.Max(best,tri.a.y+u*(tri.b.y-tri.a.y)+w*(tri.c.y-tri.a.y));
        }
        return float.IsNegativeInfinity(best)?fallback:best;
    }
    static int UpstreamLength(RiverCurrent river,int flow,float level)
    {
        int length=0;
        for(int distance=1;distance<=30;distance++)
        {
            float x=-flow*distance;var p=river.transform.TransformPoint(new Vector3(x,0,river.ChannelOffset(x)));
            if(Height(p,level)<level+distance*0.012f-0.06f)break;length=distance;
        }
        return length;
    }
    static void FitProfile(RiverCurrent river)
    {
        float level=river.transform.position.y;
        int upPlus=UpstreamLength(river,1,level),upMinus=UpstreamLength(river,-1,level);
        float plus=Height(river.transform.TransformPoint(new Vector3(30,0,river.ChannelOffset(30))),level);
        float minus=Height(river.transform.TransformPoint(new Vector3(-30,0,river.ChannelOffset(-30))),level);
        int flow=upPlus>upMinus+3?1:upMinus>upPlus+3?-1:plus<minus?1:-1;
        int upstream=Mathf.Max(3,flow>0?upPlus:upMinus);
        river.profileStart=flow>0?-upstream:-30;river.profileEnd=flow>0?30:upstream;
        int count=Mathf.RoundToInt(river.profileEnd-river.profileStart)+1;
        river.surfaceHeights=new float[count];river.channelWidths=new float[count];
        float previous=float.PositiveInfinity;
        for(int step=0;step<count;step++)
        {
            int i=flow>0?step:count-1-step;float x=Mathf.Lerp(river.profileStart,river.profileEnd,i/(float)(count-1));
            var p=river.transform.TransformPoint(new Vector3(x,0,river.ChannelOffset(x)));float ground=Height(p,level);
            float desired=level-flow*x*0.012f;
            if(flow*x>0)desired=Mathf.Min(desired,ground-0.08f);
            desired=Mathf.Min(desired,previous-0.003f);river.surfaceHeights[i]=desired;previous=desired;
            float width=Mathf.Lerp(river.halfWidth,1.5f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,25,Mathf.Abs(x))));
            float end=Mathf.Min(x-river.profileStart,river.profileEnd-x);
            width*=Mathf.Lerp(0.015f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(end/2.5f)));
            river.channelWidths[i]=width;
        }
        river.currentSpeed=Mathf.Abs(river.currentSpeed)*flow;
        EditorUtility.SetDirty(river);
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        mesh.name=name;string path=Root+"/"+name+".asset";
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void RebuildChannel(RiverCurrent river,int id)
    {
        int rows=river.surfaceHeights.Length,columns=33;var water=new List<Vector3>();var waterUv=new List<Vector2>();var waterT=new List<int>();
        var bank=new List<Vector3>();var bankUv=new List<Vector2>();var bankT=new List<int>();
        for(int i=0;i<rows;i++)
        {
            float x=Mathf.Lerp(river.profileStart,river.profileEnd,i/(float)(rows-1)),width=river.HalfWidthAt(x),surface=river.SurfaceHeight(x),center=river.ChannelOffset(x);
            float end=Mathf.Min(x-river.profileStart,river.profileEnd-x);float depth=Mathf.Lerp(0.62f,0.35f,Mathf.Clamp01(Mathf.Abs(x)/18))*Mathf.SmoothStep(0,1,Mathf.Clamp01(end/1.5f));
            for(int j=0;j<columns;j++)
            {
                float u=j/(float)(columns-1),across=Mathf.Lerp(-width,width,u);
                water.Add(new Vector3(x,surface-river.transform.position.y,center+across));waterUv.Add(new Vector2(i/(float)(rows-1),u));
                across=Mathf.Lerp(-width-8,width+8,u);
                var p=river.transform.TransformPoint(new Vector3(x,0,center+across));float original=Height(p,surface+0.2f),y;
                if(Mathf.Abs(across)<=width)
                {
                    float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(width*0.65f,width,Mathf.Abs(across)));
                    y=Mathf.Lerp(surface-depth,surface+0.06f,edge);
                }
                else
                {float outer=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(across)-width)/8));y=Mathf.Lerp(Mathf.Max(surface+0.10f,original),original,outer);}
                y=Mathf.Lerp(original,y,Mathf.SmoothStep(0,1,Mathf.Clamp01(end/1.5f)));
                bank.Add(new Vector3(x,y-river.transform.position.y,center+across));bankUv.Add(new Vector2(x/3,across/3));
                int n=i*columns+j;
                if(i<rows-1&&j<columns-1){waterT.AddRange(new[]{n,n+1,n+columns,n+1,n+columns+1,n+columns});bankT.AddRange(new[]{n,n+1,n+columns,n+1,n+columns+1,n+columns});}
            }
        }
        var wm=new Mesh();wm.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;wm.SetVertices(water);wm.SetUVs(0,waterUv);wm.SetTriangles(waterT,0);wm.RecalculateNormals();wm.RecalculateBounds();
        river.transform.Find("Animated current surface").GetComponent<MeshFilter>().sharedMesh=SaveMesh(wm,"Terrain river water "+id);
        var bm=new Mesh();bm.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;bm.SetVertices(bank);bm.SetUVs(0,bankUv);bm.SetTriangles(bankT,0);bm.RecalculateNormals();bm.RecalculateTangents();bm.RecalculateBounds();
        var bed=river.transform.Find("Gravel ford bed - physical");var mesh=SaveMesh(bm,"Terrain river banks "+id);bed.GetComponent<MeshFilter>().sharedMesh=mesh;bed.GetComponent<MeshCollider>().sharedMesh=mesh;
        var box=river.GetComponent<BoxCollider>();float min=river.surfaceHeights.Min()-2,max=river.surfaceHeights.Max()+2;
        box.center=new Vector3((river.profileStart+river.profileEnd)/2,(min+max)/2-river.transform.position.y,0);box.size=new Vector3(river.profileEnd-river.profileStart+1,max-min,river.halfWidth*2+5);
        var material=river.transform.Find("Animated current surface").GetComponent<Renderer>().sharedMaterial;material.SetFloat("_CurrentSpeed",Mathf.Abs(river.currentSpeed));
        material.SetVector("_FlowDirection",new Vector4(river.transform.right.x*Mathf.Sign(river.currentSpeed),river.transform.right.z*Mathf.Sign(river.currentSpeed),0,0));
        material.SetColor("_ShallowColor",new Color(0.055f,0.20f,0.18f,1));material.SetColor("_DeepColor",new Color(0.025f,0.085f,0.095f,1));EditorUtility.SetDirty(material);
        Physics.SyncTransforms();
        foreach(var rock in river.GetComponentsInChildren<Transform>().Where(t=>t.name=="Riverbank stone").ToArray())
        {
            var p=rock.localPosition;if(p.x<river.MinimumAlong+2||p.x>river.MaximumAlong-2){UnityEngine.Object.DestroyImmediate(rock.gameObject);continue;}
            float side=Mathf.Sign(p.z-river.ChannelOffset(p.x));p.z=river.ChannelOffset(p.x)+side*(river.HalfWidthAt(p.x)+0.9f);p.y=river.SurfaceHeight(p.x)-river.transform.position.y+0.5f;
            var world=river.transform.TransformPoint(p);
            if(bed.GetComponent<MeshCollider>().Raycast(new Ray(world+Vector3.up*30,Vector3.down),out var hit,100))world.y=hit.point.y+0.09f;
            rock.position=world;
        }
    }
    static bool WetRoot(Vector3 p)
    {
        foreach(var r in rivers){var q=r.transform.InverseTransformPoint(p);if(q.x<r.MinimumAlong+0.5f||q.x>r.MaximumAlong-0.5f)continue;if(Mathf.Abs(q.z-r.ChannelOffset(q.x))<r.HalfWidthAt(q.x)+0.6f && p.y<r.SurfaceHeight(q.x)+0.5f)return true;}
        return false;
    }
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var scene=SceneManager.GetActiveScene();string backup="Assets/Game/Scenes/BeforeTerrainRiverRepair.unity";
        if(File.Exists(backup))throw new Exception("Repair backup exists; refusing repeated initial repair.");
        EditorSceneManager.SaveScene(scene,backup,true);Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        var forest=scene.GetRootGameObjects().First(g=>g.name.StartsWith("Forest - imported"));
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Forest/Dirt road scene.fbx");IndexOriginalGround(forest,source);
        rivers=UnityEngine.Object.FindObjectsByType<RiverCurrent>().OrderBy(r=>r.name).ToArray();foreach(var r in rivers)FitProfile(r);
        int removed=0;
        foreach(var f in forest.GetComponentsInChildren<MeshFilter>())
        {
            string name=f.GetComponent<Renderer>().sharedMaterial.name;
            if(name.Contains("Fence")||name.Contains("Wood_Log"))continue;
            var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);
            bool vegetation=name.Contains("Trunk_")||name.Contains("Tree_Atlas")||name.Contains("Vegetation")||name.Contains("Bush");
            if(vegetation)
            {
                var anchor=new List<Vector4>();mesh.GetUVs(2,anchor);var vertices=mesh.vertices;var t=mesh.triangles;var kept=new List<int>();
                for(int i=0;i<t.Length;i+=3)
                {
                    bool remove=false;
                    if(anchor.Count==vertices.Length)
                    {var a=anchor[t[i]];remove=WetRoot(new Vector3(a.x,a.z,a.y));}
                    if(name.Contains("Vegetation")||name.Contains("Bush"))remove|=WetRoot(f.transform.TransformPoint((vertices[t[i]]+vertices[t[i+1]]+vertices[t[i+2]])/3));
                    if(remove)removed++;else kept.AddRange(new[]{t[i],t[i+1],t[i+2]});
                }
                mesh.SetTriangles(kept,0);mesh.RecalculateBounds();
            }
            mesh=SaveMesh(mesh,"Fitted "+f.name);f.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(f);
            if(f.TryGetComponent<MeshCollider>(out var collider)){collider.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
        }
        for(int i=0;i<rivers.Length;i++)RebuildChannel(rivers[i],i);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed.");
        return string.Join("; ",rivers.Select(r=>r.name+" profile="+r.profileStart+".."+r.profileEnd+"m water="+r.surfaceHeights.Min().ToString("F2")+".."+r.surfaceHeights.Max().ToString("F2")))+"; removed channel vegetation triangles="+removed;
    }
}

