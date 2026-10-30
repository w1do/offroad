using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

public static class BuildMountainDrive
{
    const string Root = "Assets/MountainDrive";
    const string Generated = Root + "/Generated";
    const int Samples = 2001;
    static Vector3[] route;
    static float[] distances;
    static Transform roadRoot;
    static Material asphalt, ice, broken, gravel, paint, steel, rock;

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/MountainPass.unity") != null)
            throw new Exception("MountainPass already exists. Refusing to overwrite scene.");
        Directory.CreateDirectory(Generated);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        route = new Vector3[Samples]; distances = new float[Samples];
        for (int i = 0; i < Samples; i++)
        {
            float t = i / (float)(Samples - 1);
            float z = 240 + t * 2700;
            route[i] = new Vector3(1600 + 650 * Mathf.Sin(t * Mathf.PI * 5 - 0.7f), 68 + t * 252 + 8 * Mathf.Sin(t * Mathf.PI * 4), z);
            if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(route[i], route[i - 1]);
        }
        asphalt = SurfaceMaterial("Asphalt", "Asphalt006", Color.white, 0.11f);
        broken = SurfaceMaterial("Broken asphalt", "Asphalt012", new Color(0.78f,0.75f,0.70f), 0.06f);
        ice = SurfaceMaterial("Black ice", "Ice004", new Color(0.55f,0.68f,0.73f), 0.90f);
        ice.SetFloat("_Metallic",0.18f);
        gravel = SurfaceMaterial("Gravel shoulder", "Ground037", new Color(0.74f,0.68f,0.52f), 0.04f);
        rock = SurfaceMaterial("Exposed rock", "Rock030", new Color(0.75f,0.78f,0.72f),0.08f);
        paint = Plain("Road marking",new Color(0.91f,0.89f,0.72f),0.12f);
        steel = Plain("Galvanized guardrail", new Color(0.46f,0.52f,0.54f),0.47f);steel.SetFloat("_Metallic",0.7f);
        BuildTerrain();
        roadRoot = new GameObject("Serpentine - asphalt, ice, damaged road").transform;
        var track = roadRoot.gameObject.AddComponent<MountainRoute>(); track.points=route; track.length=distances[Samples-1];
        BuildRoad();
        BuildRocks();
        BuildLighting();
        BuildCar(track);
        var overview = new GameObject("Overview Camera - disabled").AddComponent<Camera>();
        overview.transform.position = new Vector3(3210,1570,-510);overview.transform.LookAt(new Vector3(1600,200,1510));
        overview.farClipPlane=9000; overview.fieldOfView=48;overview.enabled=false;
        var view=SceneView.lastActiveSceneView;
        if(view!=null) view.LookAt(new Vector3(1600,190,1500),Quaternion.Euler(48,-24,0),2050);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene,Root+"/MountainPass.unity");
        // Keep the previous scenes in the project; unload only clean scenes to avoid losing unsaved work.
        for(int i=SceneManager.sceneCount-1;i>=0;i--)
        {
            var other=SceneManager.GetSceneAt(i);
            if(other!=scene && !other.isDirty && !string.IsNullOrEmpty(other.path)) EditorSceneManager.CloseScene(other,true);
        }
        var builds=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if(!builds.Exists(x=>x.path==scene.path))builds.Insert(0,new EditorBuildSettingsScene(scene.path,true));
        EditorBuildSettings.scenes=builds.ToArray();
        return "Saved "+scene.path+" | "+track.length.ToString("F0")+" m serpentine | 3200m mountains | 4WD SUV | 3 road surfaces";
    }

    static Texture2D Texture(string id,string suffix)
    {
        string path=Root+"/Art/"+id+"/"+id+"_1K-JPG_"+suffix+".jpg";
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer!=null)
        {
            bool normal=suffix=="NormalGL";
            bool change=normal && importer.textureType!=TextureImporterType.NormalMap;
            if(change){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
        }
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null)throw new Exception("Missing texture "+path);
        return texture;
    }

    static Material Plain(string name,Color color,float smoothness)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=name;
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smoothness);
        AssetDatabase.CreateAsset(material,Generated+"/"+name+".mat");return material;
    }

    static Material SurfaceMaterial(string name,string id,Color color,float smoothness)
    {
        var m=Plain(name,color,smoothness);m.SetTexture("_BaseMap",Texture(id,"Color"));
        m.SetTexture("_BumpMap",Texture(id,"NormalGL"));m.SetFloat("_BumpScale",0.7f);m.EnableKeyword("_NORMALMAP");
        return m;
    }

    static float NaturalHeight(float x,float z)
    {
        float t=(z-240)/2700;
        float floor=42+Mathf.Clamp01(t)*252;
        float valleyX=1600+140*Mathf.Sin(z/630);
        float side=Mathf.Abs(x-valleyX)/1500;
        float ridges=Mathf.Pow(1-Mathf.Abs(2*Mathf.PerlinNoise(x/650+19,z/590+43)-1),2);
        float mountains=Mathf.Pow(side,1.55f)*510+ridges*(23+side*145);
        float hills=Mathf.PerlinNoise(x/240+7,z/270+18)*24+Mathf.PerlinNoise(x/75,z/75)*8;
        return floor+mountains+hills;
    }

    static void BuildTerrain()
    {
        const int resolution=1025;const float scale=3200f/1024;
        var data=new TerrainData {heightmapResolution=resolution,size=new Vector3(3200,950,3200),alphamapResolution=512,baseMapResolution=1024};
        var heights=new float[resolution,resolution];var near=new float[resolution,resolution];var roadHeight=new float[resolution,resolution];
        for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++){heights[z,x]=NaturalHeight(x*scale,z*scale)/950;near[z,x]=10000;}
        for(int i=0;i<route.Length;i++)
        {
            Vector3 p=route[i];int cx=Mathf.RoundToInt(p.x/scale),cz=Mathf.RoundToInt(p.z/scale);
            for(int z=Mathf.Max(0,cz-79);z<=Mathf.Min(resolution-1,cz+79);z++)
                for(int x=Mathf.Max(0,cx-79);x<=Mathf.Min(resolution-1,cx+79);x++)
                {
                    float d=new Vector2(x*scale-p.x,z*scale-p.z).magnitude;
                    if(d<near[z,x]){near[z,x]=d;roadHeight[z,x]=p.y-0.65f;}
                }
        }
        for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
        {
            float d=near[z,x];
            float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,240,d));
            float target=roadHeight[z,x]+Mathf.Max(0,d-8)*0.5f;
            heights[z,x]=Mathf.Lerp(heights[z,x],target/950,blend);
        }
        data.SetHeights(0,0,heights);
        string[] ids={"Ground037","Rock030"};var layers=new TerrainLayer[3];
        for(int i=0;i<3;i++)
        {
            string id=ids[i==0?0:1];
            layers[i]=new TerrainLayer {diffuseTexture=Texture(id,"Color"),normalMapTexture=Texture(id,"NormalGL"),tileSize=new Vector2(i==0?12:19,i==0?12:19),normalScale=0.8f,smoothness=0.08f};
            if(i==0) layers[i].diffuseRemapMax=new Vector4(0.78f,0.89f,0.64f,1);
            if(i==2) layers[i].diffuseRemapMax=new Vector4(1.35f,1.38f,1.35f,1);
            AssetDatabase.CreateAsset(layers[i],Generated+"/MountainLayer"+i+".terrainlayer");
        }
        data.terrainLayers=layers;
        var alpha=new float[512,512,3];
        for(int z=0;z<512;z++)for(int x=0;x<512;x++)
        {
            float u=x/511f,v=z/511f;
            float steep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,49,data.GetSteepness(u,v)));
            float high=Mathf.SmoothStep(0,1,Mathf.InverseLerp(480,790,data.GetInterpolatedHeight(u,v)));
            float patch=Mathf.PerlinNoise(u*39+9,v*39+7)*0.42f;
            float r=Mathf.Clamp01(steep+patch);
            alpha[z,x,0]=(1-r)*(1-high);alpha[z,x,1]=r*(1-high);alpha[z,x,2]=high;
        }
        data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();AssetDatabase.CreateAsset(data,Generated+"/MountainTerrain.asset");
        var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();terrain.name="Mountain Range - sculpted road cut";
        terrain.heightmapPixelError=6;terrain.basemapDistance=2400;terrain.drawInstanced=true;
        var terrainMaterial=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));AssetDatabase.CreateAsset(terrainMaterial,Generated+"/Mountains.mat");terrain.materialTemplate=terrainMaterial;
    }

    static DriveSurface Surface(int i)
    {
        float t=i/(float)(Samples-1);
        if((t>=0.18f&&t<0.27f)||(t>=0.60f&&t<0.69f)||(t>=0.89f&&t<0.95f))return DriveSurface.Ice;
        if((t>=0.33f&&t<0.44f)||(t>=0.74f&&t<0.84f))return DriveSurface.Broken;
        return DriveSurface.Asphalt;
    }

    static Vector3 Right(int i)
    {
        var tangent=route[Mathf.Min(Samples-1,i+1)]-route[Mathf.Max(0,i-1)];tangent.y=0;return Vector3.Cross(Vector3.up,tangent).normalized;
    }

    static float Rough(int i,float offset)
    {
        if(Surface(i)!=DriveSurface.Broken)return 0;
        float t=i/(float)(Samples-1);
        // Fade both ends of the damaged zones so there is no collider step at a material seam.
        float edge=t<0.5f?Mathf.Min(t-0.33f,0.44f-t):Mathf.Min(t-0.74f,0.84f-t);
        float fade=Mathf.Clamp01(edge*350);
        return fade*((Mathf.PerlinNoise(distances[i]*0.22f,offset*0.37f+17)-0.5f)*0.27f-0.13f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(distances[i]*0.43f+offset)),8));
    }

    static Vector3 RoadPoint(int i,float offset,float up=0)
    {return route[i]+Right(i)*offset+Vector3.up*(Rough(i,offset)+up);}

    static GameObject MeshObject(string name,List<Vector3> verts,List<int> tris,List<Vector2> uv,Material mat,bool collision)
    {
        var mesh=new Mesh();mesh.name=name;mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Generated+"/"+name+".asset");
        var go=new GameObject(name);go.transform.SetParent(roadRoot,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
    }

    static void Quad(List<int> tris,int a,int b,int c,int d)
    {tris.Add(a);tris.Add(c);tris.Add(b);tris.Add(b);tris.Add(c);tris.Add(d);}

    static void Ribbon(string name,int start,int end,float left,float right,float up,Material material,bool collision,DriveSurface surface)
    {
        var v=new List<Vector3>();var tr=new List<int>();var uv=new List<Vector2>();
        for(int i=start;i<=end;i++)
        {
            v.Add(RoadPoint(i,left,up));v.Add(RoadPoint(i,right,up));uv.Add(new Vector2(left/4.5f,distances[i]/4.5f));uv.Add(new Vector2(right/4.5f,distances[i]/4.5f));
            if(i>start){int n=v.Count-4;Quad(tr,n,n+1,n+2,n+3);}
        }
        var obj=MeshObject(name,v,tr,uv,material,collision);if(collision)obj.AddComponent<RoadSurface>().kind=surface;
    }

    static void BuildRoad()
    {
        int start=0,index=0;
        while(start<Samples-1)
        {
            var kind=Surface(start);int end=Mathf.Min(start+65,Samples-1);
            for(int i=start+1;i<end;i++)if(Surface(i)!=kind){end=i;break;}
            var verts=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
            const int across=9;
            for(int i=start;i<=end;i++)for(int j=0;j<across;j++)
            {
                float offset=Mathf.Lerp(-4.5f,4.5f,j/(float)(across-1));verts.Add(RoadPoint(i,offset));uv.Add(new Vector2((offset+4.5f)/4.5f,distances[i]/4.5f));
                if(i>start&&j>0){int n=verts.Count-1;Quad(tris,n-across-1,n-across,n-1,n);}
            }
            var road=MeshObject("Road_"+index+"_"+kind,verts,tris,uv,kind==DriveSurface.Ice?ice:kind==DriveSurface.Broken?broken:asphalt,true);road.AddComponent<RoadSurface>().kind=kind;
            Ribbon("ShoulderL_"+index,start,end,-5.8f,-4.5f,-0.035f,gravel,true,DriveSurface.Gravel);
            Ribbon("ShoulderR_"+index,start,end,4.5f,5.8f,-0.035f,gravel,true,DriveSurface.Gravel);
            Ribbon("EdgeL_"+index,start,end,-4.08f,-3.94f,0.025f,paint,false,kind);
            Ribbon("EdgeR_"+index,start,end,3.94f,4.08f,0.025f,paint,false,kind);
            start=end;index++;
        }
        // Broken centre line, using the same road height including potholes.
        for(int i=0;i<Samples-3;i+=9) Ribbon("Centre_"+i,i,Mathf.Min(i+3,Samples-1),-0.075f,0.075f,0.03f,paint,false,Surface(i));
        for(int side=-1;side<=1;side+=2)
        {
            var verts=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<Samples;i+=2)
            {
                var p=RoadPoint(i,side*5.45f);
                verts.Add(p+Vector3.up*0.52f);verts.Add(p+Vector3.up*0.91f);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(0,1));
                if(verts.Count>2){int n=verts.Count-4;Quad(tris,n,n+1,n+2,n+3);Quad(tris,n+1,n,n+3,n+2);}
            }
            MeshObject("Guardrail_"+side,verts,tris,uv,steel,true);
            for(int i=0;i<Samples;i+=7)
            {
                var p=RoadPoint(i,side*5.45f,0.43f);
                Box("Guardrail post",p,new Vector3(0.11f,0.86f,0.11f),steel,roadRoot,false);
            }
        }
        Sign(15,"MOUNTAIN PASS",new Color(0.10f,0.30f,0.24f));
        Sign(345,"ICE AHEAD",new Color(0.12f,0.39f,0.63f));
        Sign(645,"ROUGH ROAD",new Color(0.66f,0.35f,0.10f));
        Sign(1180,"ICE AHEAD",new Color(0.12f,0.39f,0.63f));
        Sign(1460,"ROUGH ROAD",new Color(0.66f,0.35f,0.10f));
        Sign(1970,"SUMMIT / FINISH",new Color(0.10f,0.30f,0.24f));
    }

    static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,Transform parent,bool collision)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,true);obj.transform.position=p;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collision)UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
    }

    static void Sign(int i,string label,Color color)
    {
        var p=RoadPoint(i,7);var root=new GameObject(label).transform;root.SetParent(roadRoot);root.position=p;
        root.rotation=Quaternion.LookRotation((route[Mathf.Max(0,i-2)]-route[i]).normalized);
        Box("Pole",p+Vector3.up*1.4f,new Vector3(.1f,2.8f,.1f),steel,root,false);
        var board=Box("Warning board",p+Vector3.up*2.65f,new Vector3(3.6f,.85f,.12f),Plain("Sign"+i,color,.1f),root,false);board.transform.rotation=root.rotation;
        var text=new GameObject("Sign text").AddComponent<TextMesh>();text.transform.SetParent(root,false);text.transform.localPosition=new Vector3(0,2.65f,-.08f);text.transform.localRotation=Quaternion.Euler(0,180,0);text.text=label;text.fontSize=48;text.characterSize=.075f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
    }

    static void BuildRocks()
    {
        var terrain=UnityEngine.Object.FindAnyObjectByType<Terrain>();
        // Find the terrain belonging to the active scene if earlier landscapes are also loaded.
        foreach(var t in UnityEngine.Object.FindObjectsByType<Terrain>())if(t.gameObject.scene==SceneManager.GetActiveScene())terrain=t;
        var parent=new GameObject("Scree and mountain boulders").transform;var random=new System.Random(2941);
        for(int i=0;i<430;i++)
        {
            int r=random.Next(0,Samples);float side=random.Next(0,2)==0?-1:1;
            var p=route[r]+Right(r)*side*(13+(float)random.NextDouble()*190);
            p.y=terrain.SampleHeight(p)-.6f;
            var boulder=GameObject.CreatePrimitive(PrimitiveType.Sphere);boulder.name="Rock";boulder.transform.SetParent(parent);boulder.transform.position=p;
            float s=1.3f+(float)random.NextDouble()*7;boulder.transform.localScale=new Vector3(s*1.4f,s*.75f,s);
            boulder.transform.rotation=Quaternion.Euler(random.Next(0,40),random.Next(0,360),random.Next(0,30));boulder.GetComponent<Renderer>().sharedMaterial=rock;
        }
    }

    static void BuildLighting()
    {
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.3f;sun.color=new Color(1,.95f,.85f);sun.transform.rotation=Quaternion.Euler(36,-48,0);sun.shadows=LightShadows.Soft;
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.55f,.67f,.80f);RenderSettings.ambientEquatorColor=new Color(.39f,.43f,.43f);RenderSettings.ambientGroundColor=new Color(.20f,.23f,.17f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.65f,.74f,.79f);RenderSettings.fogDensity=.00024f;
        var skyShader=Shader.Find("Skybox/Procedural");if(skyShader!=null){var sky=new Material(skyShader);sky.SetFloat("_AtmosphereThickness",.85f);sky.SetColor("_SkyTint",new Color(.45f,.52f,.60f));sky.SetColor("_GroundColor",new Color(.35f,.38f,.34f));AssetDatabase.CreateAsset(sky,Generated+"/AlpineSky.mat");RenderSettings.skybox=sky;}
        QualitySettings.shadowDistance=450;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
    }

    static void BuildCar(MountainRoute track)
    {
        var car=new GameObject("Player - Kenney SUV 4WD");
        var rb=car.AddComponent<Rigidbody>();rb.mass=1450;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.linearDamping=.05f;rb.angularDamping=.65f;
        var box=car.AddComponent<BoxCollider>();box.center=new Vector3(0,1.12f,.08f);box.size=new Vector3(2.1f,1.22f,3.8f);
        var controller=car.AddComponent<MountainSUV>();controller.route=track;
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Art/Kenney/Models/FBX format/suv.fbx"),car.transform);model.name="SUV visual (Kenney CC0)";model.transform.localScale=Vector3.one*1.6f;
        var carMat=Plain("SUV colormap",Color.white,.3f);carMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Kenney/Models/FBX format/Textures/colormap.png"));
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=carMat;
        string[] names={"wheel-front-left","wheel-front-right","wheel-back-left","wheel-back-right"};
        controller.wheels=new WheelCollider[4];controller.wheelVisuals=new Transform[4];
        for(int i=0;i<4;i++)
        {
            var original=model.transform.Find(names[i]);var center=original.GetComponent<Renderer>().bounds.center;
            var visual=new GameObject("Wheel visual "+i).transform;visual.SetParent(car.transform,false);visual.position=center;original.SetParent(visual,true);controller.wheelVisuals[i]=visual;
            var wheel=new GameObject("WheelCollider "+i).AddComponent<WheelCollider>();wheel.transform.SetParent(car.transform,false);wheel.transform.position=center+Vector3.up*.12f;
            wheel.radius=.48f;wheel.mass=32;wheel.suspensionDistance=.32f;wheel.forceAppPointDistance=.16f;
            var spring=wheel.suspensionSpring;spring.spring=38000;spring.damper=4800;spring.targetPosition=.5f;wheel.suspensionSpring=spring;
            controller.wheels[i]=wheel;
        }
        // Rugged roof basket and front bumper complement the imported SUV.
        var rackMat=Plain("SUV black accessories",new Color(.055f,.065f,.062f),.2f);
        for(int side=-1;side<=1;side+=2)Box("Roof rack rail",new Vector3(side*.77f,2.16f,-.05f),new Vector3(.09f,.13f,1.6f),rackMat,car.transform,false);
        for(int j=0;j<4;j++)Box("Roof rack crossbar",new Vector3(0,2.16f,-.75f+j*.48f),new Vector3(1.6f,.09f,.07f),rackMat,car.transform,false);
        Box("Front bullbar",new Vector3(0,.78f,2.1f),new Vector3(1.85f,.13f,.12f),rackMat,car.transform,false);
        car.transform.SetPositionAndRotation(route[8]+Right(8)*1.9f+Vector3.up*.42f,Quaternion.LookRotation(route[11]-route[8]));
        var camera=new GameObject("Driving Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=65;camera.nearClipPlane=.1f;camera.farClipPlane=6500;camera.allowHDR=true;camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position=car.transform.position-car.transform.forward*8.8f+Vector3.up*4.3f;camera.transform.LookAt(car.transform.position+Vector3.up*1.5f+car.transform.forward*5);
        camera.gameObject.AddComponent<DriveCamera>().target=car.transform;
        camera.gameObject.AddComponent<DriveHUD>().car=controller;
        PrefabUtility.SaveAsPrefabAsset(car,Generated+"/DriveableSUV.prefab");Selection.activeGameObject=car;
    }
}
