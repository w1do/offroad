using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FinishWindingRoad{
 static void OnState(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode)return;EditorApplication.playModeStateChanged-=OnState;Main();}
 public static object Main(){if(Application.isPlaying){EditorApplication.playModeStateChanged+=OnState;EditorApplication.isPlaying=false;return new{queued=true};}var ground=GameObject.Find("Ground - square 100 x 100 m");var root=GameObject.Find("Winding road - persistent wheel ruts");var colliders=root.GetComponentsInChildren<MeshCollider>().Concat(new[]{ground.GetComponent<MeshCollider>()}).ToArray();int rays=0;for(int z=-49;z<50;z++)foreach(float offset in new[]{-3.01f,-2.99f,0,2.99f,3.01f}){var ray=new Ray(new Vector3(12*Mathf.Pow(Mathf.Sin(z*Mathf.PI/50),3)+offset,3,z),Vector3.down);bool found=false;foreach(var c in colliders)if(c.Raycast(ray,out var h,4)&&Mathf.Abs(h.point.y)<.001f)found=true;if(!found)throw new Exception("Seam gap");rays++;}AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(ground.scene);EditorSceneManager.SaveScene(ground.scene);return new{success=true,rays};}
}

