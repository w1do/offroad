using UnityEngine;

public sealed partial class VehicleAudio
{
    private AudioSource[] coastBands,loadedBands;
    private float audibleRpm,audibleLoad,startupTime;
    public bool RecordedEngine=>coastBands!=null&&coastBands.Length>0;
    public AudioSource[] CoastBands=>coastBands;
    public AudioSource[] LoadedBands=>loadedBands;

    private void InitializeEngineLayers()
    {
        StopEngineLayers();
        if(coastBands!=null)foreach(var source in coastBands)if(source!=null)Destroy(source.gameObject);
        if(loadedBands!=null)foreach(var source in loadedBands)if(source!=null)Destroy(source.gameObject);
        coastBands=null;loadedBands=null;
        audibleRpm=profile.idleRpm;audibleLoad=0;startupTime=0;
        if(!Application.isPlaying||profile.engineBands==null||profile.engineBands.Length==0)return;
        float previous=0;
        foreach(var band in profile.engineBands){if(band==null||band.coast==null||band.loaded==null||band.referenceRpm<=previous){Debug.LogError("Invalid recorded engine audio bands",this);return;}previous=band.referenceRpm;}
        if(coastBands==null){
            coastBands=new AudioSource[profile.engineBands.Length];loadedBands=new AudioSource[coastBands.Length];
            for(int i=0;i<coastBands.Length;i++){
                coastBands[i]=EngineSource("Engine coast "+i,profile.engineBands[i].coast);
                loadedBands[i]=EngineSource("Engine load "+i,profile.engineBands[i].loaded);
            }
        }
        for(int i=0;i<coastBands.Length;i++){coastBands[i].volume=0;loadedBands[i].volume=0;coastBands[i].Play();loadedBands[i].Play();}
        if(profile.engineStart!=null){effects.PlayOneShot(profile.engineStart,profile.engineVolume*profile.masterVolume);startupTime=profile.startupBlendSeconds;}
    }
    private AudioSource EngineSource(string label,AudioClip clip)
    {
        var root=new GameObject(label);root.transform.SetParent(transform,false);root.transform.localPosition=profile.enginePosition;
        var source=root.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.playOnAwake=false;source.volume=0;source.spatialBlend=profile.engineSpatialBlend;source.dopplerLevel=0;source.minDistance=profile.minDistance;source.maxDistance=profile.maxDistance;source.rolloffMode=AudioRolloffMode.Logarithmic;source.priority=80;return source;
    }
    private void StopEngineLayers()
    {
        if(coastBands!=null)foreach(var source in coastBands)if(source!=null)source.Stop();
        if(loadedBands!=null)foreach(var source in loadedBands)if(source!=null)source.Stop();
    }
    private void MixEngineLayers(float dt)
    {
        if(dt<=0)return;
        audibleRpm=Mathf.Lerp(audibleRpm,Mathf.Max(profile.idleRpm,EngineRpm),1-Mathf.Exp(-profile.rpmResponse*dt));
        float engineLoad=Common?Mathf.Clamp01(vehicle.EngineLoad*.65f+Pedal*.35f):Pedal;
        audibleLoad=Mathf.Lerp(audibleLoad,engineLoad,1-Mathf.Exp(-profile.loadResponse*dt));
        int low=0;while(low<profile.engineBands.Length-1&&audibleRpm>profile.engineBands[low+1].referenceRpm)low++;
        int high=Mathf.Min(low+1,profile.engineBands.Length-1);
        float blend=low==high?0:Mathf.InverseLerp(profile.engineBands[low].referenceRpm,profile.engineBands[high].referenceRpm,audibleRpm);
        startupTime=Mathf.Max(0,startupTime-dt);float fade=profile.startupBlendSeconds>0?1-startupTime/profile.startupBlendSeconds:1;
        float gain=profile.engineVolume*profile.masterVolume*fade;
        for(int i=0;i<coastBands.Length;i++){
            float weight=i==low?Mathf.Sqrt(1-blend):i==high?Mathf.Sqrt(blend):0;
            float pitch=Mathf.Clamp(audibleRpm/profile.engineBands[i].referenceRpm,profile.minimumRecordedPitch,profile.maximumRecordedPitch);
            coastBands[i].pitch=pitch;loadedBands[i].pitch=pitch;
            Smooth(coastBands[i],gain*profile.engineBands[i].gain*weight*Mathf.Sqrt(1-audibleLoad)*.75f,dt,profile.loadResponse);
            Smooth(loadedBands[i],gain*profile.engineBands[i].gain*weight*Mathf.Sqrt(audibleLoad),dt,profile.loadResponse);
        }
    }
}
