using System;
using System.IO;
using System.Linq;
using UnityEngine;
public static class ValidateWholeRoadPersistence
{
 public static string Main(){
 if(!Application.isPlaying)throw new Exception("Play mode required");
 var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();GameObject first=null,second=null;
 try{
 first=route.CreateBlock(0,101);
 var soil=first.GetComponentsInChildren<SoftGroundRuts>().First(s=>s.profile!=null&&s.profile.soil!=null&&s.profile.soil.soilType==SoilType.DryDirt&&s.GetComponent<MeshFilter>().sharedMesh.vertexCount>500);
 var mesh=soil.GetComponent<MeshFilter>().sharedMesh;int[] t=mesh.triangles;var v=mesh.vertices;int i=(t.Length/6)*3;i=Mathf.Min(i,t.Length-3);Vector3 point=soil.transform.TransformPoint((v[t[i]]+v[t[i+1]]+v[t[i+2]])/3);
 soil.Dig(point,1,1);soil.Commit();var tracks=soil.GetComponent<TireTrackRenderer>();tracks.Stamp(point,Vector3.forward,.7f,.7f,0,.16f,.4f);
 float before=soil.DepthAt(point),mark=tracks.TrackAt(point);string id=soil.persistentId;
 if(before<.001f||mark<=0)throw new Exception("No road rut/tread in fixture");
 UnityEngine.Object.DestroyImmediate(first);first=null;second=route.CreateBlock(0,101);
 var restored=second.GetComponentsInChildren<SoftGroundRuts>().First(s=>s.persistentId==id);
 float after=restored.DepthAt(point),restoredMark=restored.GetComponent<TireTrackRenderer>().TrackAt(point);
 if(Mathf.Abs(before-after)>.001f||Mathf.Abs(mark-restoredMark)>.001f)throw new Exception("Dry road lost tracks after streaming");
 string report="PASS full route block unload/recreate: "+id+"; dry rut="+before+" -> "+after+"m; tread="+mark+" -> "+restoredMark;
 File.WriteAllText("work/whole-road-persistence.txt",report);return report;
 }finally{if(first!=null)UnityEngine.Object.DestroyImmediate(first);if(second!=null)UnityEngine.Object.DestroyImmediate(second);}
 }
}
