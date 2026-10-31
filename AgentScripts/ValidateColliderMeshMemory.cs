using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
public static class ValidateColliderMeshMemory {
public static object Main(){var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();var block=route.CreateBlock(0,1001);try{long visible=0,collision=0;int count=0;foreach(var soil in block.GetComponentsInChildren<SoftGroundRuts>()){
var render=soil.GetComponent<MeshFilter>().sharedMesh;var physical=soil.GetComponent<MeshCollider>().sharedMesh;if(render==null||physical==null||physical.name!="Matching mud collision")continue;
if(!render.vertices.SequenceEqual(physical.vertices))throw new Exception("Visible/collision vertices differ");
var a=render.triangles;var b=physical.triangles;if(a.Length!=b.Length)throw new Exception("Triangle count differs");
for(int t=0;t<a.Length;t+=3)if(a[t]!=b[t]||!((a[t+1]==b[t+1]&&a[t+2]==b[t+2])||(a[t+1]==b[t+2]&&a[t+2]==b[t+1])))throw new Exception("Triangle geometry differs");if(physical.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))throw new Exception("Collision still retains UVs");visible+=Profiler.GetRuntimeMemorySizeLong(render);collision+=Profiler.GetRuntimeMemorySizeLong(physical);count++;}
if(count==0||collision>=visible)throw new Exception("No mesh memory reduction");var result=new {passed=true,patches=count,visibleBytes=visible,collisionBytes=collision,savedAgainstFullCloneBytes=visible-collision,geometryExact=true};System.IO.File.WriteAllText("docs/reports/collider-mesh-memory.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;}finally{UnityEngine.Object.DestroyImmediate(block);}}}
