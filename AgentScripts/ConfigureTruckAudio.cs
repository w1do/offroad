using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class ConfigureTruckAudio
{
    const string Root="Assets/Game/Audio/";
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        AssetDatabase.Refresh();
        var truck=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();
        if(truck==null)throw new Exception("Missing KAMAZ.");
        var profile=AssetDatabase.LoadAssetAtPath<VehicleAudioProfile>(Root+"KamazAudio.asset");
        if(profile==null){profile=ScriptableObject.CreateInstance<VehicleAudioProfile>();AssetDatabase.CreateAsset(profile,Root+"KamazAudio.asset");}
        profile.idle=Clip("DieselIdle");profile.load=Clip("DieselLoad");profile.road=Clip("TireRoad");profile.slip=Clip("TireSlip");profile.chains=Clip("ChainRattle");
        profile.brakeAir=Clip("AirBrakeRelease");profile.gear=Clip("GearShift");profile.chainFitting=Clip("ChainFitting");profile.horn=Clip("TruckHorn");
        EditorUtility.SetDirty(profile);
        var old=truck.transform.Find("Truck audio");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var root=new GameObject("Truck audio");root.transform.SetParent(truck.transform,false);
        var audio=truck.GetComponent<VehicleAudio>();if(audio==null)audio=truck.gameObject.AddComponent<VehicleAudio>();audio.profile=profile;
        audio.idle=Source(root.transform,"Diesel idle",profile.idle,new Vector3(0,1.1f,1.8f),true,profile);
        audio.load=Source(root.transform,"Diesel load",profile.load,new Vector3(0,1.1f,1.8f),true,profile);
        audio.road=Source(root.transform,"Tire roll",profile.road,new Vector3(0,0.5f,-0.4f),true,profile);
        audio.slip=Source(root.transform,"Wheel slip",profile.slip,new Vector3(0,0.5f,-1.7f),true,profile);
        audio.chains=Source(root.transform,"Chain rattle",profile.chains,new Vector3(0,0.5f,-1.7f),true,profile);
        audio.effects=Source(root.transform,"Air and mechanical effects",null,new Vector3(0,0.7f,0),false,profile);
        audio.effects.volume=1;
        audio.horn=Source(root.transform,"Air horn - hold H",profile.horn,new Vector3(0,2.4f,2),true,profile);
        EditorUtility.SetDirty(audio);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new Exception("Save failed.");
        return "Saved truck audio: 9 original WAV clips, 7 sources, audio profile; horn H.";
    }
    static AudioClip Clip(string name)
    {
        string path=Root+name+".wav";
        var importer=AssetImporter.GetAtPath(path) as AudioImporter;
        if(importer==null)throw new Exception("Missing imported WAV: "+path);
        importer.forceToMono=true;
        var settings=importer.defaultSampleSettings;settings.preloadAudioData=true;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(clip==null)throw new Exception("Cannot load clip: "+path);return clip;
    }
    static AudioSource Source(Transform root,string name,AudioClip clip,Vector3 pos,bool loop,VehicleAudioProfile profile)
    {
        var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=pos;
        var source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=loop;source.playOnAwake=false;source.volume=0;
        source.spatialBlend=0.7f;source.dopplerLevel=0;source.minDistance=profile.minDistance;source.maxDistance=profile.maxDistance;
        source.rolloffMode=AudioRolloffMode.Linear;source.priority=loop?100:80;return source;
    }
}
