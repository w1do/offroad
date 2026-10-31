using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class ApplyBicycleRoad
{
 public static object Main()
 {
  if(Application.isPlaying)throw new Exception("Stop Play mode first");
  var scene=SceneManager.GetActiveScene();
  if(scene.path!="Assets/MountainDrive/MountainPass.unity"||scene.isDirty)throw new Exception("Expected saved MountainPass scene");
  var ground=GameObject.Find("Ground - square 100 x 100 m");
  if(ground==null)throw new Exception("Ground missing");
  const string maps="Assets/Game/Art/RoadScans/bicycle-trail-road-tile-texture/";
  const string folder="Assets/Game/Generated/BicycleRoad";
  Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  const string backup="Assets/Game/Scenes/BeforeBicycleRoad.unity";
  if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
  foreach(var file in Directory.GetFiles(maps,"*.jpg"))
  {
   var ti=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
   ti.sRGBTexture=file.Contains("basecolor");ti.wrapMode=TextureWrapMode.Repeat;ti.anisoLevel=8;
   ti.isReadable=file.Contains("roughness");
   if(file.Contains("height")){ti.textureType=TextureImporterType.NormalMap;ti.convertToNormalmap=true;ti.heightmapScale=.015f;}
   ti.SaveAndReimport();
  }
  var rough=AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"BicycleTrailRoad_roughness.jpg");
  var packed=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,false,true);
  try{
   var pixels=rough.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(0,0,0,(byte)(255-pixels[i].r));
   packed.SetPixels32(pixels);packed.Apply();File.WriteAllBytes(folder+"/MetallicSmoothness.png",packed.EncodeToPNG());
  }finally{UnityEngine.Object.DestroyImmediate(packed);}
  AssetDatabase.Refresh();
  var pi=(TextureImporter)AssetImporter.GetAtPath(folder+"/MetallicSmoothness.png");pi.sRGBTexture=false;pi.alphaSource=TextureImporterAlphaSource.FromInput;pi.SaveAndReimport();
  var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Bicycle trail road.mat");
  if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,folder+"/Bicycle trail road.mat");}
  mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"BicycleTrailRoad_basecolor.jpg"));
  mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"BicycleTrailRoad_height.jpg"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.55f);
  mat.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"BicycleTrailRoad_ambientOcclusion.jpg"));mat.EnableKeyword("_OCCLUSIONMAP");mat.SetFloat("_OcclusionStrength",.6f);
  mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/MetallicSmoothness.png"));mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",1);
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Ground with road.asset");
  if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,folder+"/Ground with road.asset");}else mesh.Clear();
  var vertices=new Vector3[12];var uv=new Vector2[12];var edges=new[]{-50f,-3f,3f,50f};
  for(int s=0;s<3;s++)
  {
   float l=edges[s],r=edges[s+1];int k=s*4;
   var world=new[]{new Vector3(l,0,-50),new Vector3(l,0,50),new Vector3(r,0,50),new Vector3(r,0,-50)};
   for(int j=0;j<4;j++){vertices[k+j]=ground.transform.InverseTransformPoint(world[j]);uv[k+j]=s==1?new Vector2((world[j].x+3)/6,(world[j].z+50)/6):new Vector2((world[j].x+50)/100,(world[j].z+50)/100);}
  }
  mesh.vertices=vertices;mesh.uv=uv;mesh.subMeshCount=2;
  mesh.SetTriangles(new[]{0,1,2,0,2,3,8,9,10,8,10,11},0);mesh.SetTriangles(new[]{4,5,6,4,6,7},1);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
  var renderer=ground.GetComponent<MeshRenderer>();var gravel=renderer.sharedMaterials[0];
  ground.GetComponent<MeshFilter>().sharedMesh=mesh;renderer.sharedMaterials=new[]{gravel,mat};
  var col=ground.GetComponent<MeshCollider>();col.sharedMesh=null;col.sharedMesh=mesh;Physics.SyncTransforms();
  int checks=0;foreach(float x in new[]{-49f,-3.01f,-3f,-2.99f,0,2.99f,3f,3.01f,49f})foreach(float z in new[]{-49f,0,49f}){if(!col.Raycast(new Ray(new Vector3(x,10,z),Vector3.down),out var hit,20)||Mathf.Abs(hit.point.y)>.001f)throw new Exception("Contact mismatch");checks++;}
  EditorUtility.SetDirty(mesh);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
  Selection.activeGameObject=ground;
  if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(Vector3.zero,Quaternion.Euler(55,20,0),45);
  return new {success=true,scene=scene.path,roadWidth=6,roadLength=100,rayChecks=checks,materials=new[]{gravel.name,mat.name},matchingCollider=col.sharedMesh==mesh};
 }
}
