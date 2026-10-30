using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class BuildContinuousForestGround
{
 struct Face {public Vector3 a,b,c;public Vector2 u,v,w;public int material;}
 static List<Face> faces=new List<Face>();static Dictionary<Vector2Int,List<int>> cells=new Dictionary<Vector2Int,List<int>>();
 static Vector2Int Cell(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/8),Mathf.FloorToInt(z/8));
 static Vector3 Bary(Face f,float x,float z){float d=(f.b.z-f.c.z)*(f.a.x-f.c.x)+(f.c.x-f.b.x)*(f.a.z-f.c.z);float a=((f.b.z-f.c.z)*(x-f.c.x)+(f.c.x-f.b.x)*(z-f.c.z))/d;float b=((f.c.z-f.a.z)*(x-f.c.x)+(f.a.x-f.c.x)*(z-f.c.z))/d;return new Vector3(a,b,1-a-b);}
 static int Sample(float x,float z,out float height){height=float.NegativeInfinity;int found=-1;if(!cells.TryGetValue(Cell(x,z),out var list))return found;foreach(int id in list){var f=faces[id];var b=Bary(f,x,z);if(b.x<-.0001f||b.y<-.0001f||b.z<-.0001f)continue;float y=f.a.y*b.x+f.b.y*b.y+f.c.y*b.z;if(y>height){height=y;found=id;}}return found;}
 public static string Main(){
  if(Application.isPlaying)throw new Exception("Stop Play first");var scene=EditorSceneManager.GetActiveScene();string backup="Assets/Game/Scenes/BeforeContinuousGround.unity";
  if(File.Exists(backup)||GameObject.Find("Continuous forest ground")!=null)throw new Exception("Already built; preserve existing scene");EditorSceneManager.SaveScene(scene,backup,true);
  string folder="Assets/Game/Generated/ContinuousGround";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  var forest=GameObject.Find("Forest - imported dirt road environment");var materials=new List<Material>();var sources=new List<MeshFilter>();Bounds bounds=new Bounds();bool first=true;
  foreach(var mf in forest.GetComponentsInChildren<MeshFilter>()){
   var renderer=mf.GetComponent<MeshRenderer>();string n=renderer.sharedMaterial.name;
   if(!(n.Contains("Terrain_Far")||n.Contains("Aerial_Grass")||n.Contains("Ground_Dirt")||n.Contains("Dirt_Road")||n.Contains("Road_Edge")||n.Contains("Grass_Close")||n.Contains("Sloped_Rock")||n.Contains("Tall_Cliff")||n.Contains("Cobblestone")))continue;
   sources.Add(mf);if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
   int mat=materials.IndexOf(renderer.sharedMaterial);if(mat<0){mat=materials.Count;materials.Add(renderer.sharedMaterial);}var mesh=mf.sharedMesh;var p=mesh.vertices;var uv=mesh.uv;var t=mesh.triangles;
   for(int j=0;j<t.Length;j+=3){var a=mf.transform.TransformPoint(p[t[j]]);var b=mf.transform.TransformPoint(p[t[j+1]]);var c=mf.transform.TransformPoint(p[t[j+2]]);if(Vector3.Cross(b-a,c-a).y<.0001f)continue;
    int id=faces.Count;faces.Add(new Face{a=a,b=b,c=c,u=uv[t[j]],v=uv[t[j+1]],w=uv[t[j+2]],material=mat});var lo=Cell(Mathf.Min(a.x,Mathf.Min(b.x,c.x)),Mathf.Min(a.z,Mathf.Min(b.z,c.z)));var hi=Cell(Mathf.Max(a.x,Mathf.Max(b.x,c.x)),Mathf.Max(a.z,Mathf.Max(b.z,c.z)));
    for(int z=lo.y;z<=hi.y;z++)for(int x=lo.x;x<=hi.x;x++){var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<int>();list.Add(id);}
   }
  }
  const float step=1.5f;float x0=Mathf.Floor(bounds.min.x-35),z0=Mathf.Floor(bounds.min.z-35);int nx=Mathf.CeilToInt((bounds.size.x+70)/step)+1,nz=Mathf.CeilToInt((bounds.size.z+70)/step)+1;var height=new float[nx*nz];var ids=new int[height.Length];int holes=0;
  for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=z*nx+x;ids[i]=Sample(x0+x*step,z0+z*step,out height[i]);if(ids[i]<0)holes++;}
  // Fill true holes from neighbouring real surface samples, retaining one continuous top surface.
  var queue=new Queue<int>();for(int i=0;i<ids.Length;i++)if(ids[i]>=0)queue.Enqueue(i);
  while(queue.Count>0){int i=queue.Dequeue(),x=i%nx,z=i/nx;foreach(int n in new[]{x>0?i-1:-1,x<nx-1?i+1:-1,z>0?i-nx:-1,z<nz-1?i+nx:-1})if(n>=0&&ids[n]<0){ids[n]=ids[i];height[n]=height[i];queue.Enqueue(n);}}
  // Raise lower edges to join overlapping plates; never create a second underground driving layer.
  for(int pass=0;pass<24;pass++){for(int z=1;z<nz-1;z++)for(int x=1;x<nx-1;x++){int i=z*nx+x;float high=Mathf.Max(Mathf.Max(height[i-1],height[i+1]),Mathf.Max(height[i-nx],height[i+nx]));height[i]=Mathf.Max(height[i],high-step*1.4f);}}
  // Visible enclosing hills close the finite imported environment instead of an exposed paper edge.
  for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=z*nx+x;float edge=Mathf.Min(Mathf.Min(x,nx-1-x),Mathf.Min(z,nz-1-z))*step;height[i]+=Mathf.Pow(Mathf.Clamp01(1-edge/28),2)*32;}
  var rivers=UnityEngine.Object.FindObjectsByType<RiverCurrent>();
  for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=z*nx+x;var p=new Vector3(x0+x*step,height[i],z0+z*step);foreach(var r in rivers){var q=r.transform.InverseTransformPoint(p);float width=r.HalfWidthAt(q.x),cross=Mathf.Abs(q.z-r.ChannelOffset(q.x));if(q.x<r.MinimumAlong||q.x>r.MaximumAlong||cross>width+5)continue;var bed=r.transform.Find("Gravel ford bed - physical").GetComponent<MeshCollider>();if(bed.Raycast(new Ray(new Vector3(p.x,150,p.z),Vector3.down),out var hit,300)){float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(width+1,width+5,cross));height[i]=Mathf.Lerp(height[i],hit.point.y,blend);}}}
  var root=new GameObject("Continuous forest ground");int triangles=0;
  for(int cz=0;cz<nz-1;cz+=32)for(int cx=0;cx<nx-1;cx+=32){var vertices=new List<Vector3>();var uv=new List<Vector2>();var normals=new List<Vector3>();var bins=materials.Select(m=>new List<int>()).ToArray();
   for(int z=cz;z<Mathf.Min(cz+32,nz-1);z++)for(int x=cx;x<Mathf.Min(cx+32,nx-1);x++){int i=z*nx+x;var face=faces[ids[i]];int start=vertices.Count;foreach(int n in new[]{i,i+nx,i+1,i+nx+1}){int vx=n%nx,vz=n/nx;float wx=x0+vx*step,wz=z0+vz*step;vertices.Add(new Vector3(wx,height[n],wz));var b=Bary(face,wx,wz);uv.Add(face.u*b.x+face.v*b.y+face.w*b.z);float dx=height[vz*nx+Mathf.Min(vx+1,nx-1)]-height[vz*nx+Mathf.Max(vx-1,0)],dz=height[Mathf.Min(vz+1,nz-1)*nx+vx]-height[Mathf.Max(vz-1,0)*nx+vx];normals.Add(new Vector3(-dx,2*step,-dz).normalized);}bins[face.material].AddRange(new[]{start,start+1,start+2,start+2,start+1,start+3});triangles+=2;}
   var mesh=new Mesh{name="Solid ground "+cx+" "+cz,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetNormals(normals);mesh.subMeshCount=materials.Count;for(int m=0;m<bins.Length;m++)mesh.SetTriangles(bins[m],m);mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/"+mesh.name+".asset");var go=new GameObject(mesh.name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();go.AddComponent<MeshCollider>().sharedMesh=mesh;
  }
  foreach(var mf in sources){mf.GetComponent<MeshRenderer>().enabled=false;if(mf.TryGetComponent<MeshCollider>(out var mc))mc.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(mf.GetComponent<MeshRenderer>());if(mc!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(mc);}
  var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var rb=car.GetComponent<Rigidbody>();rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;Physics.SyncTransforms();var pos=car.transform.position;
  var hits=Physics.RaycastAll(new Vector3(pos.x,150,pos.z),Vector3.down,300,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(root.transform)).OrderBy(h=>h.distance).ToArray();if(hits.Length>0){pos.y=hits[0].point.y+.5f;car.transform.position=pos;}
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Built continuous visible/collision ground: "+nx+"x"+nz+" samples, "+triangles+" triangles, filled "+holes+" missing samples; replaced "+sources.Count+" overlapping ground plates. Backup="+backup;
 }
}
