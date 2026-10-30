using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class BuildForestKamaz
{
    const string Root = "Assets/Game";
    static string[] textures;
    static Dictionary<string, Material> materials = new Dictionary<string, Material>();

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before authoring.");
        var old = SceneManager.GetActiveScene();
        if (old.path != "Assets/MountainDrive/MountainPass.unity") throw new Exception("Expected MountainPass scene.");
        Directory.CreateDirectory(Root + "/Scenes");
        Directory.CreateDirectory(Root + "/Generated");
        string backup = Root + "/Scenes/MountainPass_BeforeForest.unity";
        if (File.Exists(backup)) throw new Exception("Backup already exists; refuse repeat build.");
        EditorSceneManager.SaveScene(old, backup, true);
        AssetDatabase.Refresh();
        textures = AssetDatabase.FindAssets("t:Texture2D", new[] {Root + "/Art"})
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var forest = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Forest/Dirt road scene.fbx"), scene);
        forest.name = "Forest - imported dirt road environment";
        forest.transform.position = new Vector3(0, -130, 0);
        MeshFilter road = null;
        foreach (var r in forest.GetComponentsInChildren<MeshRenderer>())
        {
            string source = r.sharedMaterial.name;
            r.sharedMaterial = ForestMaterial(source);
            if (source == "Dirt_Road") road = r.GetComponent<MeshFilter>();
            if (new[] {"Dirt_Road", "Dirt_Road_Bare", "Ground_Dirt", "Aerial_Grass", "Terrain_Far", "Tall_Cliff", "Sloped_Rock", "Broken_Rocks", "Cobblestone", "Mud_Pile", "Trunk_Oak", "Trunk_Birch", "Wood_Log", "Wood_Fence", "Metal_Fence"}.Contains(source))
                r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
        }
        if (road == null) throw new Exception("Missing road mesh.");
        var vertices = road.sharedMesh.vertices;
        var triangles = road.sharedMesh.triangles;
        float best = 0; Vector3 start = Vector3.zero, direction = Vector3.forward;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = road.transform.TransformPoint(vertices[triangles[i]]);
            var b = road.transform.TransformPoint(vertices[triangles[i+1]]);
            var c = road.transform.TransformPoint(vertices[triangles[i+2]]);
            var n = Vector3.Cross(b-a,c-a);
            if (Mathf.Abs(n.normalized.y) < 0.95f || n.magnitude <= best) continue;
            best = n.magnitude; start = (a+b+c)/3;
            var edges = new[] {b-a,c-b,a-c};
            direction = edges.OrderByDescending(e=>e.sqrMagnitude).First(); direction.y=0;
        }
        if (best <= 0) throw new Exception("No safe road triangle.");
        var rig = new GameObject("KAMAZ - six wheel prototype");
        rig.transform.SetPositionAndRotation(start + Vector3.up * 0.3f, Quaternion.LookRotation(direction));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Kamaz/KAMAZ.fbx"), scene);
        model.transform.SetParent(rig.transform, false);
        foreach(var r in model.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = TruckMaterial(r.sharedMaterial.name);
        var body = rig.AddComponent<Rigidbody>(); body.mass = 8500; body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var box = rig.AddComponent<BoxCollider>(); box.center = new Vector3(0,1.5f,-0.3f); box.size = new Vector3(2.35f,1.8f,5.7f);
        var controller = rig.AddComponent<ForestKamazController>();
        controller.wheels = new WheelCollider[6]; controller.visuals = new Transform[6];
        var tireFilter = model.GetComponentsInChildren<MeshFilter>().First(f=>f.name.EndsWith("_Tires"));
        var points = tireFilter.sharedMesh.vertices.Select(v=>model.transform.InverseTransformPoint(tireFilter.transform.TransformPoint(v))).ToArray();
        float[] centers = {points.Max(v=>v.z), -1.3f, points.Min(v=>v.z)};
        for(int iteration=0;iteration<25;iteration++) for(int k=0;k<3;k++)
        {
            int cluster=k;
            var group=points.Where(v=>Nearest(v.z,centers)==cluster).ToArray();
            if(group.Length>0) centers[k]=group.Average(v=>v.z);
        }
        Array.Sort(centers); Array.Reverse(centers);
        float radius = (points.Max(v=>v.y)-points.Min(v=>v.y))/2;
        float height = (points.Max(v=>v.y)+points.Min(v=>v.y))/2;
        for(int i=0;i<6;i++)
        {
            float x = i%2==0 ? -1.05f : 1.05f;
            var p = new Vector3(x,height,centers[i/2]);
            var wheelGo = new GameObject("Wheel collider " + i); wheelGo.transform.SetParent(rig.transform,false); wheelGo.transform.localPosition=p;
            var wheel=wheelGo.AddComponent<WheelCollider>(); wheel.radius=radius; wheel.mass=85; wheel.suspensionDistance=0.22f;
            var spring=wheel.suspensionSpring; spring.spring=90000; spring.damper=11000; spring.targetPosition=0.5f; wheel.suspensionSpring=spring;
            wheel.forceAppPointDistance=0.25f;
            var f=wheel.forwardFriction; f.stiffness=1.15f; wheel.forwardFriction=f;
            f=wheel.sidewaysFriction; f.stiffness=1.1f; wheel.sidewaysFriction=f;
            controller.wheels[i]=wheel;
            var pivot=new GameObject("Animated wheel " + i); pivot.transform.SetParent(rig.transform,false); pivot.transform.localPosition=p;
            controller.visuals[i]=pivot.transform;
        }
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.EndsWith("_Tires") || f.name.EndsWith("_Wheels")).ToArray())
            SplitWheels(filter, model.transform, controller.visuals, centers);
        var cameraGo=new GameObject("Main Camera"); cameraGo.tag="MainCamera";
        var cam=cameraGo.AddComponent<Camera>(); cam.farClipPlane=1600; cam.nearClipPlane=0.15f; cam.fieldOfView=62;
        cameraGo.AddComponent<AudioListener>();
        var follow=cameraGo.AddComponent<DriveCamera>(); follow.target=rig.transform;
        cameraGo.transform.position=rig.transform.position-rig.transform.forward*9+Vector3.up*4;
        cameraGo.transform.LookAt(rig.transform.position+Vector3.up*1.5f);
        var sun=new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.5f; sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(48,-35,0);
        RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(0.65f,0.73f,0.82f);
        RenderSettings.ambientEquatorColor=new Color(0.43f,0.48f,0.48f); RenderSettings.ambientGroundColor=new Color(0.26f,0.28f,0.23f);
        RenderSettings.fog=true; RenderSettings.fogColor=new Color(0.69f,0.76f,0.8f); RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=0.0015f;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=RenderSettings.fogColor;
        AssetDatabase.SaveAssets();
        EditorSceneManager.CloseScene(old,true);
        if (!EditorSceneManager.SaveScene(scene,"Assets/MountainDrive/MountainPass.unity")) throw new Exception("Scene save failed.");
        Selection.activeGameObject=rig;
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(rig.transform.position+Vector3.up,Quaternion.Euler(25,rig.transform.eulerAngles.y,0),16);
        return "Saved forest + KAMAZ; backup="+backup+"; spawn="+rig.transform.position+"; axles="+string.Join(",",centers)+"; radius="+radius;
    }

    static int Nearest(float z,float[] centers)
    { int best=0; for(int i=1;i<centers.Length;i++) if(Mathf.Abs(z-centers[i])<Mathf.Abs(z-centers[best])) best=i; return best; }

    static void SplitWheels(MeshFilter source,Transform model,Transform[] pivots,float[] centers)
    {
        var mesh=source.sharedMesh; var vertices=mesh.vertices; var normals=mesh.normals; var uv=mesh.uv; var indices=mesh.triangles;
        var bins=new List<int>[6]; for(int i=0;i<6;i++) bins[i]=new List<int>();
        for(int i=0;i<indices.Length;i+=3)
        {
            var c=model.InverseTransformPoint(source.transform.TransformPoint((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3));
            int bin=Nearest(c.z,centers)*2+(c.x>0?1:0);
            bins[bin].AddRange(new[]{indices[i],indices[i+1],indices[i+2]});
        }
        for(int i=0;i<6;i++)
        {
            var vs=new List<Vector3>(); var ns=new List<Vector3>(); var uvs=new List<Vector2>(); var tris=new List<int>();
            foreach(int index in bins[i])
            {
                vs.Add(pivots[i].InverseTransformPoint(source.transform.TransformPoint(vertices[index])));
                ns.Add(pivots[i].InverseTransformDirection(source.transform.TransformDirection(normals[index])));
                uvs.Add(uv[index]); tris.Add(tris.Count);
            }
            var part=new Mesh(); part.name=source.name+"_"+i; part.indexFormat=IndexFormat.UInt32;
            part.SetVertices(vs); part.SetNormals(ns); part.SetUVs(0,uvs); part.SetTriangles(tris,0); part.RecalculateBounds();
            AssetDatabase.CreateAsset(part,Root+"/Generated/"+part.name+".asset");
            var go=new GameObject(source.name); go.transform.SetParent(pivots[i],false);
            go.AddComponent<MeshFilter>().sharedMesh=part; go.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponent<MeshRenderer>().sharedMaterial;
        }
        source.GetComponent<MeshRenderer>().enabled=false;
    }

    static Material ForestMaterial(string name)
    {
        string stem=name;
        var remap=new Dictionary<string,string>{{"Forest_Bush","Bush_Leaves"},{"Mud_Pile","Wall_Mud"},{"Road_Edge_Gravel_Dusty","Dusty_Road_Edge_Gravel"},{"Sloped_Rock","Rock_Sloped"},{"Wood_Fence","Wooden Fence"}};
        if(remap.ContainsKey(name)) stem=remap[name];
        return MaterialFor("Forest_"+name,stem,"Forest");
    }
    static Material TruckMaterial(string name)
    {
        var map=new Dictionary<string,string>{{"KAM_Back","Back"},{"KAM_Body","Body"},{"KAM_Bump","Bump"},{"KAM_Cabin_Add","Cabin_Add"},{"KAM_Cabin","Cabin"},{"KAM_Cabin_Inside","Cabin_Inside"},{"KAM_Seats","Seats"},{"KAM_Tire","Tire"},{"KAM_Wheels","Wheels"},{"KAM_Wipers","Wipers"},{"KAM_Cap","CopCap"},{"KAM_SWheel","Swheel"},{"KAM_Curtains","Curtains"},{"KAM_Cabin_Inside_Panel","InPanel"},{"KAM_Glass","Glass_Dirt"}};
        return MaterialFor(name,"TX_ClassicTruck1969_"+(map.ContainsKey(name)?map[name]:name),"Kamaz");
    }
    static Material MaterialFor(string name,string stem,string folder)
    {
        if(materials.ContainsKey(name)) return materials[name];
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name=name;
        string prefix=stem=="Wooden Fence" ? stem+" " : stem+"_";
        string path=textures.FirstOrDefault(p=>p.Contains("/"+folder+"/") && (Path.GetFileNameWithoutExtension(p)==prefix+"Diffuse" || Path.GetFileNameWithoutExtension(p)==prefix+"Base_color" || Path.GetFileNameWithoutExtension(p)==prefix+"D" || Path.GetFileNameWithoutExtension(p)==prefix+"Base_colo"));
        if(stem.EndsWith("InPanel") || stem.EndsWith("Glass_Dirt")) path=textures.FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p)==stem || Path.GetFileName(p)==stem+".tga.png");
        mat.SetFloat("_Smoothness",0.16f); mat.SetFloat("_Cull",0);
        if(path!=null)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string opacity=textures.FirstOrDefault(p=>p.Contains("/"+folder+"/") && Path.GetFileNameWithoutExtension(p)==prefix+"Opacity");
            if (opacity == null && stem.StartsWith("Grass_Vegetation")) opacity=textures.FirstOrDefault(p=>Path.GetFileName(p)=="Grass_Vegetation_Opacity.png");
            if(opacity!=null)
            {
                var color=Readable(texture); var mask=Readable(AssetDatabase.LoadAssetAtPath<Texture2D>(opacity));
                var pixels=color.GetPixels(); for(int y=0;y<color.height;y++) for(int x=0;x<color.width;x++) {int i=y*color.width+x;pixels[i].a=mask.GetPixelBilinear((x+0.5f)/color.width,(y+0.5f)/color.height).r;}
                color.SetPixels(pixels); color.Apply(); string packed=Root+"/Generated/"+name+"_Alpha.png"; File.WriteAllBytes(packed,color.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(color); UnityEngine.Object.DestroyImmediate(mask); AssetDatabase.ImportAsset(packed);
                texture=AssetDatabase.LoadAssetAtPath<Texture2D>(packed);
                mat.SetFloat("_AlphaClip",1); mat.SetFloat("_Cutoff",0.35f); mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue=2450;
            }
            mat.SetTexture("_BaseMap",texture); mat.SetColor("_BaseColor",Color.white);
        }
        else mat.SetColor("_BaseColor",name=="KAM_Glass" ? new Color(0.15f,0.22f,0.25f) : new Color(0.25f,0.28f,0.23f));
        AssetDatabase.CreateAsset(mat,Root+"/Generated/"+name+".mat"); materials[name]=mat; return mat;
    }
    static Texture2D Readable(Texture source)
    {
        var previous=RenderTexture.active; var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        Graphics.Blit(source,rt); RenderTexture.active=rt;
        var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false); copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0); copy.Apply();
        RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); return copy;
    }
}
