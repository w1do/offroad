using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class FeatherMudDecorations
{
    const string Folder="Assets/Game/Generated/NaturalMud/";
    static int Apply(GameObject[] roots,string token)
    {
        var sections=roots.SelectMany(r=>r.GetComponentsInChildren<MudRoadSection>()).Where(s=>s.enabled).ToArray();if(sections.Length==0)return 0;
        int count=0;
        foreach(var f in roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).ToArray())
        {
            var renderer=f.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled||f.sharedMesh==null)continue;
            var mats=renderer.sharedMaterials;
            if(!mats.Any(m=>m!=null&&(m.name.Contains("Rock_Decal")||m.name.Contains("Fallen_Maple_Leaves"))))continue;
            if(!sections.Any(s=>s.GetComponent<Renderer>().bounds.Intersects(renderer.bounds)))continue;
            var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);mesh.name="Feathered road decoration "+token+" "+f.name;
            var a=new List<Vector2>();var b=new List<Vector2>();
            foreach(var v in mesh.vertices)
            {
                var world=f.transform.TransformPoint(v);var local=sections[0].transform.InverseTransformPoint(world);a.Add(new Vector2(local.x,local.z));
                local=sections.Length>1?sections[1].transform.InverseTransformPoint(world):new Vector3(10000,0,10000);b.Add(new Vector2(local.x,local.z));
            }
            mesh.SetUVs(3,a);mesh.SetUVs(4,b);string path=Folder+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);old=mesh;}else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}f.sharedMesh=old;
            for(int i=0;i<mats.Length;i++)
            {
                var source=mats[i];if(source==null||!(source.name.Contains("Rock_Decal")||source.name.Contains("Fallen_Maple_Leaves")))continue;
                string matPath=Folder+"Feather "+token+" "+source.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mat==null){mat=new Material(source);AssetDatabase.CreateAsset(mat,matPath);}
                mat.SetFloat("_MudBlend",1);mat.SetFloat("_MudOverlayFade",1);var half=sections[0].halfSize;mat.SetVector("_MudHalfSize",new Vector4(half.x,half.y,sections[0].profile.edgeBlendMetres,0));
                half=sections.Length>1?sections[1].halfSize:Vector2.zero;mat.SetVector("_MudHalfSize2",new Vector4(half.x,half.y,1.05f,0));mats[i]=mat;EditorUtility.SetDirty(mat);
            }
            renderer.sharedMaterials=mats;EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(f);count++;
        }
        return count;
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit Mode");AssetDatabase.Refresh();var scene=EditorSceneManager.GetActiveScene();int count=Apply(scene.GetRootGameObjects(),"main");
        var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();foreach(var prefab in route.arms.SelectMany(a=>a.modules).Distinct())
        {
            string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try{count+=Apply(new[]{root},prefab.name);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Feathered "+count+" dry rock/leaf decorative meshes over natural mud; native FBX unchanged.";
    }
}
