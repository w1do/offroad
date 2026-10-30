using UnityEngine;
using UnityEngine.Rendering;

// One time-sliced capture for the nearby puddles, shared by their shader.
public sealed class PuddleReflections : MonoBehaviour
{
    public PuddleEffectsProfile profile;
    ReflectionProbe probe;
    int renderId=-1;
    float nextCapture;
    void Start()
    {
        if(profile==null)return;
        var root=new GameObject("Nearby puddle reflection capture");root.transform.SetParent(transform,false);
        probe=root.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;
        probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution=profile.reflectionResolution;probe.cullingMask=~(1<<4);
        probe.shadowDistance=0;probe.nearClipPlane=.05f;probe.farClipPlane=160;
        probe.size=Vector3.one*120;probe.clearFlags=ReflectionProbeClearFlags.Skybox;
    }
    void Update()
    {
        if(probe==null)return;
        if(renderId>=0)
        {
            if(!probe.IsFinishedRendering(renderId))return;
            Shader.SetGlobalTexture("_PuddleReflectionCube",probe.texture);
            Shader.SetGlobalFloat("_PuddleReflectionReady",1);renderId=-1;
        }
        if(Time.unscaledTime<nextCapture)return;
        nextCapture=Time.unscaledTime+Mathf.Max(1,profile.reflectionRefreshSeconds);
        WaterVolume nearest=null;float distance=profile.reflectionDistanceMetres*profile.reflectionDistanceMetres;
        foreach(var water in Object.FindObjectsByType<WaterVolume>())
        {
            if(water.surfaceMesh==null||water.name!="Basin puddles")continue;
            var p=water.surfaceMesh.GetComponent<Renderer>().bounds.center;
            float d=(p-transform.position).sqrMagnitude;
            if(d<distance){distance=d;nearest=water;}
        }
        if(nearest==null)return;
        probe.transform.position=nearest.surfaceMesh.GetComponent<Renderer>().bounds.center+Vector3.up*.25f;
        renderId=probe.RenderProbe();
    }
    void OnDisable(){if(probe!=null)Shader.SetGlobalFloat("_PuddleReflectionReady",0);}
    void OnDestroy(){if(probe!=null)Destroy(probe.gameObject);}
}
