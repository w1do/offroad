using System;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class ValidateSurfaceVisualLifecycle
{
    static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name="TEMP surface visual lifecycle";
        var tracks=ground.AddComponent<TireTrackRenderer>();
        var waterRoot=new GameObject("TEMP ripple lifecycle");
        waterRoot.SetActive(false);
        var water=waterRoot.AddComponent<WaterRippleRenderer>();
        try
        {
            tracks.BindPersistence("lifecycle-test");
            tracks.Stamp(Vector3.zero,Vector3.forward,1,1,0,.2f,.8f);
            float before=tracks.TrackAt(Vector3.zero);
            if(before<=0)throw new Exception("Track fixture did not stamp");
            for(int i=0;i<3;i++)
            {
                // Reproduce managed fields lost during script reload and streamed re-enable.
                Field(typeof(TireTrackRenderer),"properties").SetValue(tracks,null);
                typeof(TireTrackRenderer).GetMethod("Upload",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tracks,null);
                var block=new MaterialPropertyBlock();ground.GetComponent<Renderer>().GetPropertyBlock(block);
                if(block.GetTexture("_SoilTrackMask")==null)throw new Exception("Track mask was not rebound");
                tracks.enabled=false;tracks.enabled=true;
                if(tracks.TrackAt(Vector3.zero)!=before)throw new Exception("Re-enable lost retained track");
                Field(typeof(WaterRippleRenderer),"properties").SetValue(water,null);
                water.maximumRipples=8;waterRoot.SetActive(true);
                if(Field(typeof(WaterRippleRenderer),"properties").GetValue(water)==null||
                   (int)Field(typeof(WaterRippleRenderer),"next").GetValue(water)!=0)
                    throw new Exception("Ripple pool did not initialize");
                Field(typeof(WaterRippleRenderer),"next").SetValue(water,63);
                waterRoot.SetActive(false);
            }
            string json="{\"passed\":true,\"reloadCycles\":3,\"trackMaskRebound\":true,\"tracksRetained\":true,\"ripplePoolReset\":true}";
            File.WriteAllText("docs/reports/surface-visual-lifecycle.json",json);return json;
        }
        finally{UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(waterRoot);}
    }
}
