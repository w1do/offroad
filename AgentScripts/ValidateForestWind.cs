using System;
using System.Linq;
using UnityEngine;
public static class ValidateForestWind {
 public static object Main(){
 if(!Application.isPlaying)throw new Exception("Requires Play Mode");
 var root=GameObject.Find("Low poly forest - planted territory");var audio=root.GetComponent<ForestWindAudio>();var wind=UnityEngine.Object.FindAnyObjectByType<ForestWind>();
 var trees=root.GetComponentsInChildren<ForestTreeWind>();var voices=audio.GetComponentsInChildren<AudioSource>();
 var renderers=trees.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).ToArray();var block=new MaterialPropertyBlock();int configured=0;foreach(var r in renderers){r.GetPropertyBlock(block);if(block.GetFloat("_TreeSway")>0&&block.GetFloat("_LocalWindAnchor")==1)configured++;}
 if(trees.Length!=330||voices.Length!=4||configured!=renderers.Length||wind.WindTime<=0||audio.PlayedCreaks==0)throw new Exception("Wind validation failed: trees="+trees.Length+" voices="+voices.Length+" configured="+configured+" creaks="+audio.PlayedCreaks+" time="+wind.WindTime);
 return new{success=true,trees=trees.Length,configuredRenderers=configured,windTime=wind.WindTime,creaks=audio.PlayedCreaks,voices=voices.Length,playingVoices=voices.Count(v=>v.isPlaying),shaderErrors=UnityEditor.ShaderUtil.GetShaderMessages(Shader.Find("Game/Living Forest")).Count(m=>m.severity.ToString()=="Error")};
 }
}
