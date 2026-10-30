using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class FinishLivingForest
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var skyShader=Shader.Find("Skybox/Procedural");
        if(skyShader!=null)
        {
            var sky=new Material(skyShader);sky.name="Clear winter forest sky";sky.SetFloat("_SunSize",0.025f);sky.SetFloat("_AtmosphereThickness",1.0f);sky.SetColor("_SkyTint",new Color(0.54f,0.60f,0.67f));sky.SetColor("_GroundColor",new Color(0.29f,0.33f,0.31f));sky.SetFloat("_Exposure",0.85f);
            AssetDatabase.CreateAsset(sky,"Assets/Game/Generated/LivingForest/ForestSky.mat");RenderSettings.skybox=sky;Camera.main.clearFlags=CameraClearFlags.Skybox;
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>())if(light.type==LightType.Directional){RenderSettings.sun=light;break;}
            DynamicGI.UpdateEnvironment();
        }
        var root=GameObject.Find("Living forest and river fords");
        var wind=root.GetComponent<ForestWind>();wind.UpdateShader();
        var forest=GameObject.Find("Forest - imported dirt road environment");
        foreach(var renderer in forest.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial.shader.name=="Game/Living Forest")renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return "Saved sky, reflected sunlight and forest reflection-probe blending.";
    }
}
