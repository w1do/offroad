using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class CaptureForestWindMotion {
 public static object Main(){
 var go=new GameObject("Wind validation camera");var camera=go.AddComponent<Camera>();var rt=new RenderTexture(960,640,24);var texture=new Texture2D(960,640,TextureFormat.RGB24,false);var previous=RenderTexture.active;var state=Shader.GetGlobalVector("_ForestWindState");bool paused=EditorApplication.isPaused;
 try{EditorApplication.isPaused=true;camera.transform.position=new Vector3(0,3,-46);camera.transform.LookAt(new Vector3(-5,9,-10));camera.farClipPlane=200;camera.targetTexture=rt;Color32[] first=null;int changed=0;
 for(int i=0;i<2;i++){Shader.SetGlobalVector("_ForestWindState",new Vector4(state.x,state.y,state.z,i*4));camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,640),0,0);texture.Apply();var pixels=texture.GetPixels32();if(i==0)first=pixels;else for(int j=0;j<pixels.Length;j++)if(Mathf.Abs(pixels[j].r-first[j].r)+Mathf.Abs(pixels[j].g-first[j].g)+Mathf.Abs(pixels[j].b-first[j].b)>25)changed++;File.WriteAllBytes("work/forest-wind-"+i+".png",texture.EncodeToPNG());}
 if(changed<1000)throw new Exception("No visible motion: "+changed);return new{success=true,changedPixels=changed,totalPixels=960*640};
 }finally{Shader.SetGlobalVector("_ForestWindState",state);EditorApplication.isPaused=paused;RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
 }
}
