using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class RefineMountainClimb
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var stage=UnityEngine.Object.FindAnyObjectByType<MountainClimbStage>();
        var terrain=stage.transform.Find("Snowy mountain terrain").GetComponent<MeshFilter>();var mesh=terrain.sharedMesh;var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++)
        {
            float z=vertices[i].z+180;float sample=Mathf.Clamp(z/2,0,stage.centerline.Length-1.001f);int index=Mathf.FloorToInt(sample);
            var road=Vector3.Lerp(stage.centerline[index],stage.centerline[index+1],sample-index);float side=vertices[i].x-road.x;
            float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(260,285,z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(370,400,z)));
            float bank=fade*8*Mathf.Sin((z-260)/24f)*Mathf.Deg2Rad;
            if(Mathf.Abs(side)<18)vertices[i].y=Mathf.Lerp(road.y+Mathf.Tan(bank)*side-0.26f,vertices[i].y,Mathf.SmoothStep(0,1,Mathf.InverseLerp(10,18,Mathf.Abs(side))));
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        terrain.GetComponent<MeshCollider>().sharedMesh=null;terrain.GetComponent<MeshCollider>().sharedMesh=mesh;
        var snow=new Texture2D(256,256,TextureFormat.RGB24,true);
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {float n=0.78f+Mathf.PerlinNoise(x/13f,y/13f)*0.16f+Mathf.PerlinNoise(x/2.8f,y/2.8f)*0.06f;snow.SetPixel(x,y,new Color(n,n,n));}
        snow.Apply();string path="Assets/Game/Generated/MountainClimb/SnowTexture.png";System.IO.File.WriteAllBytes(path,snow.EncodeToPNG());UnityEngine.Object.DestroyImmediate(snow);AssetDatabase.ImportAsset(path);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        foreach(var name in new[]{"Snowy mountains","Packed snow"})
        {var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/MountainClimb/"+name+".mat");m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",name=="Packed snow"?new Color(0.64f,0.69f,0.73f):new Color(0.84f,0.87f,0.89f));m.SetTextureScale("_BaseMap",name=="Packed snow"?new Vector2(3,1):new Vector2(60,120));EditorUtility.SetDirty(m);}
        foreach(var text in stage.GetComponentsInChildren<TextMesh>())
        {
            text.transform.Rotate(0,180,0,Space.Self);
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="Sign backing";plate.transform.SetParent(text.transform,false);plate.transform.localPosition=new Vector3(0,0,0.08f);plate.transform.localScale=new Vector3(6.5f,2.6f,0.05f);plate.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/MountainClimb/Snowy mountains.mat");UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new Exception("Save failed.");
        return "Terrain clearance repaired, snow texture created, signs face approaching driver; saved.";
    }
}
