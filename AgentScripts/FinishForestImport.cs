using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class FinishForestImport
{
    public static string Main()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play first.");
        string root="Assets/Game/";
        foreach(string kind in new[]{"Dry","Green"})
        {
            string stem="Grass_Vegetation_"+kind;
            var color=Read(AssetDatabase.LoadAssetAtPath<Texture2D>(root+"Art/Forest/textures/"+stem+"_Diffuse.png"));
            var alpha=Read(AssetDatabase.LoadAssetAtPath<Texture2D>(root+"Art/Forest/textures/Grass_Vegetation_Opacity.png"));
            var pixels=color.GetPixels();
            for(int y=0;y<color.height;y++) for(int x=0;x<color.width;x++)
            {int i=y*color.width+x;pixels[i].a=alpha.GetPixelBilinear((x+0.5f)/color.width,(y+0.5f)/color.height).r;}
            color.SetPixels(pixels);color.Apply();
            string path=root+"Generated/Forest_"+stem+"_Alpha.png";
            File.WriteAllBytes(path,color.EncodeToPNG()); AssetDatabase.ImportAsset(path);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(root+"Generated/Forest_"+stem+".mat");
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",0.35f);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;
            EditorUtility.SetDirty(mat); UnityEngine.Object.DestroyImmediate(color);UnityEngine.Object.DestroyImmediate(alpha);
        }
        var camera=UnityEngine.Object.FindFirstObjectByType<DriveCamera>();
        camera.hoodOffset=new Vector3(-0.6f,2.1f,2.55f); camera.followDistance=11;camera.followHeight=4.8f;
        var car=UnityEngine.Object.FindFirstObjectByType<ForestKamazController>();
        car.testControl=false;car.testThrottle=0;car.testBrake=false;
        AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) throw new Exception("Failed save.");
        return "Foliage transparency repaired; truck camera set; scene saved.";
    }
    static Texture2D Read(Texture source)
    {
        var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(source.width,source.height,0);
        Graphics.Blit(source,rt);RenderTexture.active=rt;
        var t=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
        t.ReadPixels(new Rect(0,0,source.width,source.height),0,0);t.Apply();RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);return t;
    }
}
