using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
public static class ValidateForestAmbience
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play.");
        var ambience=UnityEngine.Object.FindAnyObjectByType<ForestAmbience>();var wind=ambience.forestWind;float time=wind.WindTime;
        var sources=new[]{ambience.birds,ambience.wind}.Concat(UnityEngine.Object.FindObjectsByType<RiverCurrent>().Select(r=>r.GetComponent<AudioSource>())).ToArray();
        var buffer=new float[1024];foreach(var source in sources){if(source.clip==null||!source.isPlaying)throw new Exception("Ambience stopped: "+source.name);source.GetOutputData(buffer,0);}
        await Task.Delay(900);
        float minimum=float.MaxValue;
        foreach(var source in sources)
        {
            source.GetOutputData(buffer,0);double sum=0;foreach(float value in buffer)sum+=value*value;
            float rms=Mathf.Sqrt((float)(sum/buffer.Length));minimum=Mathf.Min(minimum,rms);if(rms<0.000001f)throw new Exception("Silent ambience source "+source.name);
        }
        if(wind.WindTime<=time)throw new Exception("Wind clock not advancing.");
        int vertices=0,meshes=0;
        foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>())
        {
            var material=filter.GetComponent<Renderer>()?.sharedMaterial;
            if(material==null||material.shader.name!="Game/Living Forest"||material.GetFloat("_TreeSway")<=0&&material.GetFloat("_LeafFlutter")<=0)continue;
            var mesh=filter.sharedMesh;
            if(!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2)||mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord2)!=4)throw new Exception("Missing wind anchor vertex stream "+filter.name);
            vertices+=mesh.vertexCount;meshes++;
            if(material.FindPass("ShadowCaster")<0||material.FindPass("DepthOnly")<0)throw new Exception("Wind shadow/depth pass missing.");
        }
        if(meshes<4)throw new Exception("No animated forest meshes.");
        return "PASS 4 recorded bird/wind/river sources playing, minimum output RMS="+minimum.ToString("F6")+"; wind clock advances; "+meshes+" anchored forest meshes / "+vertices+" vertices with wind-aware shadow/depth passes.";
    }
}
