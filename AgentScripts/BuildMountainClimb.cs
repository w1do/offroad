using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class BuildMountainClimb
{
    const string Root="Assets/Game/Generated/MountainClimb";
    static Vector3 origin=new Vector3(-50,55,-180);
    static float[] zs={0,45,115,145,215,260,360,395,430,515,550,620,660,740};
    static float[] ys={0,0,12,12,-8,-8,24,32,32,-4,-4,16,16,-10};
    static GameObject world;
    static Material snow,ice,ground,bark,metal;
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        if(GameObject.Find("Mountain climb stage")!=null)throw new Exception("Stage already exists; refusing duplicate build.");
        string backup="Assets/Game/Scenes/BeforeMountainClimb.unity";
        if(File.Exists(backup))throw new Exception("Backup already exists.");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),backup,true);
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        snow=Mat("Packed snow",new Color(0.68f,0.73f,0.75f),0.25f);
        ice=Mat("Glazed ice",new Color(0.29f,0.43f,0.49f),0.92f);
        ground=Mat("Snowy mountains",new Color(0.78f,0.82f,0.83f),0.12f);
        metal=Mat("Steel chains",new Color(0.38f,0.43f,0.47f),0.65f);metal.SetFloat("_Metallic",0.85f);
        var dirt=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/Forest_Ground_Dirt.mat");
        if(dirt!=null){ground.SetTexture("_BaseMap",dirt.GetTexture("_BaseMap"));ground.SetTextureScale("_BaseMap",new Vector2(80,150));}
        world=new GameObject("Mountain climb stage");
        var stage=world.AddComponent<MountainClimbStage>();stage.centerline=new Vector3[371];
        for(int i=0;i<stage.centerline.Length;i++)stage.centerline[i]=Point(i*2,0);
        BuildTerrain();
        for(int start=0;start<740;start+=20)BuildRoad(start,Mathf.Min(start+20,740));
        int trees=BuildForest();
        var oldForest=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("Forest - imported"));
        if(oldForest!=null)oldForest.SetActive(false);
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();stage.truck=car;
        var direction=Point(18,0)-Point(12,0);car.transform.SetPositionAndRotation(Point(12,0)+Vector3.up*0.35f,Quaternion.LookRotation(direction));
        car.lowRange=false;car.reverseGear=false;car.testControl=false;car.testThrottle=0;car.testBrake=false;
        var chains=car.GetComponent<TruckChains>();if(chains==null)chains=car.gameObject.AddComponent<TruckChains>();
        chains.wheelChainVisuals=new GameObject[4];
        for(int i=2;i<6;i++)
        {
            var chain=new GameObject("Chains - driven wheel "+i);chain.transform.SetParent(car.visuals[i],false);
            chain.AddComponent<MeshFilter>().sharedMesh=ChainMesh(i%2==0?1:-1,i);
            chain.AddComponent<MeshRenderer>().sharedMaterial=metal;chain.SetActive(false);chains.wheelChainVisuals[i-2]=chain;
        }
        Sign(8,"MOUNTAIN ROUTE\nW: GAS  S: BRAKE\nL: LOW / HIGH",false);
        Sign(155,"STEEP DESCENT\nSLOW DOWN",true);
        Sign(238,"STOP HERE\nHOLD SPACE + T: CHAINS\nL: LOW RANGE",false);
        Sign(280,"ICE / SIDE SLOPE\nCONTROL THE SLIDE",true);
        Sign(423,"STEEP DESCENT\nUSE BRAKES",true);
        Sign(725,"TEST ROUTE FINISH",false);
        var camera=UnityEngine.Object.FindAnyObjectByType<DriveCamera>();camera.target=car.transform;camera.orbitYaw=0;camera.orbitPitch=18;camera.followDistance=12;
        camera.transform.position=car.transform.position-car.transform.forward*12+Vector3.up*5;camera.transform.LookAt(car.transform.position+Vector3.up*1.6f);
        RenderSettings.fogColor=new Color(0.72f,0.79f,0.83f);RenderSettings.fogDensity=0.0018f;
        foreach(var c in car.GetComponentsInChildren<Component>())if(c!=null)EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new Exception("Save failed.");
        Selection.activeGameObject=car.gameObject;
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(origin+new Vector3(0,20,340),Quaternion.Euler(35,-25,0),430);
        return "Saved 740m mountain stage, alternating hills/descents, banked ice climb, rear chains; forest instances="+trees+"; backup="+backup;
    }
    static float Height(float z)
    {z=Mathf.Clamp(z,0,740);for(int i=0;i<zs.Length-1;i++)if(z<=zs[i+1])return Mathf.Lerp(ys[i],ys[i+1],Mathf.SmoothStep(0,1,Mathf.InverseLerp(zs[i],zs[i+1],z)));return ys[ys.Length-1];}
    static float CenterX(float z){return 22*Mathf.Sin(z/95f)+9*Mathf.Sin(z/42f);}
    static float Bank(float z)
    {float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(260,285,z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(370,400,z)));return fade*8*Mathf.Sin((z-260)/24f)*Mathf.Deg2Rad;}
    static Vector3 Point(float z,float side){return origin+new Vector3(CenterX(z)+side,Height(z)+Mathf.Tan(Bank(z))*side,z);}
    static float TerrainY(float x,float z)
    {
        float side=x-CenterX(z);float edge=Mathf.Max(0,Mathf.Abs(side)-5.5f);
        float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/22));
        float hills=10+24*Mathf.PerlinNoise((x+430)/95,(z+60)/100)+15*Mathf.Sin(z/85+x/80);
        return Height(z)+Mathf.Tan(Bank(z))*Mathf.Clamp(side,-6,6)-0.09f+blend*(hills-12);
    }
    static void BuildTerrain()
    {
        int nx=91,nz=201;var v=new Vector3[nx*nz];var uv=new Vector2[v.Length];var t=new List<int>();
        for(int j=0;j<nz;j++)for(int i=0;i<nx;i++){float x=-180+i*4;float z=-30+j*4;int n=j*nx+i;v[n]=origin+new Vector3(x,TerrainY(x,z),z);uv[n]=new Vector2(i/(float)(nx-1),j/(float)(nz-1));if(i<nx-1&&j<nz-1)t.AddRange(new[]{n,n+nx,n+1,n+1,n+nx,n+nx+1});}
        var mesh=Mesh(v,uv,t,"Mountain terrain");Create("Snowy mountain terrain",mesh,ground,true);
    }
    static void BuildRoad(int start,int end)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
        for(int z=start;z<=end;z+=2){int n=v.Count;v.Add(Point(z,-5));v.Add(Point(z,5));uv.Add(new Vector2(0,z/8f));uv.Add(new Vector2(1,z/8f));if(z<end)t.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});}
        bool frozen=start>=260&&start<400;
        var go=Create((frozen?"ICE CLIMB ":"Snow road ")+start,Mesh(v.ToArray(),uv.ToArray(),t,"Road "+start),frozen?ice:snow,true);
        var surface=go.AddComponent<ForestDrivingSurface>();surface.cover=frozen?ForestDrivingSurface.Cover.Ice:ForestDrivingSurface.Cover.PackedSnow;
        surface.forwardGrip=frozen?0.10f:0.65f;surface.lateralGrip=frozen?0.35f:0.85f;surface.rollingResistance=0.01f;
        surface.chainsImproveGrip=true;surface.chainedForwardGrip=frozen?1.05f:0.8f;surface.chainedLateralGrip=frozen?0.85f:0.9f;
        if(start%40==0)foreach(float side in new[]{-5.6f,5.6f})
        {var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);post.name="Road marker";post.transform.SetParent(world.transform,false);post.transform.position=Point(start,side)+Vector3.up*0.65f;post.transform.localScale=new Vector3(0.12f,0.65f,0.12f);post.GetComponent<Renderer>().sharedMaterial=metal;}
    }
    static int BuildForest()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Forest/Dirt road scene.fbx");
        var trunks=source.GetComponentsInChildren<MeshFilter>().First(f=>f.GetComponent<Renderer>().sharedMaterial.name=="Trunk_Oak");
        var foliage=source.GetComponentsInChildren<MeshFilter>().First(f=>f.GetComponent<Renderer>().sharedMaterial.name=="Background_Tree_Atlas");
        var variants=new List<GameObject>();
        var centers=new[]{new Vector3(-112.67f,139.50f,-110.72f),new Vector3(-82.05f,130.01f,-129.14f),new Vector3(-75.65f,132.21f,-97.84f)};
        bark=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/Forest_Trunk_Oak.mat");
        var leaves=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/Forest_Background_Tree_Atlas.mat");
        for(int i=0;i<centers.Length;i++)
        {
            var tree=new GameObject("Imported forest tree variant "+i);tree.transform.SetParent(world.transform,false);
            Patch(trunks,centers[i],1.25f,tree.transform,bark,"Trunk "+i);
            Patch(foliage,centers[i],4.5f,tree.transform,leaves,"Needles "+i);
            var collider=tree.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,6,0);collider.height=12;collider.radius=0.38f;
            variants.Add(tree);tree.SetActive(false);
        }
        var random=new System.Random(4392);int count=0;
        for(int i=0;i<650;i++)
        {
            float z=-15+(float)random.NextDouble()*770;float side=(random.Next(2)==0?-1:1)*(10+(float)random.NextDouble()*140);float x=CenterX(z)+side;
            var tree=UnityEngine.Object.Instantiate(variants[random.Next(variants.Count)],world.transform);tree.name="Taiga tree "+count++;
            tree.transform.position=origin+new Vector3(x,TerrainY(x,z),z);tree.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
            tree.transform.localScale=Vector3.one*(0.75f+(float)random.NextDouble()*0.6f);tree.SetActive(true);
        }
        return count;
    }
    static void Patch(MeshFilter source,Vector3 center,float radius,Transform parent,Material material,string name)
    {
        var m=source.sharedMesh;var v=m.vertices;var n=m.normals;var uv=m.uv;var t=m.triangles;
        var vs=new List<Vector3>();var ns=new List<Vector3>();var uvs=new List<Vector2>();var ts=new List<int>();
        for(int i=0;i<t.Length;i+=3)
        {
            var a=source.transform.TransformPoint(v[t[i]]);var b=source.transform.TransformPoint(v[t[i+1]]);var c=source.transform.TransformPoint(v[t[i+2]]);var mid=(a+b+c)/3;
            if(new Vector2(mid.x-center.x,mid.z-center.z).sqrMagnitude>radius*radius || mid.y<center.y-0.5f || mid.y>center.y+30)continue;
            for(int k=0;k<3;k++){int index=t[i+k];vs.Add(source.transform.TransformPoint(v[index])-center);ns.Add(source.transform.TransformDirection(n[index]));uvs.Add(uv[index]);ts.Add(ts.Count);}
        }
        var mesh=Mesh(vs.ToArray(),uvs.ToArray(),ts,name);mesh.SetNormals(ns);
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    static Mesh ChainMesh(int innerDirection,int wheel)
    {
        var v=new List<Vector3>();var t=new List<int>();
        foreach(float center in new[]{0f,innerDirection*0.30f})
        {
            for(int j=0;j<24;j++)
            {
                float a=j*Mathf.PI*2/24,b=(j+1)*Mathf.PI*2/24;
                foreach(float x in new[]{center-0.135f,center+0.135f})Rod(new Vector3(x,Mathf.Cos(a)*0.514f,Mathf.Sin(a)*0.514f),new Vector3(x,Mathf.Cos(b)*0.514f,Mathf.Sin(b)*0.514f),0.008f,v,t);
                if(j%2==0)Rod(new Vector3(center-0.15f,Mathf.Cos(a)*0.522f,Mathf.Sin(a)*0.522f),new Vector3(center+0.15f,Mathf.Cos(a)*0.522f,Mathf.Sin(a)*0.522f),0.014f,v,t);
            }
        }
        return Mesh(v.ToArray(),new Vector2[v.Count],t,"Wheel chains "+wheel);
    }
    static void Rod(Vector3 a,Vector3 b,float radius,List<Vector3> v,List<int> t)
    {
        Vector3 d=(b-a).normalized;Vector3 u=Vector3.Cross(d,Mathf.Abs(d.y)<0.9f?Vector3.up:Vector3.right).normalized*radius;Vector3 w=Vector3.Cross(d,u).normalized*radius;int start=v.Count;
        for(int i=0;i<6;i++){float angle=i*Mathf.PI/3;var p=u*Mathf.Cos(angle)+w*Mathf.Sin(angle);v.Add(a+p);v.Add(b+p);}
        for(int i=0;i<6;i++){int next=(i+1)%6;t.AddRange(new[]{start+i*2,start+next*2,start+i*2+1,start+i*2+1,start+next*2,start+next*2+1});}
    }
    static void Sign(float z,string text,bool warning)
    {
        var go=new GameObject(text.Replace('\n',' '));go.transform.SetParent(world.transform,false);go.transform.position=Point(z,6.5f)+Vector3.up*2.1f;
        go.transform.rotation=Quaternion.LookRotation(-(Point(z+1,0)-Point(z,0)));
        var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=60;label.characterSize=0.13f;label.color=warning?new Color(1,0.45f,0.08f):new Color(0.1f,0.15f,0.2f);
    }
    static Material Mat(string name,Color color,float smooth)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(m,Root+"/"+name+".mat");return m;}
    static Mesh Mesh(Vector3[] v,Vector2[] uv,List<int> t,string name)
    {var m=new Mesh();m.name=name;m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.vertices=v;m.uv=uv;m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Root+"/"+name+".asset");return m;}
    static GameObject Create(string name,Mesh mesh,Material mat,bool collision)
    {var go=new GameObject(name);go.transform.SetParent(world.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;}
}
