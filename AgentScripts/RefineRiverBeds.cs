using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class RefineRiverBeds
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var forest=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name.StartsWith("Forest - imported"));
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Forest/Dirt road scene.fbx");
        var rivers=UnityEngine.Object.FindObjectsByType<RiverCurrent>();int changed=0;
        foreach(var f in forest.GetComponentsInChildren<MeshFilter>())
        {
            string name=f.GetComponent<Renderer>().sharedMaterial.name;
            bool trunk=name.Contains("Trunk_"),foliage=name.Contains("Tree_Atlas")||name.Contains("Vegetation")||name.Contains("Bush");
            if(trunk||foliage||name.Contains("Fence")||name.Contains("Wood_Log"))continue;
            var original=source.GetComponentsInChildren<MeshFilter>().First(g=>g.name==f.name).sharedMesh;
            var mesh=f.sharedMesh;var vertices=original.vertices;var triangles=original.triangles;
            for(int j=0;j<vertices.Length;j++)
            {
                var p=f.transform.TransformPoint(vertices[j]);
                foreach(var river in rivers)
                {
                    var q=river.transform.InverseTransformPoint(p);float cross=Mathf.Abs(q.z-river.ChannelOffset(q.x));
                    if(Mathf.Abs(q.x)>30||cross>river.halfWidth+4)continue;
                    float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(river.halfWidth+4,river.halfWidth-1,cross));
                    blend*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(24,30,Mathf.Abs(q.x)));
                    float bed=river.SurfaceHeight(q.x)-0.62f-Mathf.Clamp01(Mathf.Abs(q.x)/22)*0.38f+Mathf.PerlinNoise(p.x*0.5f,p.z*0.5f)*0.05f;
                    p.y=Mathf.Lerp(p.y,Mathf.Min(p.y,bed),blend);
                }
                vertices[j]=f.transform.InverseTransformPoint(p);
            }
            mesh.vertices=vertices;
            bool decorative=name.Contains("Fallen_")||name.Contains("Rock_Decal")||name.Contains("Grass_Close")||name.Contains("Puddle");
            var kept=new List<int>();
            for(int j=0;j<triangles.Length;j+=3)
            {
                var p=f.transform.TransformPoint((vertices[triangles[j]]+vertices[triangles[j+1]]+vertices[triangles[j+2]])/3);bool remove=false;
                if(decorative)foreach(var r in rivers){var q=r.transform.InverseTransformPoint(p);if(Mathf.Abs(q.x)<27&&Mathf.Abs(q.z-r.ChannelOffset(q.x))<r.halfWidth-0.5f)remove=true;}
                if(!remove)kept.AddRange(new[]{triangles[j],triangles[j+1],triangles[j+2]});
            }
            mesh.SetTriangles(kept,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            if(f.TryGetComponent<MeshCollider>(out var collider)){collider.sharedMesh=null;collider.sharedMesh=mesh;}changed++;
        }
        foreach(var river in rivers)
        {
            var f=river.transform.Find("Gravel ford bed - physical").GetComponent<MeshFilter>();var mesh=f.sharedMesh;var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                float along=vertices[i].x,across=vertices[i].z-river.ChannelOffset(along);
                float slope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(river.halfWidth-1,river.halfWidth+3,Mathf.Abs(across)));
                vertices[i].y=-along*river.surfaceSlope-0.62f-Mathf.Clamp01(Mathf.Abs(along)/22)*0.38f+slope*0.82f+Mathf.PerlinNoise(along*1.7f+20,across*1.7f)*0.045f;
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);f.GetComponent<MeshCollider>().sharedMesh=null;f.GetComponent<MeshCollider>().sharedMesh=mesh;
        }
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return "Refined "+changed+" channel surfaces and two sloped gravel ford beds.";
    }
}
