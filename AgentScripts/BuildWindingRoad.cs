using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class BuildWindingRoad
{
 public static float Centre(float z){float s=Mathf.Sin(z*Mathf.PI/50);return 12*s*s*s;}
 static Mesh Save(Mesh mesh,string path){var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
 public static object Main(){
 if(Application.isPlaying)throw new Exception("Stop Play first");var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/MountainDrive/MountainPass.unity"||scene.isDirty)throw new Exception("Expected saved MountainPass");
 var ground=GameObject.Find("Ground - square 100 x 100 m");if(ground==null||GameObject.Find("Winding road - persistent wheel ruts")!=null)throw new Exception("Unexpected scene state");
 const string folder="Assets/Game/Generated/WindingRoad";Directory.CreateDirectory(folder);AssetDatabase.Refresh();const string backup="Assets/Game/Scenes/BeforeWindingRoad.unity";if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
 var profile=ScriptableObject.CreateInstance<SoftGroundProfile>();profile.maximumDepthMetres=.10f;profile.settlementDepthMetres=.025f;profile.rollingCompactionMetresPerSecond=.035f;profile.diggingMetresPerSecond=.045f;profile.maximumBermHeightMetres=.025f;profile.collisionUpdateSeconds=.1f;AssetDatabase.CreateAsset(profile,folder+"/Packed road soil.asset");
 var material=new Material(Shader.Find("Game/Living Forest"));material.name="Bicycle trail with wheel tracks";const string maps="Assets/Game/Art/RoadScans/bicycle-trail-road-tile-texture/";
 material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"BicycleTrailRoad_basecolor.jpg"));material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"BicycleTrailRoad_height.jpg"));material.SetFloat("_BumpScale",.55f);material.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"BicycleTrailRoad_roughness.jpg"));material.SetFloat("_UseRoughness",1);material.SetFloat("_SnowAmount",0);material.SetFloat("_Cull",2);AssetDatabase.CreateAsset(material,folder+"/Bicycle trail with wheel tracks.mat");
 var root=new GameObject("Winding road - persistent wheel ruts");var gv=new List<Vector3>();var guv=new List<Vector2>();var gt=new List<int>();float distance=0;
 const int nx=60,nz=100;
 for(int chunk=0;chunk<10;chunk++){
 float z0=-50+chunk*10;var vertices=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[vertices.Length];var tris=new List<int>();
 for(int row=0;row<=nz;row++){
 float z=z0+row*.1f,c=Centre(z);if(row>0)distance+=Vector2.Distance(new Vector2(Centre(z-.1f),z-.1f),new Vector2(c,z));
 for(int x=0;x<=nx;x++){int i=row*(nx+1)+x;vertices[i]=new Vector3(c-3+x*.1f,0,z-z0);uv[i]=new Vector2(x/(float)nx,distance/6);if(row<nz&&x<nx)tris.AddRange(new[]{i,i+nx+1,i+1,i+1,i+nx+1,i+nx+2});}
 if(row<nz){float next=z+.1f,cn=Centre(next);for(int side=0;side<2;side++){int k=gv.Count;float a=side==0?-50:c+3,b=side==0?c-3:50,an=side==0?-50:cn+3,bn=side==0?cn-3:50;var points=new[]{new Vector3(a,0,z),new Vector3(an,0,next),new Vector3(bn,0,next),new Vector3(b,0,z)};foreach(var p in points){gv.Add(ground.transform.InverseTransformPoint(p));guv.Add(new Vector2((p.x+50)/100,(p.z+50)/100));}gt.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}}
 }
 var mesh=new Mesh{name="Dense winding road "+chunk};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();mesh=Save(mesh,folder+"/Road "+chunk+".asset");
 var go=new GameObject("Road soil "+chunk);go.transform.SetParent(root.transform);go.transform.position=new Vector3(0,0,z0);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;
 var surface=go.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Mud;surface.forwardGrip=.85f;surface.lateralGrip=.8f;surface.rollingResistance=.025f;surface.chainsImproveGrip=true;surface.chainedForwardGrip=.9f;surface.chainedLateralGrip=.85f;
 var ruts=go.AddComponent<SoftGroundRuts>();ruts.profile=profile;ruts.maximumDepth=.10f;ruts.persistentId="winding-road/v1/"+chunk;
 var tracks=go.AddComponent<TireTrackRenderer>();tracks.resolution=512;tracks.persistentId=ruts.persistentId;
 }
 var gravelMesh=new Mesh{name="Gravel outside winding road",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};gravelMesh.SetVertices(gv);gravelMesh.SetUVs(0,guv);gravelMesh.SetTriangles(gt,0);gravelMesh.RecalculateNormals();gravelMesh.RecalculateTangents();gravelMesh.RecalculateBounds();gravelMesh=Save(gravelMesh,folder+"/Gravel outside road.asset");
 var gravel=ground.GetComponent<MeshRenderer>().sharedMaterials[0];ground.GetComponent<MeshFilter>().sharedMesh=gravelMesh;ground.GetComponent<MeshRenderer>().sharedMaterials=new[]{gravel};ground.GetComponent<MeshCollider>().sharedMesh=gravelMesh;Physics.SyncTransforms();
 int checkedRays=0;for(int z=-49;z<50;z++){foreach(float offset in new[]{-3.01f,-2.99f,0,2.99f,3.01f}){float x=Centre(z)+offset;bool found=false;foreach(var collider in root.GetComponentsInChildren<MeshCollider>()){if(collider.Raycast(new Ray(new Vector3(x,3,z),Vector3.down),out var hit,4)&&Mathf.Abs(hit.point.y)<.001f)found=true;}if(ground.GetComponent<MeshCollider>().Raycast(new Ray(new Vector3(x,3,z),Vector3.down),out var gravelHit,4)&&Mathf.Abs(gravelHit.point.y)<.001f)found=true;if(!found)throw new Exception("Road seam gap at "+x+","+z);checkedRays++;}}
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");Selection.activeGameObject=root;
 return new{success=true,chunks=10,verticesPerChunk=(nx+1)*(nz+1),checkedRays,roadLength=distance,maximumRut=.10f};
 }
}
