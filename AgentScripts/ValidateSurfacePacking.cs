using System;
using UnityEngine;
public static class ValidateSurfacePacking {
public static object Main(){
 int count=16384;var depth=new float[count];var packing=new float[count];var banks=new float[count];var pixels=new Color32[256*256];
 var random=new System.Random(1347);for(int i=3900;i<4050;i++){depth[i]=(float)random.NextDouble()*.38f;packing[i]=(float)random.NextDouble();banks[i]=(float)random.NextDouble()*.08f;}
 for(int i=10000;i<10500;i++)pixels[i]=new Color32((byte)random.Next(256),180,0,255);
 SurfaceStatePacking.Pack(new float[1],new float[1],new float[1]);SurfaceStatePacking.Pack(new Color32[1]);
 var timer=System.Diagnostics.Stopwatch.StartNew();var soil=SurfaceStatePacking.Pack(depth,packing,banks);var tracks=SurfaceStatePacking.Pack(pixels);timer.Stop();
 var d=new float[count];var p=new float[count];var b=new float[count];var colors=new Color32[pixels.Length];
 if(!SurfaceStatePacking.Restore(soil,d,p,b)||!SurfaceStatePacking.Restore(tracks,colors))throw new Exception("Could not restore");
 for(int i=0;i<count;i++)if(depth[i]!=d[i]||packing[i]!=p[i]||banks[i]!=b[i])throw new Exception("Soil state changed");
 for(int i=0;i<pixels.Length;i++)if(!pixels[i].Equals(colors[i]))throw new Exception("Tread changed");
 if(SurfaceStatePacking.Restore(soil,new float[4],new float[4],new float[4]))throw new Exception("Geometry mismatch accepted");
 var result=new {passed=true,rawSoil=count*12,packedSoil=soil.Length,rawTrack=pixels.Length*4,packedTrack=tracks.Length,packingMs=timer.Elapsed.TotalMilliseconds,exact=true};
 System.IO.File.WriteAllText("docs/reports/surface-cache-memory.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
}}
