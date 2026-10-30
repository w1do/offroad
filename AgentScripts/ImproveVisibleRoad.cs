using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ImproveVisibleRoad
{
 public static string Main(){
 if(Application.isPlaying)throw new Exception("Edit Mode required");
 AssetDatabase.Refresh();
 int count=0;
 foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Game"})){
 var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
 if(m==null||m.shader.name!="Game/Living Forest"||!m.name.Contains("Dirt_Road")||m.GetFloat("_MudBlend")>.5f)continue;
 m.SetFloat("_RoadMaterialBlend",1);m.SetFloat("_RoadTextureScale",1/3f);
 m.SetTexture("_RoadMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/dry_mud_field_001_diff_2k.jpg"));
 m.SetTexture("_RoadNormal",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/dry_mud_field_001_nor_gl_2k.jpg"));
 m.SetTexture("_RoadRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Puddles/dry_mud_field_001_rough_2k.jpg"));
 m.SetFloat("_RoadLeavesBlend",0);
 m.SetFloat("_RoadWetPatch",0);
 EditorUtility.SetDirty(m);count++;
 }
 AssetDatabase.SaveAssets();return "Dry Mud Field 001 PBR on "+count+" road materials; original alpha masks and mesh retained";
 }
}

