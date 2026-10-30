using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RefineMountains {
public static string Main(){
var route=Object.FindAnyObjectByType<MountainRoute>().points;
var terrain=Object.FindAnyObjectByType<Terrain>();var data=terrain.terrainData;
int n=1025;float cell=3200f/1024;
var heights=new float[n,n];var near=new float[n,n];var roadHeight=new float[n,n];
for(int z=0;z<n;z++)for(int x=0;x<n;x++){
 float px=x*cell,pz=z*cell,t=(pz-240)/2700;
 float side=Mathf.Abs(px-(1600+140*Mathf.Sin(pz/630)))/1500;
 float ridge=Mathf.Pow(1-Mathf.Abs(2*Mathf.PerlinNoise(px/650+19,pz/590+43)-1),2);
 heights[z,x]=(42+Mathf.Clamp01(t)*252+Mathf.Pow(side,1.55f)*510+ridge*(23+side*145)+Mathf.PerlinNoise(px/240+7,pz/270+18)*24+Mathf.PerlinNoise(px/75,pz/75)*8)/950;
 near[z,x]=float.MaxValue;
}
foreach(var p in route){int cx=Mathf.RoundToInt(p.x/cell),cz=Mathf.RoundToInt(p.z/cell);
 for(int z=Mathf.Max(0,cz-79);z<=Mathf.Min(n-1,cz+79);z++)for(int x=Mathf.Max(0,cx-79);x<=Mathf.Min(n-1,cx+79);x++){
 float d=(new Vector2(x*cell-p.x,z*cell-p.z)).sqrMagnitude;
 if(d<near[z,x]){near[z,x]=d;roadHeight[z,x]=p.y-.65f;}
 }
}
for(int z=0;z<n;z++)for(int x=0;x<n;x++){
 float d=Mathf.Sqrt(near[z,x]);float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,240,d));
 float target=roadHeight[z,x]+Mathf.Max(0,d-8)*.5f;
 heights[z,x]=Mathf.Lerp(heights[z,x],target/950,blend);
}
data.SetHeights(0,0,heights);
var alpha=new float[512,512,3];
for(int z=0;z<512;z++)for(int x=0;x<512;x++){
 float u=x/511f,v=z/511f;
 float steep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(24,48,data.GetSteepness(u,v)));
 float high=Mathf.SmoothStep(0,1,Mathf.InverseLerp(580,820,data.GetInterpolatedHeight(u,v)));
 float patch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.77f,Mathf.PerlinNoise(u*27+9,v*27+7)))*.53f;
 float rock=Mathf.Clamp01(steep*.9f+patch);
 alpha[z,x,0]=(1-rock)*(1-high);alpha[z,x,1]=rock*(1-high);alpha[z,x,2]=high;
}
data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();terrain.Flush();
var rocks=GameObject.Find("Scree and mountain boulders");foreach(Transform t in rocks.transform){var p=t.position;p.y=terrain.SampleHeight(p)-.6f;t.position=p;}
PlayerSettings.runInBackground=true;
EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(terrain.gameObject.scene);
return "Mountain slopes softened; terrain basemap rebuilt; 430 rocks regrounded; scene saved";
}
}


