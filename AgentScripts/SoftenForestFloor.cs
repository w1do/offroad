using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class SoftenForestFloor{
 static void OnStop(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode)return;EditorApplication.playModeStateChanged-=OnStop;try{Debug.Log("Soft forest: "+Main());}catch(Exception e){Debug.LogException(e);}}
 static float Centre(float z){float s=Mathf.Sin(z*Mathf.PI/50);return 12*s*s*s;}
 public static object Main(){if(Application.isPlaying){EditorApplication.playModeStateChanged+=OnStop;EditorApplication.isPaused=false;EditorApplication.isPlaying=false;return new{queued=true};}
 var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/MountainDrive/MountainPass.unity")throw new Exception("Expected MountainPass");if(GameObject.Find("Soft forest floor - wheel ruts")!=null)return new{alreadyApplied=true};
 var ground=GameObject.Find("Ground - square 100 x 100 m");if(ground==null)throw new Exception("Ground missing");const string backup="Assets/Game/Scenes/BeforeSoftForestFloor.unity";if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
 const string folder="Assets/Game/Generated/SoftForestFloor";Directory.CreateDirectory(folder);AssetDatabase.Refresh();var profile=ScriptableObject.CreateInstance<SoftGroundProfile>();profile.maximumDepthMetres=.07f;profile.settlementDepthMetres=.035f;profile.rollingCompactionMetresPerSecond=.025f;profile.diggingMetresPerSecond=.025f;profile.maximumBermHeightMetres=.015f;profile.displacedSoilFraction=.25f;profile.collisionUpdateSeconds=.12f;AssetDatabase.CreateAsset(profile,folder+"/Forest floor soil.asset");
 var material=ground.GetComponent<MeshRenderer>().sharedMaterial;var root=new GameObject("Soft forest floor - wheel ruts");const int n=64;int chunks=0;
 for(int zchunk=0;zchunk<10;zchunk++)for(int side=0;side<2;side++)for(int strip=0;strip<5;strip++){
 float z0=-50+zchunk*10;var origin=new Vector3(0,0,z0);var v=new Vector3[(n+1)*(n+1)];var uv=new Vector2[v.Length];var t=new int[n*n*6];int ti=0;
 for(int row=0;row<=n;row++){float z=z0+10f*row/n,c=Centre(z);float left=side==0?-50:c+3,right=side==0?c-3:50;float a=Mathf.Lerp(left,right,strip/5f),b=Mathf.Lerp(left,right,(strip+1)/5f);
 for(int x=0;x<=n;x++){int i=row*(n+1)+x;float wx=Mathf.Lerp(a,b,x/(float)n);v[i]=new Vector3(wx,0,z-z0);uv[i]=new Vector2((wx+50)/100,(z+50)/100);if(x<n&&row<n){t[ti++]=i;t[ti++]=i+n+1;t[ti++]=i+1;t[ti++]=i+1;t[ti++]=i+n+1;t[ti++]=i+n+2;}}}
 var mesh=new Mesh{name="Soft forest patch "+chunks};mesh.vertices=v;mesh.uv=uv;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Patch "+chunks+".asset");
 var go=new GameObject("Forest soil "+chunks);go.transform.SetParent(root.transform);go.transform.position=origin;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;
 var surface=go.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Mud;surface.forwardGrip=.70f;surface.lateralGrip=.72f;surface.rollingResistance=.04f;surface.chainsImproveGrip=true;surface.chainedForwardGrip=.8f;surface.chainedLateralGrip=.8f;
 var ruts=go.AddComponent<SoftGroundRuts>();ruts.profile=profile;ruts.maximumDepth=.07f;ruts.persistentId="forest-floor/v1/"+chunks;var tracks=go.AddComponent<TireTrackRenderer>();tracks.resolution=256;tracks.persistentId=ruts.persistentId;chunks++;
 }
 ground.GetComponent<MeshRenderer>().enabled=false;ground.GetComponent<MeshCollider>().enabled=false;Physics.SyncTransforms();
 int rays=0;foreach(var col in root.GetComponentsInChildren<MeshCollider>()){var p=col.bounds.center;if(!col.Raycast(new Ray(p+Vector3.up*2,Vector3.down),out var hit,4)||Mathf.Abs(hit.point.y)>.001f)throw new Exception("Patch ray mismatch");rays++;}
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");return new{success=true,chunks,rays,vertices=chunks*(n+1)*(n+1),settlement=.035f,maximumRut=.07f};
 }
}
