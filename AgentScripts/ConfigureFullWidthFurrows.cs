using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ConfigureFullWidthFurrows
{
    const string Folder="Assets/Game/Generated/FullWidthFurrows",Art="Assets/Game/Art/FarmFurrows/";
    static MudRoadProfile road;
    static SoftGroundProfile ground;
    static Texture2D height;
    static int sections;
    static T Save<T>(T asset,string path) where T:UnityEngine.Object
    {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old==null){AssetDatabase.CreateAsset(asset,path);return asset;}
        EditorUtility.CopySerialized(asset,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(asset);return old;
    }
    static void Configure(GameObject root,string prefabName=null)
    {
        foreach(var section in root.GetComponentsInChildren<MudRoadSection>(true))
        {
            if(!section.gameObject.activeInHierarchy||!section.GetComponent<SoftGroundRuts>().enabled)continue;
            if(prefabName==null&&PrefabUtility.IsPartOfPrefabInstance(section))continue;
            var soil=section.GetComponent<SoftGroundRuts>();
            string token=prefabName==null?soil.persistentId.Split('/')[0]:"module "+prefabName;
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Generated/NaturalMud/Road "+token+".asset");
            if(source==null)throw new Exception("Missing authored road source: "+token);
            var mesh=UnityEngine.Object.Instantiate(source);var original=source.vertices;var vertices=(Vector3[])original.Clone();
            var uv=new Vector2[vertices.Length];var local=new List<Vector2>(vertices.Length);
            var rows=Enumerable.Range(0,vertices.Length).GroupBy(i=>Mathf.RoundToInt(original[i].z*10000));
            float feather=road.edgeBlendMetres;
            foreach(var row in rows)
            {
                var ordered=row.OrderBy(i=>original[i].x).ToArray();
                Vector3 left=section.transform.TransformPoint(original[ordered.First()]),right=section.transform.TransformPoint(original[ordered.Last()]);
                float min=original[ordered.First()].x,max=original[ordered.Last()].x;
                foreach(int i in ordered)
                {
                    var p=original[i];float x=Mathf.InverseLerp(min,max,p.x);
                    float edge=Mathf.Min(section.halfSize.x-Mathf.Abs(p.x),section.halfSize.y-Mathf.Abs(p.z));
                    float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/feather));
                    float u=p.x/road.furrowTileMetres,v=p.z/road.furrowTileMetres;
                    float relief=(height.GetPixelBilinear(Mathf.Repeat(u,1),Mathf.Repeat(v,1)).r-.5f)*road.furrowReliefMetres;
                    float target=Mathf.Lerp(left.y,right.y,x)-road.fullWidthBedDepthMetres+relief;
                    float current=section.transform.TransformPoint(p).y;
                    vertices[i]=p+section.transform.InverseTransformVector(Vector3.up*((target-current)*blend));
                    uv[i]=new Vector2(u,v);
                }
            }
            foreach(var p in original)local.Add(new Vector2(p.x,p.z));
            mesh.vertices=vertices;mesh.uv2=uv;mesh.SetUVs(3,local);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            mesh=Save(mesh,Folder+"/Bed "+token+".asset");
            section.GetComponent<MeshFilter>().sharedMesh=mesh;
            var collision=UnityEngine.Object.Instantiate(mesh);var indices=collision.triangles;
            for(int i=0;i<indices.Length;i+=3)
                if(Vector3.Cross(section.transform.TransformVector(vertices[indices[i+1]]-vertices[indices[i]]),section.transform.TransformVector(vertices[indices[i+2]]-vertices[indices[i]])).y<0)
                {int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}
            collision.subMeshCount=1;collision.triangles=indices;
            section.GetComponent<MeshCollider>().sharedMesh=Save(collision,Folder+"/Collision "+token+".asset");
            var material=new Material(section.GetComponent<Renderer>().sharedMaterial);
            material.shader=Shader.Find("Game/Living Forest");material.SetFloat("_MudBlend",1);material.SetFloat("_RoadMaterialBlend",0);
            material.SetTexture("_MudMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"farm_furrows_diff_2k.jpg"));
            material.SetTexture("_MudNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"farm_furrows_nor_gl_2k.jpg"));
            material.SetTexture("_MudRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"farm_furrows_rough_2k.jpg"));
            material.SetFloat("_MudUseRoughness",1);material.SetFloat("_MudOverlayFade",0);material.SetFloat("_RoadWetPatch",0);
            material.SetVector("_MudUVScale",Vector4.one);material.SetColor("_MudTint",new Color(.90f,.86f,.79f));
            material.SetVector("_MudHalfSize",new Vector4(section.halfSize.x,section.halfSize.y,feather,0));
            section.GetComponent<Renderer>().sharedMaterial=Save(material,Folder+"/Surface "+token+".mat");
            section.profile=road;section.mudHalfWidthMetres=section.halfSize.x;section.laneCentreMetres=0;
            soil.profile=ground;soil.maximumDepth=ground.maximumDepthMetres;soil.fieldHalfSize=section.halfSize;
            var surface=section.GetComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Mud;
            surface.forwardGrip=.4f;surface.lateralGrip=.44f;surface.rollingResistance=.06f;surface.chainsImproveGrip=true;
            if(section.GetComponent<TireTrackRenderer>()==null)section.gameObject.AddComponent<TireTrackRenderer>();
            section.GetComponent<TireTrackRenderer>().persistentId=soil.persistentId;
            foreach(Transform child in section.transform)
                if(child.name=="Basin puddles"||child.name=="Water in low ruts")child.gameObject.SetActive(false);
            sections++;
        }
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit mode");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var scene=EditorSceneManager.GetActiveScene();string backup="Assets/_Recovery/BeforeFullWidthFurrows.unity";
        if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Scene backup failed");
        foreach(string name in new[]{"farm_furrows_diff_2k.jpg","farm_furrows_nor_gl_2k.jpg","farm_furrows_rough_2k.jpg","farm_furrows_disp_2k.png"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Art+name);if(importer==null)throw new Exception("Texture missing: "+name);
            importer.textureType=name.Contains("nor_gl")?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=name.Contains("diff");importer.wrapMode=TextureWrapMode.Repeat;
            importer.isReadable=name.Contains("disp");importer.SaveAndReimport();
        }
        height=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"farm_furrows_disp_2k.png");
        var source=AssetDatabase.LoadAssetAtPath<MudRoadProfile>("Assets/Game/Generated/NaturalMud/Forest mud lanes.asset");
        road=AssetDatabase.LoadAssetAtPath<MudRoadProfile>(Folder+"/Full width lanes.asset");
        if(road==null){road=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(road,Folder+"/Full width lanes.asset");}
        road.fullWidthFurrows=true;road.edgeBlendMetres=.55f;road.fullWidthBedDepthMetres=.12f;road.furrowReliefMetres=.075f;road.furrowTileMetres=2.1f;
        road.firmGripMultiplier=1.1f;road.softGripMultiplier=.70f;road.buriedRollingResistance=.44f;
        ground=AssetDatabase.LoadAssetAtPath<SoftGroundProfile>(Folder+"/Full width soft soil.asset");
        if(ground==null){ground=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SoftGroundProfile>("Assets/Game/Generated/DeformableMud/Forest soft soil.asset"));AssetDatabase.CreateAsset(ground,Folder+"/Full width soft soil.asset");}
        foreach(var root in scene.GetRootGameObjects())Configure(root);
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Generated/AuthoredLongForest"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid),copy="work/BeforeFullWidth-"+Path.GetFileName(path);
            if(!File.Exists(copy))File.Copy(path,copy);
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Configure(root,Path.GetFileNameWithoutExtension(path));PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        EditorUtility.SetDirty(road);EditorUtility.SetDirty(ground);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Configured "+sections+" full-width mud sections; original perimeter retained, height-map bed and matching collision, persistent wheel ruts.";
    }
}
