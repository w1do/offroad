using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ConfigureRealisticPuddles
{
    const string Folder = "Assets/Game/Configs/Puddles";
    static PuddleEffectsProfile profile;
    static Material water;
    static int sections, triangles;
    static readonly List<string> measurements = new List<string>();
    static Mesh SaveMesh(Mesh mesh,string path)
    {
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;
    }
    static float BasinDistance(Vector3 local,Vector2 centre,Vector2 radius)
    {
        Vector2 q=new Vector2((local.x-centre.x)/radius.x,(local.z-centre.y)/radius.y);
        float angle=Mathf.Atan2(q.y,q.x);
        return q.magnitude/(1+.055f*Mathf.Sin(angle*5)+.035f*Mathf.Cos(angle*9));
    }
    static void Configure(GameObject root, string token)
    {
        int sectionIndex = 0;
        foreach (var section in root.GetComponentsInChildren<MudRoadSection>(true))
        {
            var filter = section.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            if (root.scene.IsValid() && !EditorSceneManager.IsPreviewScene(root.scene) && PrefabUtility.IsPartOfPrefabInstance(section))
            {
                foreach (Transform child in section.transform.Cast<Transform>().ToArray())
                    if (child.name == "Basin puddles")
                    {
                        if (PrefabUtility.IsAddedGameObjectOverride(child.gameObject)) UnityEngine.Object.DestroyImmediate(child.gameObject);
                        else foreach (var component in child.GetComponents<Component>())
                            PrefabUtility.RevertObjectOverride(component, InteractionMode.AutomatedAction);
                    }
                continue; // Streamed instances inherit the configured source prefab.
            }
            var original = section.transform.Find("Water in low ruts");
            if (original != null) original.gameObject.SetActive(false);
            string name = "Basin puddles";
            var old = section.transform.Find(name);
            var go = old == null ? new GameObject(name) : old.gameObject;
            go.layer=4; // Water is omitted from its own reflection capture.
            go.transform.SetParent(section.transform, false);
            var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
            string sourceToken=token.StartsWith("main")?section.GetComponent<SoftGroundRuts>().persistentId.Split('/')[0]:"module "+token;
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Generated/NaturalMud/Road "+sourceToken+".asset");
            if(source==null)throw new Exception("Missing original road mesh: "+sourceToken);
            // The existing blend shader preserves the original forest UV at the section edge.
            foreach (var material in section.GetComponent<Renderer>().sharedMaterials)
            {
                if (material.shader.name!="Game/Living Forest") continue;
                material.SetTexture("_MudMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/brown_mud_03_diff_2k.jpg"));
                material.SetTexture("_MudNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/brown_mud_03_nor_gl_2k.jpg"));
                material.SetColor("_MudTint", new Color(.95f,.92f,.88f));
                material.SetVector("_MudUVScale", new Vector4(section.mudHalfWidthMetres*2/1.3f, section.halfSize.y*2/1.3f,0,0));
                material.SetFloat("_MudBlend",0);material.SetFloat("_RoadMaterialBlend",1);
                material.SetFloat("_RoadTextureScale",1/3f);material.SetFloat("_RoadWetPatch",1);
                material.SetTexture("_RoadMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/dry_mud_field_001_diff_2k.jpg"));
                material.SetTexture("_RoadNormal",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/dry_mud_field_001_nor_gl_2k.jpg"));
                material.SetTexture("_RoadRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/dry_mud_field_001_rough_2k.jpg"));
                material.SetTexture("_WetRoadRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/brown_mud_03_rough_2k.jpg"));
                material.SetFloat("_RoadLeavesBlend",1);
                material.SetTexture("_RoadLeavesMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/brown_mud_leaves_01_diff_2k.jpg"));
                material.SetTexture("_RoadLeavesNormal",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/brown_mud_leaves_01_nor_gl_2k.jpg"));
                material.SetTexture("_RoadLeavesRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/brown_mud_leaves_01_rough_2k.jpg"));
                EditorUtility.SetDirty(material);
            }
            var local=source.vertices;
            var world = local.Select(v => section.transform.TransformPoint(v)).ToArray();
            bool large=token.Contains("main")||token.Contains("Family 1");
            var basins=large?new[]{new Vector2(section.laneCentreMetres,0)}:
                new[]{new Vector2(section.laneCentreMetres-.65f,-1.5f),new Vector2(section.laneCentreMetres+.65f,1.4f)};
            Vector2 radius=large?profile.largeBasinRadiusMetres:profile.smallBasinRadiusMetres;
            radius.x=Mathf.Min(radius.x,section.mudHalfWidthMetres*.86f);
            var levels=new List<float>();
            foreach(var centre in basins)
            {
                // The waterline stays below the uncut rim, including the downhill side.
                var rim=Enumerable.Range(0,local.Length).Where(i=>{float r=BasinDistance(local[i],centre,radius);return r>.88f&&r<1.13f;}).ToArray();
                if(rim.Length==0)throw new Exception("No road rim for basin");
                float level=rim.Min(i=>world[i].y)-.015f;levels.Add(level);
                for(int i=0;i<local.Length;i++)
                {
                    float r=BasinDistance(local[i],centre,radius);if(r>=1)continue;
                    float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-r)/.3f));
                    float bed=level-profile.basinDepthMetres*(1-r*r);
                    world[i].y=Mathf.Lerp(world[i].y,Mathf.Min(world[i].y,bed),blend);
                }
            }
            var bedMesh=UnityEngine.Object.Instantiate(source);
            bedMesh.vertices=world.Select(v=>section.transform.InverseTransformPoint(v)).ToArray();bedMesh.RecalculateNormals();bedMesh.RecalculateTangents();bedMesh.RecalculateBounds();
            filter.sharedMesh=SaveMesh(bedMesh,Folder+"/Bed "+sourceToken+".asset");
            var collision=UnityEngine.Object.Instantiate(filter.sharedMesh);var front=collision.triangles;var both=new List<int>(front);
            for(int i=0;i<front.Length;i+=3)both.AddRange(new[]{front[i],front[i+2],front[i+1]});collision.SetTriangles(both,0);
            section.GetComponent<MeshCollider>().sharedMesh=SaveMesh(collision,Folder+"/Bed collision "+sourceToken+".asset");
            var points = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
            var t = source.triangles;
            for(int basin=0;basin<basins.Length;basin++)
            {
            float level=levels[basin];
            for (int i = 0; i < t.Length; i += 3)
            {
                var mid=(local[t[i]]+local[t[i+1]]+local[t[i+2]])/3;
                if(BasinDistance(mid,basins[basin],radius)>1.05f)continue;
                var polygon = new List<Vector3> { world[t[i]], world[t[i+1]], world[t[i+2]] };
                var clipped = new List<Vector3>(); var previous = polygon[2];
                foreach (var p in polygon)
                {
                    if ((p.y < level) != (previous.y < level)) clipped.Add(Vector3.Lerp(previous, p, (level-previous.y)/(p.y-previous.y)));
                    if (p.y < level) clipped.Add(p);
                    previous = p;
                }
                for (int j = 1; j + 1 < clipped.Count; j++)
                {
                    Vector3 ab=clipped[j]-clipped[0], ac=clipped[j+1]-clipped[0];
                    if (Mathf.Abs(ab.x*ac.z-ab.z*ac.x)<.00001f) continue;
                    int start = points.Count;
                    foreach (var p in new[] { clipped[0], clipped[j], clipped[j+1] })
                    {
                        points.Add(section.transform.InverseTransformPoint(new Vector3(p.x, level + .003f, p.z)));
                        colors.Add(new Color(Mathf.Max(0, level-p.y), 0, 0, 1));
                    }
                    indices.AddRange(new[] { start, start+1, start+2 });
                }
            }
            }
            var mesh = new Mesh { name = "Flooded original road " + token, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(points); mesh.SetTriangles(indices, 0); mesh.SetColors(colors); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = Folder + "/Basin " + token + " section " + sectionIndex++ + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null) { AssetDatabase.CreateAsset(mesh,path); asset=mesh; }
            else { EditorUtility.CopySerialized(mesh,asset); EditorUtility.SetDirty(asset); UnityEngine.Object.DestroyImmediate(mesh); }
            mf.sharedMesh=asset; mr.sharedMaterial=water; mr.shadowCastingMode=ShadowCastingMode.Off;
            var box=go.GetComponent<BoxCollider>(); if(box==null)box=go.AddComponent<BoxCollider>();
            box.isTrigger=true; var b=asset.bounds; box.center=b.center-Vector3.up*.6f;
            box.size=new Vector3(Mathf.Max(.01f,b.size.x),b.size.y+1.2f,Mathf.Max(.01f,b.size.z));
            var volume=go.GetComponent<WaterVolume>(); if(volume==null)volume=go.AddComponent<WaterVolume>();
            volume.surfaceMesh=mf; volume.localSurfaceHeight=b.max.y; volume.BuildSurface();
            sections++; triangles+=indices.Count/3;
            measurements.Add(token+": "+indices.Count/3+" water triangles; bounds "+mr.bounds.size);
        }
    }
    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Requires Edit Mode");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var soilNormal=(TextureImporter)AssetImporter.GetAtPath("Assets/Game/Art/Puddles/brown_mud_03_nor_gl_2k.jpg");
        soilNormal.textureType=TextureImporterType.NormalMap;soilNormal.SaveAndReimport();
        profile=AssetDatabase.LoadAssetAtPath<PuddleEffectsProfile>(Folder+"/Puddle effects.asset");
        if(profile==null){profile=ScriptableObject.CreateInstance<PuddleEffectsProfile>();AssetDatabase.CreateAsset(profile,Folder+"/Puddle effects.asset");}
        profile.smallFillDepthMetres=.08f; profile.largeFillDepthMetres=.23f;
        // Reuse the project's existing transparent droplet material and original puddle normal map.
        var vehicle=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        if(vehicle==null)throw new Exception("Open the vehicle scene");
        profile.dropletMaterial=vehicle.profile.wheelEffects.splashMaterials[0];
        var splash=vehicle.GetComponent<VehiclePuddleSplash>(); if(splash==null)splash=vehicle.gameObject.AddComponent<VehiclePuddleSplash>(); splash.profile=profile;
        var reflections=vehicle.GetComponent<PuddleReflections>();if(reflections==null)reflections=vehicle.gameObject.AddComponent<PuddleReflections>();reflections.profile=profile;
        water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Forest puddle.mat");
        if(water==null){water=new Material(Shader.Find("Game/Forest Puddle"));AssetDatabase.CreateAsset(water,Folder+"/Forest puddle.mat");}
        water.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/brown_mud_03_diff_2k.jpg"));
        water.SetColor("_BaseColor",new Color(.22f,.28f,.28f,.95f));water.SetFloat("_NormalStrength",.035f);
        string normalPath="Assets/Game/Art/Forest/textures/Puddle_Streaks_Normal.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(normalPath);
        if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
        water.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        foreach(var root in vehicle.gameObject.scene.GetRootGameObjects()) Configure(root,"main "+root.name);
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Generated/AuthoredLongForest"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid); var root=PrefabUtility.LoadPrefabContents(path);
            try { Configure(root,Path.GetFileNameWithoutExtension(path));PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        EditorUtility.SetDirty(profile);EditorUtility.SetDirty(water);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);EditorSceneManager.SaveScene(vehicle.gameObject.scene);
        // Only remove obsolete meshes created by this tool inside its own new output folder.
        var dependencies=new HashSet<string>(AssetDatabase.GetDependencies(new[]{vehicle.gameObject.scene.path}.Concat(
            AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Generated/AuthoredLongForest"}).Select(AssetDatabase.GUIDToAssetPath)).ToArray(),true));
        foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{Folder}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!dependencies.Contains(path))AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.DeleteAsset("Assets/Game/Art/Puddles/mud_forest_diff_1k.jpg");
        File.WriteAllLines("work/puddle-layout.txt",measurements);
        return "Configured "+sections+" puddle sections, "+triangles+" triangles; per-wheel splashes attached. Details: work/puddle-layout.txt";
    }
}




