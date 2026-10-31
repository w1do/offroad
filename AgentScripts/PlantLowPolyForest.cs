using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class PlantLowPolyForest
{
 const string Art="Assets/Game/Art/LowPolyForest/";
 const string Gen="Assets/Game/Generated/LowPolyForest/";
 static float Centre(float z){float s=Mathf.Sin(z*Mathf.PI/50);return 12*s*s*s;}
 static float RoadDistance(Vector3 p){float best=float.MaxValue;for(int i=0;i<1000;i++){float z=-50+i*.1f;var a=new Vector2(Centre(z),z);var b=new Vector2(Centre(z+.1f),z+.1f);var d=b-a;var q=new Vector2(p.x,p.z);best=Mathf.Min(best,Vector2.Distance(q,a+d*Mathf.Clamp01(Vector2.Dot(q-a,d)/d.sqrMagnitude)));}return best;}
 static Material Mat(string name,string diffuse,string opacity=null){
 string path=Gen+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
 m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
 Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"textures/"+diffuse+".png");
 if(opacity!=null){var c=new Texture2D(2,2);var a=new Texture2D(2,2);c.LoadImage(File.ReadAllBytes(Art+"textures/"+diffuse+".png"));a.LoadImage(File.ReadAllBytes(Art+"textures/"+opacity+".png"));var pixels=c.GetPixels();for(int y=0;y<c.height;y++)for(int x=0;x<c.width;x++){int i=y*c.width+x;pixels[i].a=a.GetPixelBilinear(x/(float)c.width,y/(float)c.height).r;}c.SetPixels(pixels);c.Apply();string tp=Gen+name+" RGBA.png";File.WriteAllBytes(tp,c.EncodeToPNG());UnityEngine.Object.DestroyImmediate(c);UnityEngine.Object.DestroyImmediate(a);AssetDatabase.ImportAsset(tp);texture=AssetDatabase.LoadAssetAtPath<Texture2D>(tp);m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.45f);m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;m.SetFloat("_Cull",0);}
 m.SetTexture("_BaseMap",texture);m.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(m,path);return m;
 }
 static GameObject Make(string name,Renderer[] parts,Dictionary<string,Material> materials,bool tree){
 var root=new GameObject(name);Bounds bounds=parts[0].bounds;foreach(var p in parts)bounds.Encapsulate(p.bounds);var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
 foreach(var r in parts){var go=new GameObject(r.name);go.transform.SetParent(root.transform);var source=r.GetComponent<MeshFilter>().sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);mesh.name=name+" "+r.name;mesh.vertices=source.vertices.Select(v=>r.transform.TransformPoint(v)-origin).ToArray();mesh.normals=source.normals.Select(n=>r.transform.TransformDirection(n).normalized).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();string path=Gen+mesh.name+".asset";AssetDatabase.CreateAsset(mesh,path);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=r.sharedMaterials.Select(m=>materials[m.name]).ToArray();if(!tree)go.AddComponent<MeshCollider>().sharedMesh=mesh;}
 if(tree){var col=root.AddComponent<CapsuleCollider>();col.radius=Mathf.Clamp(bounds.size.y*.022f,.16f,.55f);col.height=bounds.size.y*.65f;col.center=new Vector3(0,col.height*.5f,0);}
 var prefab=PrefabUtility.SaveAsPrefabAsset(root,Gen+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
 }
 public static object Main(){
 var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty||scene.path!="Assets/MountainDrive/MountainPass.unity")throw new Exception("Expected saved MountainPass in Edit Mode");if(GameObject.Find("Low poly forest - planted territory")!=null)throw new Exception("Forest already exists");
 Directory.CreateDirectory(Gen);AssetDatabase.Refresh();const string backup="Assets/Game/Scenes/BeforeLowPolyForest.unity";if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
 var materials=new Dictionary<string,Material>{{"Background_Tree_Atlas",Mat("Forest atlas","Background_Tree_Atlas_Diffuse","Background_Tree_Atlas_Opacity")},{"Rocks",Mat("Forest rocks","ROCKS_DIFFUSE")},{"Tree_Branches_01",Mat("Branches 1","Tree_Branches_1_Diffuse","Tree_Branches_1_Opacity")},{"Tree_Branches_02",Mat("Branches 2","Tree_Branches_2_Diffuse","Tree_Branches_2_Opacity")},{"Tree_Trunk_01",Mat("Trunk 1","Tree_Trunk_01_Diffuse")},{"Tree_Trunk_02",Mat("Trunk 2","Tree_Trunk_02_Diffuse")}};
 var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Tree_Pack.fbx"));var renderers=model.GetComponentsInChildren<Renderer>();var trees=new List<GameObject>();var debris=new List<GameObject>();
 foreach(var r in renderers.Where(r=>r.name.StartsWith("Background")||r.name.StartsWith("Rocks"))){bool tree=r.bounds.size.y>10;var p=Make(r.name,new[]{r},materials,tree);if(tree)trees.Add(p);else debris.Add(p);}
 foreach(var trunk in renderers.Where(r=>r.name.StartsWith("Tree_Trunk"))){var branch=renderers.Where(r=>r.name.StartsWith("Tree_Branches")).OrderBy(r=>Mathf.Abs(r.bounds.center.x-trunk.bounds.center.x)).First();trees.Add(Make("Detailed "+trunk.name,new[]{trunk,branch},materials,true));}
 UnityEngine.Object.DestroyImmediate(model);
 var root=new GameObject("Low poly forest - planted territory");var random=new System.Random(3101026);var positions=new List<Vector3>();int treeCount=0,debrisCount=0;float minimumRoad=float.MaxValue;
 for(int attempt=0;attempt<12000&&treeCount<330;attempt++){
 var p=new Vector3(-48+(float)random.NextDouble()*96,0,-48+(float)random.NextDouble()*96);float distance=RoadDistance(p);if(distance<5.6f||positions.Any(q=>(q-p).sqrMagnitude<12.25f))continue;
 var prefab=trees[treeCount%trees.Count];var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(root.transform);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);float scale=.65f+(float)random.NextDouble()*.45f;go.transform.localScale=Vector3.one*scale;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);positions.Add(p);minimumRoad=Mathf.Min(minimumRoad,distance);treeCount++;
 }
 for(int i=0;i<160;i++){
 var p=new Vector3(-48+(float)random.NextDouble()*96,0,-48+(float)random.NextDouble()*96);if(RoadDistance(p)<5.2f){i--;continue;}var go=(GameObject)PrefabUtility.InstantiatePrefab(debris[i%debris.Count]);go.transform.SetParent(root.transform);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);go.transform.localScale=Vector3.one*(.5f+(float)random.NextDouble()*.65f);debrisCount++;
 }
 Physics.SyncTransforms();int roadChecks=0;for(float z=-49;z<50;z+=.5f)for(float x=-2.9f;x<=2.9f;x+=1.45f){var p=new Vector3(Centre(z)+x,0,z);foreach(var c in root.GetComponentsInChildren<Collider>())if(c.Raycast(new Ray(p+Vector3.up*40,Vector3.down),out var hit,41))throw new Exception("Blocked road: "+c.name);roadChecks++;}
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=root;
 return new{success=true,treeCount,debrisCount,treeVariants=trees.Count,debrisVariants=debris.Count,minimumRoad,roadChecks};
 }
}
