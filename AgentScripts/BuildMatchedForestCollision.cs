using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class BuildMatchedForestCollision
{
 struct Face{public Vector3 a,b,c;public int cover;}
 static List<Face> faces=new List<Face>();static Dictionary<Vector2Int,List<int>> grid=new Dictionary<Vector2Int,List<int>>();
 static Vector2Int Cell(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/8),Mathf.FloorToInt(z/8));
 static int Sample(float x,float z,out float height){height=float.NegativeInfinity;int kind=-1;if(!grid.TryGetValue(Cell(x,z),out var list))return kind;foreach(int id in list){var f=faces[id];float den=(f.b.z-f.c.z)*(f.a.x-f.c.x)+(f.c.x-f.b.x)*(f.a.z-f.c.z);float u=((f.b.z-f.c.z)*(x-f.c.x)+(f.c.x-f.b.x)*(z-f.c.z))/den,v=((f.c.z-f.a.z)*(x-f.c.x)+(f.a.x-f.c.x)*(z-f.c.z))/den;if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;float y=u*f.a.y+v*f.b.y+(1-u-v)*f.c.y;if(y>height){height=y;kind=f.cover;}}return kind;}
 static void Wall(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,float bottom){int n=v.Count;v.AddRange(new[]{a,b,new Vector3(a.x,bottom,a.z),new Vector3(b.x,bottom,b.z)});t.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2,n+2,n+1,n,n+2,n+3,n+1});}
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play first");if(GameObject.Find("Forest solid collision - visible surface")!=null)throw new Exception("Matched forest collision already exists");var scene=EditorSceneManager.GetActiveScene();string backup="Assets/Game/Scenes/BeforePajeroPhysicsV2.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);string folder="Assets/Game/Generated/MatchedForestCollision";Directory.CreateDirectory(folder);AssetDatabase.Refresh();var forest=GameObject.Find("Forest - imported dirt road environment");var sources=new List<MeshFilter>();Bounds bounds=new Bounds();bool first=true;
 foreach(var f in forest.GetComponentsInChildren<MeshFilter>()){string n=f.GetComponent<Renderer>().sharedMaterial.name;if(!(n.Contains("Terrain_Far")||n.Contains("Aerial_Grass")||n.Contains("Ground_Dirt")||n.Contains("Dirt_Road")||n.Contains("Road_Edge")||n.Contains("Grass_Close")||n.Contains("Sloped_Rock")||n.Contains("Tall_Cliff")||n.Contains("Cobblestone")||n.Contains("Mud_Pile")))continue;sources.Add(f);var b=f.GetComponent<Renderer>().bounds;if(first){bounds=b;first=false;}else bounds.Encapsulate(b);int kind=n.Contains("Grass")||n.Contains("Terrain_Far")?1:0;var p=f.sharedMesh.vertices;var t=f.sharedMesh.triangles;
 for(int j=0;j<t.Length;j+=3){var a=f.transform.TransformPoint(p[t[j]]);var bb=f.transform.TransformPoint(p[t[j+1]]);var c=f.transform.TransformPoint(p[t[j+2]]);if(Mathf.Abs(Vector3.Cross(bb-a,c-a).y)<.00001f)continue;int id=faces.Count;faces.Add(new Face{a=a,b=bb,c=c,cover=kind});var lo=Cell(Mathf.Min(a.x,Mathf.Min(bb.x,c.x)),Mathf.Min(a.z,Mathf.Min(bb.z,c.z)));var hi=Cell(Mathf.Max(a.x,Mathf.Max(bb.x,c.x)),Mathf.Max(a.z,Mathf.Max(bb.z,c.z)));for(int z=lo.y;z<=hi.y;z++)for(int x=lo.x;x<=hi.x;x++){var key=new Vector2Int(x,z);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<int>();list.Add(id);}}
 }
 const float step=.75f;float x0=Mathf.Floor(bounds.min.x),z0=Mathf.Floor(bounds.min.z);int nx=Mathf.CeilToInt(bounds.size.x/step)+1,nz=Mathf.CeilToInt(bounds.size.z/step)+1;var heights=new float[nx*nz];var kinds=new int[heights.Length];var valid=new bool[(nx-1)*(nz-1)];int samples=0;
 for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=z*nx+x;kinds[i]=Sample(x0+x*step,z0+z*step,out heights[i]);if(kinds[i]>=0)samples++;}
 for(int z=0;z<nz-1;z++)for(int x=0;x<nx-1;x++){int i=z*nx+x;valid[z*(nx-1)+x]=kinds[i]>=0&&kinds[i+1]>=0&&kinds[i+nx]>=0&&kinds[i+nx+1]>=0;}
 var root=new GameObject("Forest solid collision - visible surface");int chunks=0,triangles=0;
 for(int cz=0;cz<nz-1;cz+=32)for(int cx=0;cx<nx-1;cx+=32){int columns=Math.Min(32,nx-1-cx),rows=Math.Min(32,nz-1-cz);var v=new List<Vector3>();var t=new List<int>();var cover=new int[columns*rows];float px=x0+cx*step,pz=z0+cz*step;
 for(int z=0;z<=rows;z++)for(int x=0;x<=columns;x++){int i=(cz+z)*nx+cx+x;v.Add(new Vector3(x*step,kinds[i]>=0?heights[i]:bounds.min.y,z*step));}
 for(int z=0;z<rows;z++)for(int x=0;x<columns;x++){int gx=cx+x,gz=cz+z,index=gz*(nx-1)+gx;if(!valid[index])continue;int n=z*(columns+1)+x;cover[z*columns+x]=kinds[gz*nx+gx];t.AddRange(new[]{n,n+columns+1,n+1,n+1,n+columns+1,n+columns+2});
 // Close exposed physical edges down through the terrain instead of allowing entry under a paper surface.
 if(gx==0||!valid[index-1])Wall(v,t,v[n+columns+1],v[n],bounds.min.y-30);
 if(gx==nx-2||!valid[index+1])Wall(v,t,v[n+1],v[n+columns+2],bounds.min.y-30);
 if(gz==0||!valid[index-(nx-1)])Wall(v,t,v[n],v[n+1],bounds.min.y-30);
 if(gz==nz-2||!valid[index+(nx-1)])Wall(v,t,v[n+columns+2],v[n+columns+1],bounds.min.y-30);
 }
 if(t.Count==0)continue;var mesh=new Mesh{name="Matched ground "+cx+" "+cz,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/"+mesh.name+".asset");var go=new GameObject(mesh.name);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(px,0,pz);go.AddComponent<MeshCollider>().sharedMesh=mesh;var patch=go.AddComponent<ForestGroundPatch>();patch.step=step;patch.columns=columns;patch.rows=rows;patch.cover=cover;chunks++;triangles+=t.Count/3;
 }
 foreach(var f in sources)if(f.TryGetComponent<MeshCollider>(out var collider)){collider.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Baked upper visible ground collision without modifying a single rendered vertex: "+sources.Count+" source meshes, "+samples+" surface samples, "+chunks+" collision chunks, "+triangles+" triangles including closed edges. Backup="+backup;
 }
}
