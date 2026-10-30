using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ExpandFurrowCorridors
{
    static int Expand(GameObject container,MudRoadSection old,string token,int number)
    {
        if(old.transform.parent.name.StartsWith("Deformable mud fullwidth"))return 0;
        var former=old.transform.parent;
        Vector3 centre=former.position+(token=="main0"?former.right*-.9f:Vector3.zero);
        var profile=old.GetComponent<SoftGroundRuts>().profile;
        var patch=typeof(BuildDeformableMud).GetMethod("Patch",BindingFlags.Static|BindingFlags.NonPublic);
        int parts=(int)patch.Invoke(null,new object[]{container,centre,former.forward,"fullwidth "+token,profile,old.halfSize.y,3.6f});
        if(parts==0)throw new Exception("No real ground for expanded corridor "+token);
        var fresh=container.transform.Find("Deformable mud fullwidth "+token);
        var section=typeof(BuildNaturalMudSections).GetMethod("Section",BindingFlags.Static|BindingFlags.NonPublic);
        typeof(BuildNaturalMudSections).GetField("roadProfile",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,
            AssetDatabase.LoadAssetAtPath<MudRoadProfile>("Assets/Game/Generated/NaturalMud/Forest mud lanes.asset"));
        section.Invoke(null,new object[]{fresh,number,token});
        former.gameObject.SetActive(false);
        return 1;
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit mode");
        var scene=EditorSceneManager.GetActiveScene();int count=0;
        var main=UnityEngine.Object.FindObjectsByType<MudRoadSection>().Where(s=>s.GetComponent<SoftGroundRuts>().persistentId.StartsWith("main")).ToArray();
        foreach(var section in main)
        {
            var roots=scene.GetRootGameObjects();var temporary=new GameObject("TEMP wider road clipping");
            foreach(var root in roots)root.transform.SetParent(temporary.transform,true);
            try
            {
                string token=section.GetComponent<SoftGroundRuts>().persistentId.Split('/')[0];
                count+=Expand(temporary,section,token,token=="main0"?0:1);
                var fresh=temporary.transform.Find("Deformable mud fullwidth "+token);
                if(fresh!=null)fresh.SetParent(null,true);
            }
            finally{foreach(var root in roots)root.transform.SetParent(null,true);UnityEngine.Object.DestroyImmediate(temporary);}
        }
        var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();int index=0;
        foreach(var prefab in route.arms.SelectMany(a=>a.modules).Distinct())
        {
            string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var section in root.GetComponentsInChildren<MudRoadSection>().Where(s=>s.GetComponent<SoftGroundRuts>().enabled).ToArray())
                    count+=Expand(root,section,"module "+prefab.name,index++%5);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Expanded "+count+" corridors to 7.2 m using real source ground, clipped render and collision together. "+ConfigureFullWidthFurrows.Main();
    }
}
