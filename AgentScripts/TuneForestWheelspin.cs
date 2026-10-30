using System;
using UnityEngine;
using UnityEditor;
public static class TuneForestWheelspin
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play first");var v=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var p=v.profile;p.dirtLongitudinal=.24f;p.dirtLateral=.58f;p.grassLongitudinal=.24f;p.grassLateral=.42f;p.baseSoilChainsImproveGrip=true;p.baseSoilChainedLongitudinal=.46f;p.baseSoilChainedLateral=.65f;foreach(var surface in UnityEngine.Object.FindObjectsByType<ForestDrivingSurface>())if(surface.cover==ForestDrivingSurface.Cover.Mud){surface.chainedForwardGrip=.46f;EditorUtility.SetDirty(surface);}
 UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(v.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(v.gameObject.scene);EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();return "Saved base forest soil calibration: peak .24, sliding .1368; chains .46 on eligible axles. Effects="+(v.GetComponent<VehicleWheelEffects>()!=null);}
}
