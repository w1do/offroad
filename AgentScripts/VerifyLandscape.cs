using UnityEngine;
using UnityEditor.SceneManagement;
public static class VerifyLandscape {
 public static string Main() {
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
 Terrain found=null; foreach(var root in scene.GetRootGameObjects()) {var t=root.GetComponent<Terrain>();if(t!=null)found=t;}
 if(found==null || found.terrainData.terrainLayers.Length!=3 || found.GetComponent<TerrainCollider>()==null)throw new System.Exception("Terrain verification failed");
 var d=found.terrainData;float min=999,max=0;foreach(float h in d.GetHeights(0,0,513,513)){min=Mathf.Min(min,h*230);max=Mathf.Max(max,h*230);}
 if(max-min<50 || scene.isDirty)throw new System.Exception("Height or save verification failed");
 return scene.path+" | saved | TerrainCollider OK | 3 layers | elevation "+min.ToString("F1")+" to "+max.ToString("F1")+" m";
 }
}
