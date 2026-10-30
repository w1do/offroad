using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ConfigureMudWheelEffects
{
    const string Folder="Assets/Game/VFX/Mud";
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit Mode");
        AssetDatabase.Refresh();
        var shader=Shader.Find("Game/Wet Mud Spray"); if(shader==null)throw new Exception("Missing mud shader");
        var p=AssetDatabase.LoadAssetAtPath<VehicleWheelEffectsProfile>(Folder+"/Mud wheel effects.asset");
        if(p==null){p=ScriptableObject.CreateInstance<VehicleWheelEffectsProfile>();AssetDatabase.CreateAsset(p,Folder+"/Mud wheel effects.asset");}
        p.dropletSizeMetres=new Vector2(.012f,.048f);
        p.splashMaterials=new Material[3];int[] masks={2,19,26};
        for(int i=0;i<masks.Length;i++)
        {
            string texturePath=Folder+"/splat"+masks[i].ToString("00")+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.textureType=TextureImporterType.Default;
            importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.maxTextureSize=256;importer.SaveAndReimport();
            string path=Folder+"/Wet splat "+i+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.65f);
            EditorUtility.SetDirty(mat);p.splashMaterials[i]=mat;
        }
        string clodPath=Folder+"/Wet earth clods.mat";
        var clodMat=AssetDatabase.LoadAssetAtPath<Material>(clodPath);
        if(clodMat==null){var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/DeformableMud/Wet Mud material0.mat");clodMat=new Material(source);AssetDatabase.CreateAsset(clodMat,clodPath);}
        clodMat.SetFloat("_Smoothness",.62f);EditorUtility.SetDirty(clodMat);p.clodMaterial=clodMat;
        p.clodMeshes=new Mesh[3];
        var random=UnityEngine.Random.state;
        try{for(int i=0;i<3;i++)
        {
            string path=Folder+"/Earth clod "+i+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                var temp=GameObject.CreatePrimitive(PrimitiveType.Sphere);mesh=UnityEngine.Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(temp);
                mesh.name="Earth clod "+i;var vertices=mesh.vertices;
                for(int j=0;j<vertices.Length;j++)
                {
                    Vector3 v=vertices[j];float noise=Mathf.PerlinNoise(v.x*7+i*3,v.z*9+v.y*5+i)*.65f+.65f;
                    vertices[j]=Vector3.Scale(v,new Vector3(1+i*.15f,.55f+i*.13f,.85f))*noise;
                }
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            p.clodMeshes[i]=mesh;
        }}finally{UnityEngine.Random.state=random;}
        EditorUtility.SetDirty(p);
        var car=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();if(car==null)throw new Exception("No active vehicle");
        car.profile.wheelEffects=p;EditorUtility.SetDirty(car.profile);
        if(car.GetComponent<VehicleWheelEffects>()==null)car.gameObject.AddComponent<VehicleWheelEffects>();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(car.gameObject.scene);EditorSceneManager.SaveScene(car.gameObject.scene);
        return "Saved free CC0 mud masks, 3 textured clod meshes, 3 wet materials, shared profile assigned to "+car.name;
    }
}
