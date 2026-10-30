using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class RestoreForestLightSnow
{
    const string Root="Assets/Game/Generated/ForestLightSnow";
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var scene=SceneManager.GetActiveScene();
        var forest=scene.GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("Forest - imported"));
        if(forest==null)throw new Exception("Original forest root missing.");
        var shader=Shader.Find("Game/Forest Light Snow");if(shader==null)throw new Exception("Snow shader not imported.");
        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").ToArray();
        if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.message)));
        string backup="Assets/Game/Scenes/MountainClimb_WithAudio.unity";
        if(File.Exists(backup))throw new Exception("Backup already exists; refusing repeated restore.");
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup save failed.");
        Vector3 spawn;Quaternion rotation;
        var reference=EditorSceneManager.OpenScene("Assets/Game/Scenes/BeforeMountainClimb.unity",OpenSceneMode.Additive);
        try
        {
            var oldTruck=reference.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestKamazController>(true)).First();
            spawn=oldTruck.transform.position;rotation=oldTruck.transform.rotation;
        }
        finally{EditorSceneManager.CloseScene(reference,true);SceneManager.SetActiveScene(scene);}
        var stage=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Mountain climb stage");if(stage!=null)stage.SetActive(false);
        forest.SetActive(true);Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        var copies=new Dictionary<Material,Material>();int count=0;
        foreach(var renderer in forest.GetComponentsInChildren<MeshRenderer>(true))
        {
            var original=renderer.sharedMaterial;
            if(!copies.TryGetValue(original,out var material))
            {
                material=new Material(original);material.name=original.name+"_LightSnow";material.shader=shader;
                string name=original.name;
                bool road=name.Contains("Dirt_Road")||name.Contains("Road_Edge")||name.Contains("Puddle");
                bool foliage=name.Contains("Tree_Atlas")||name.Contains("Leaves")||name.Contains("Vegetation")||name.Contains("Bush");
                float coverage=road?0.23f:foliage?0.22f:name.Contains("Trunk")?0.17f:0.42f;
                material.SetFloat("_SnowAmount",coverage);material.SetFloat("_SnowScale",0.62f);
                material.SetColor("_SnowColor",new Color(0.88f,0.92f,0.95f));
                material.SetFloat("_Cull",0);material.SetFloat("_AlphaClip",original.HasProperty("_AlphaClip")?original.GetFloat("_AlphaClip"):0);
                if(name.Contains("Puddle")){material.SetFloat("_Smoothness",0.76f);material.SetColor("_BaseColor",new Color(0.74f,0.81f,0.83f));}
                material.enableInstancing=true;
                AssetDatabase.CreateAsset(material,Root+"/"+material.name+".mat");copies[original]=material;
            }
            renderer.sharedMaterial=material;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);count++;
        }
        var car=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestKamazController>(true)).First();
        car.transform.SetPositionAndRotation(spawn,rotation);car.lowRange=false;car.reverseGear=false;car.testControl=false;car.testThrottle=0;car.testBrake=false;
        var camera=UnityEngine.Object.FindAnyObjectByType<DriveCamera>();camera.target=car.transform;camera.orbitYaw=0;camera.orbitPitch=18;camera.followDistance=11;
        camera.transform.position=spawn-car.transform.forward*11+Vector3.up*4.5f;camera.transform.LookAt(spawn+Vector3.up*1.6f);
        RenderSettings.fog=true;RenderSettings.fogDensity=0.0013f;RenderSettings.fogColor=new Color(0.69f,0.75f,0.78f);
        Camera.main.backgroundColor=RenderSettings.fogColor;
        AddSnowfall(car.transform);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        Selection.activeGameObject=car.gameObject;
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(spawn+Vector3.up,Quaternion.Euler(25,rotation.eulerAngles.y,0),18);
        return "Restored original forest geometry/material textures with light patchy snow: "+count+" renderers, "+copies.Count+" material variants. Mountain stage preserved in "+backup+". Truck/audio/chains retained.";
    }
    static void AddSnowfall(Transform car)
    {
        var root=new GameObject("Light snowfall - original forest");var follow=root.AddComponent<LightSnowfall>();follow.target=car;root.transform.position=car.position+Vector3.up*13;
        var particles=root.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.duration=20;main.loop=true;main.prewarm=true;main.startLifetime=new ParticleSystem.MinMaxCurve(14,20);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(0.018f,0.04f);main.maxParticles=600;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startColor=new Color(0.94f,0.97f,1,0.65f);
        var emission=particles.emission;emission.rateOverTime=24;
        var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(55,1,55);
        var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=0.12f;velocity.y=-0.85f;velocity.z=0.06f;
        var noise=particles.noise;noise.enabled=true;noise.strength=0.17f;noise.frequency=0.16f;noise.scrollSpeed=0.12f;
        var color=particles.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(0.65f,0.1f),new GradientAlphaKey(0.65f,0.8f),new GradientAlphaKey(0,1)});color.color=gradient;
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;float alpha=Mathf.Pow(Mathf.Clamp01(1-r),1.7f);texture.SetPixel(x,y,new Color(1,1,1,alpha));}
        texture.Apply();string path=Root+"/Snowflake.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;AssetDatabase.CreateAsset(mat,Root+"/LightSnowflakes.mat");
        var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        particles.Play();
    }
}
