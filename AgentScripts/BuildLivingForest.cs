using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuildLivingForest
{
    const string Root="Assets/Game/Generated/LivingForest";
    static readonly Vector3[] FordCenters={new Vector3(-40.62f,43.15f,44.16f),new Vector3(-72.29f,40.28f,45.86f)};
    static readonly float[] Widths={5.5f,4.4f};
    static List<Vector3> anchors=new List<Vector3>();
    static Dictionary<Vector2Int,List<Vector3>> anchorGrid=new Dictionary<Vector2Int,List<Vector3>>();
    static GameObject root;
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before authoring.");
        if(GameObject.Find("Living forest and river fords")!=null)throw new Exception("Already built; refusing duplicate world.");
        var scene=SceneManager.GetActiveScene();
        var forest=scene.GetRootGameObjects().First(g=>g.name.StartsWith("Forest - imported"));
        var shader=Shader.Find("Game/Living Forest");var waterShader=Shader.Find("Game/Forest River");
        if(shader==null||waterShader==null)throw new Exception("Shaders not imported.");
        foreach(var s in new[]{shader,waterShader}){var errors=ShaderUtil.GetShaderMessages(s).Where(m=>m.severity.ToString()=="Error").ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(m=>m.message)));}
        string backup="Assets/Game/Scenes/BeforeLivingForest.unity";
        if(File.Exists(backup))throw new Exception("Backup exists. Do not overwrite later work.");
        EditorSceneManager.SaveScene(scene,backup,true);Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        root=new GameObject("Living forest and river fords");
        var wind=root.AddComponent<ForestWind>();
        var originals=forest.GetComponentsInChildren<MeshFilter>();
        foreach(var f in originals.Where(f=>f.GetComponent<Renderer>().sharedMaterial.name.Contains("Trunk_")))CollectAnchors(f);
        foreach(var anchor in anchors){var key=Cell(anchor);if(!anchorGrid.TryGetValue(key,out var list)){list=new List<Vector3>();anchorGrid[key]=list;}list.Add(anchor);}
        int windMeshes=0,carvedMeshes=0;var materials=new Dictionary<Material,Material>();
        foreach(var filter in originals)
        {
            var renderer=filter.GetComponent<MeshRenderer>();var original=renderer.sharedMaterial;string name=original.name;
            bool trunk=name.Contains("Trunk_");
            bool foliage=name.Contains("Tree_Atlas")||name.Contains("Vegetation")||name.Contains("Bush");
            bool decorative=name.Contains("Fallen_")||name.Contains("Rock_Decal")||name.Contains("Grass_Close")||name.Contains("Puddle");
            bool ground=!trunk&&!foliage&&!name.Contains("Fence")&&!name.Contains("Wood_Log");
            if(!materials.TryGetValue(original,out var mat))
            {
                mat=new Material(original);mat.name=original.name+"_Living";mat.shader=shader;mat.SetFloat("_BumpScale",0);
                mat.SetFloat("_TreeSway",trunk||name.Contains("Tree_Atlas")?0.014f:0);
                mat.SetFloat("_LeafFlutter",foliage?(name.Contains("Vegetation")?0.045f:0.025f):0);
                mat.SetFloat("_FoliageLight",foliage?0.42f:0);
                if(name.Contains("Forest_Dirt_Road_LightSnow")||name.Contains("Dirt_Road_Bare"))SetPbr(mat,"gravel_floor_04",0.4f);
                if(name.Contains("Ground_Dirt")||name.Contains("Grass_Close"))SetPbr(mat,"forest_ground_04",0.3125f);
                if(trunk)
                {
                    string normal=name.Contains("Birch")?"Trunk_Birch_Normal.png":"Trunk_Oak_Normal.png";
                    mat.SetTexture("_BumpMap",Normal("Assets/Game/Art/Forest/textures/"+normal));mat.SetFloat("_BumpScale",0.55f);
                }
                AssetDatabase.CreateAsset(mat,Root+"/"+mat.name+".mat");materials[original]=mat;
            }
            renderer.sharedMaterial=mat;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            if(ground||foliage||trunk)
            {
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Living_"+filter.name;
                if(ground){Carve(filter,mesh,decorative);carvedMeshes++;}
                if(foliage||trunk){PackWind(filter,mesh);windMeshes++;}
                mesh.bounds=new Bounds(mesh.bounds.center,mesh.bounds.size+Vector3.one*1.5f);
                AssetDatabase.CreateAsset(mesh,Root+"/"+mesh.name+".asset");filter.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                if(ground&&filter.TryGetComponent<MeshCollider>(out var collider)){collider.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
            }
        }
        wind.wetAreas=new Transform[2];
        for(int i=0;i<2;i++)wind.wetAreas[i]=BuildFord(i,waterShader,shader).transform;
        wind.UpdateShader();
        ConfigureLight();ConfigureAmbience(wind);ConfigureReflections();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed.");
        return "Saved living forest: wind meshes="+windMeshes+", tree anchors="+anchors.Count+", carved surfaces="+carvedMeshes+", 2 river fords, recorded ambience, PBR textures and shadow/reflection quality. Backup="+backup;
    }
    static Vector2Int Cell(Vector3 p){return new Vector2Int(Mathf.FloorToInt(p.x/12),Mathf.FloorToInt(p.z/12));}
    static int Find(int[] p,int a){while(p[a]!=a){p[a]=p[p[a]];a=p[a];}return a;}
    static void Union(int[] p,int a,int b){p[Find(p,a)]=Find(p,b);}
    static void CollectAnchors(MeshFilter f)
    {
        var mesh=f.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;var p=Enumerable.Range(0,v.Length).ToArray();var weld=new Dictionary<Vector3Int,int>();
        for(int i=0;i<v.Length;i++){var key=Vector3Int.RoundToInt(v[i]*1000);if(weld.TryGetValue(key,out int j))Union(p,i,j);else weld[key]=i;}
        for(int i=0;i<t.Length;i+=3){Union(p,t[i],t[i+1]);Union(p,t[i],t[i+2]);}
        foreach(var g in Enumerable.Range(0,v.Length).GroupBy(i=>Find(p,i)))
        {
            var bounds=new Bounds(f.transform.TransformPoint(v[g.First()]),Vector3.zero);foreach(int i in g)bounds.Encapsulate(f.transform.TransformPoint(v[i]));
            if(bounds.size.y>4&&bounds.size.x<12&&bounds.size.z<12)anchors.Add(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
        }
    }
    static void PackWind(MeshFilter filter,Mesh mesh)
    {
        var uv=new List<Vector4>(mesh.vertexCount);var vertices=mesh.vertices;
        foreach(var vertex in vertices)
        {
            var p=filter.transform.TransformPoint(vertex);var cell=Cell(p);float best=float.MaxValue;Vector3 anchor=new Vector3(p.x,p.y-0.5f,p.z);
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(anchorGrid.TryGetValue(cell+new Vector2Int(x,z),out var list))foreach(var a in list)
            {float d=(new Vector2(p.x-a.x,p.z-a.z)).sqrMagnitude;if(d<best){best=d;anchor=a;}}
            uv.Add(new Vector4(anchor.x,anchor.z,anchor.y,1));
        }
        mesh.SetUVs(2,uv);
    }
    static Vector3 Local(Vector3 p,int ford){return Quaternion.Inverse(Quaternion.LookRotation(Vector3.left))*(p-FordCenters[ford]);}
    static void Carve(MeshFilter filter,Mesh mesh,bool decorative)
    {
        var vertices=mesh.vertices;
        for(int n=0;n<vertices.Length;n++)
        {
            var p=filter.transform.TransformPoint(vertices[n]);
            for(int i=0;i<2;i++)
            {
                var local=Local(p,i);float center=Mathf.Sin(local.x*0.07f)*1.6f;float cross=Mathf.Abs(local.z-center);
                if(Mathf.Abs(local.x)>30||cross>Widths[i]+4)continue;
                float width=Mathf.SmoothStep(0,1,Mathf.InverseLerp(Widths[i]+4,Widths[i]-1,cross));
                float ends=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(24,30,Mathf.Abs(local.x)));
                float level=FordCenters[i].y-local.x*0.008f;
                float depth=0.62f+Mathf.Clamp01(Mathf.Abs(local.x)/22)*0.38f;
                float bed=level-depth+Mathf.PerlinNoise(p.x*0.5f,p.z*0.5f)*0.05f;
                p.y=Mathf.Lerp(p.y,Mathf.Min(p.y,bed),width*ends);
            }
            vertices[n]=filter.transform.InverseTransformPoint(p);
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(decorative)
        {
            var triangles=mesh.triangles;var kept=new List<int>();
            for(int n=0;n<triangles.Length;n+=3)
            {
                var p=filter.transform.TransformPoint((vertices[triangles[n]]+vertices[triangles[n+1]]+vertices[triangles[n+2]])/3);bool wet=false;
                for(int i=0;i<2;i++){var q=Local(p,i);if(Mathf.Abs(q.x)<27&&Mathf.Abs(q.z-Mathf.Sin(q.x*0.07f)*1.6f)<Widths[i]-0.5f)wet=true;}
                if(!wet)kept.AddRange(new[]{triangles[n],triangles[n+1],triangles[n+2]});
            }
            mesh.SetTriangles(kept,0);
        }
    }
    static GameObject BuildFord(int index,Shader waterShader,Shader terrainShader)
    {
        var go=new GameObject(index==0?"River ford 1 - shallow crossing":"River ford 2 - stronger current");go.transform.SetParent(root.transform,false);
        go.transform.position=FordCenters[index];go.transform.rotation=Quaternion.LookRotation(Vector3.left);
        var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.center=new Vector3(0,-0.7f,0);box.size=new Vector3(60,3,Widths[index]*2+3);
        var current=go.AddComponent<RiverCurrent>();current.halfLength=30;current.halfWidth=Widths[index];current.currentSpeed=index==0?2.7f:3.1f;
        var water=new Material(waterShader);water.name="Flowing water "+index;water.SetVector("_FlowDirection",new Vector4(go.transform.right.x,go.transform.right.z,0,0));water.SetFloat("_CurrentSpeed",current.currentSpeed);AssetDatabase.CreateAsset(water,Root+"/"+water.name+".mat");
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int x=0;x<=60;x++)for(int z=0;z<=12;z++)
        {
            float along=-30+x;float center=current.ChannelOffset(along);vertices.Add(new Vector3(along,-along*current.surfaceSlope,center+Mathf.Lerp(-Widths[index],Widths[index],z/12f)));uv.Add(new Vector2(along/4,z/12f));
            int n=x*13+z;if(x<60&&z<12)triangles.AddRange(new[]{n,n+1,n+13,n+1,n+14,n+13});
        }
        var mesh=new Mesh();mesh.name="River water "+index;mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,Root+"/"+mesh.name+".asset");
        var surface=new GameObject("Animated current surface");surface.transform.SetParent(go.transform,false);surface.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;
        var bedMat=new Material(terrainShader);bedMat.name="Wet river pebbles "+index;SetPbr(bedMat,"river_small_rocks",0.345f);bedMat.SetFloat("_SnowAmount",0);bedMat.SetFloat("_Smoothness",0.42f);AssetDatabase.CreateAsset(bedMat,Root+"/"+bedMat.name+".mat");
        var bedV=new List<Vector3>();var bedUv=new List<Vector2>();var bedT=new List<int>();
        for(int x=0;x<=24;x++)for(int z=0;z<=20;z++)
        {
            float along=-12+x;float across=Mathf.Lerp(-Widths[index]-3,Widths[index]+3,z/20f);float slope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(Widths[index]-1,Widths[index]+3,Mathf.Abs(across)));
            float y=-along*0.008f-0.62f+Mathf.Clamp01(Mathf.Abs(along)/22)*-0.38f+slope*0.82f;
            y+=Mathf.PerlinNoise(along*1.7f+20,across*1.7f)*0.045f;
            bedV.Add(new Vector3(along,y,across+current.ChannelOffset(along)));bedUv.Add(new Vector2(along/3,across/3));int n=x*21+z;if(x<24&&z<20)bedT.AddRange(new[]{n,n+1,n+21,n+1,n+22,n+21});
        }
        var bedMesh=new Mesh();bedMesh.name="River ford bed "+index;bedMesh.SetVertices(bedV);bedMesh.SetUVs(0,bedUv);bedMesh.SetTriangles(bedT,0);bedMesh.RecalculateNormals();AssetDatabase.CreateAsset(bedMesh,Root+"/"+bedMesh.name+".asset");
        var bed=new GameObject("Gravel ford bed - physical");bed.transform.SetParent(go.transform,false);bed.AddComponent<MeshFilter>().sharedMesh=bedMesh;bed.AddComponent<MeshRenderer>().sharedMaterial=bedMat;bed.AddComponent<MeshCollider>().sharedMesh=bedMesh;
        var traction=bed.AddComponent<ForestDrivingSurface>();traction.cover=ForestDrivingSurface.Cover.Mud;traction.forwardGrip=0.7f;traction.lateralGrip=0.36f;traction.rollingResistance=0.02f;
        var random=new System.Random(915+index);
        var stoneMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/ForestLightSnow/Forest_Broken_Rocks_LightSnow.mat");
        for(int i=0;i<30;i++)
        {
            float along=-27+i*1.8f;float across=(i%2==0?-1:1)*(Widths[index]+0.6f+(float)random.NextDouble()*1.2f);
            if(Mathf.Abs(along)<5.5f)continue;
            var rock=GameObject.CreatePrimitive(PrimitiveType.Sphere);rock.name="Riverbank stone";rock.transform.SetParent(go.transform,false);rock.transform.localPosition=new Vector3(along,-along*0.008f+0.05f,across+current.ChannelOffset(along));rock.transform.localScale=new Vector3(0.5f+(float)random.NextDouble()*0.7f,0.35f,0.6f);rock.GetComponent<Renderer>().sharedMaterial=stoneMat;
        }
        foreach(float side in new[]{-Widths[index]-2,Widths[index]+2})foreach(float lane in new[]{-4f,4f})
        {
            var pole=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pole.name="Ford lane marker";pole.transform.SetParent(go.transform,false);pole.transform.localPosition=new Vector3(lane,0.7f,side+current.ChannelOffset(lane));pole.transform.localScale=new Vector3(0.11f,0.85f,0.11f);
        }
        var sign=new GameObject("FORD warning");sign.transform.SetParent(go.transform,false);sign.transform.localPosition=new Vector3(-6,2,-Widths[index]-6);
        var text=sign.AddComponent<TextMesh>();text.text="FORD / CURRENT\nLOW RANGE\nSTEER UPSTREAM";text.fontSize=60;text.characterSize=0.10f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(0.9f,0.65f,0.14f);
        var audio=go.AddComponent<AudioSource>();audio.clip=LoadAudio("river");audio.loop=true;audio.playOnAwake=true;audio.volume=0.24f;audio.spatialBlend=1;audio.minDistance=8;audio.maxDistance=85;audio.rolloffMode=AudioRolloffMode.Linear;audio.dopplerLevel=0;
        return go;
    }
    static void SetPbr(Material material,string asset,float scale)
    {
        string dir="Assets/Game/Art/ForestEnhancement/"+asset+"/";
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+asset+"_diff_2k.jpg"));
        material.SetTexture("_BumpMap",Normal(dir+asset+"_nor_gl_2k.jpg"));
        var rough=AssetImporter.GetAtPath(dir+asset+"_rough_2k.jpg") as TextureImporter;if(rough!=null){rough.sRGBTexture=false;rough.SaveAndReimport();}
        material.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+asset+"_rough_2k.jpg"));material.SetFloat("_UseRoughness",1);material.SetFloat("_BumpScale",0.72f);material.SetFloat("_UseWorldTextures",1);material.SetFloat("_WorldTextureScale",scale);material.SetColor("_BaseColor",Color.white);
    }
    static Texture2D Normal(string path)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new Exception("Missing normal: "+path);
        if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static AudioClip LoadAudio(string kind)
    {
        string path="Assets/Game/Audio/Forest/Forest_"+kind+".wav";var importer=AssetImporter.GetAtPath(path) as AudioImporter;
        if(importer==null)throw new Exception("Missing ambience "+path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.CompressedInMemory;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=0.8f;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    static void ConfigureAmbience(ForestWind wind)
    {
        var manager=root.AddComponent<ForestAmbience>();manager.forestWind=wind;manager.drivingCamera=UnityEngine.Object.FindAnyObjectByType<DriveCamera>();
        var birds=new GameObject("Birds - natural field recording");birds.transform.SetParent(root.transform,false);manager.birds=birds.AddComponent<AudioSource>();manager.birds.clip=LoadAudio("birds");manager.birds.loop=true;manager.birds.volume=0.13f;manager.birds.spatialBlend=0;manager.birds.playOnAwake=true;
        var breeze=new GameObject("Wind through forest");breeze.transform.SetParent(root.transform,false);manager.wind=breeze.AddComponent<AudioSource>();manager.wind.clip=LoadAudio("wind");manager.wind.loop=true;manager.wind.volume=0.06f;manager.wind.spatialBlend=0;manager.wind.playOnAwake=true;
    }
    static void ConfigureLight()
    {
        var sun=UnityEngine.Object.FindObjectsByType<Light>().First(l=>l.type==LightType.Directional);sun.transform.rotation=Quaternion.Euler(34,-38,0);sun.color=new Color(1,0.93f,0.82f);sun.intensity=2.1f;sun.shadows=LightShadows.Soft;sun.shadowStrength=0.9f;sun.shadowBias=0.025f;sun.shadowNormalBias=0.22f;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(0.60f,0.70f,0.79f);RenderSettings.ambientEquatorColor=new Color(0.43f,0.49f,0.49f);RenderSettings.ambientGroundColor=new Color(0.26f,0.29f,0.25f);
        var original=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(original!=null)
        {
            var pipeline=UnityEngine.Object.Instantiate(original);pipeline.name="Living forest quality";AssetDatabase.CreateAsset(pipeline,Root+"/ForestQuality.asset");var serialized=new SerializedObject(pipeline);
            SetInt(serialized,"m_MainLightShadowmapResolution",4096);SetInt(serialized,"m_ShadowCascadeCount",4);SetInt(serialized,"m_MSAA",4);
            var distance=serialized.FindProperty("m_ShadowDistance");if(distance!=null)distance.floatValue=120;serialized.ApplyModifiedPropertiesWithoutUndo();QualitySettings.renderPipeline=pipeline;
        }
        QualitySettings.realtimeReflectionProbes=true;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Forest lighting";
        var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.ACES;
        var bloom=profile.Add<Bloom>(true);bloom.intensity.value=0.12f;bloom.threshold.value=1.2f;bloom.scatter.value=0.6f;
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.value=0.2f;color.contrast.value=6;color.saturation.value=-3;
        AssetDatabase.CreateAsset(profile,Root+"/ForestLighting.asset");var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=5;volume.sharedProfile=profile;
        var camera=Camera.main.GetUniversalAdditionalCameraData();camera.renderPostProcessing=true;camera.requiresDepthTexture=true;camera.requiresColorTexture=true;camera.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }
    static void SetInt(SerializedObject source,string name,int value){var property=source.FindProperty(name);if(property!=null)property.intValue=value;}
    static void ConfigureReflections()
    {
        var go=new GameObject("Forest river reflections");go.transform.SetParent(root.transform,false);go.transform.position=FordCenters[0]+Vector3.up*3;
        var probe=go.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.size=new Vector3(260,160,260);probe.boxProjection=true;probe.intensity=0.8f;probe.farClipPlane=200;probe.blendDistance=30;
    }
}
