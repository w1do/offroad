using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class RepairForestTruck
{
    const string Root="Assets/Game/Generated/TruckRepair";
    public static string Main()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();
        if(car==null) throw new Exception("No KAMAZ.");
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        string backup="Assets/Game/Scenes/BeforeTruckPhysicsRepair.unity";
        if(!File.Exists(backup)) EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),backup,true);
        var rig=car.transform;
        var model=rig.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="SM_ClassicTruck1969_Tires").transform.parent;
        // Read original geometry, never the previously cut wheel fragments.
        var sourceFilters=model.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="SM_ClassicTruck1969_Tires" || f.name=="SM_ClassicTruck1969_Wheels").ToArray();
        foreach(var visual in car.visuals) if(visual!=null) UnityEngine.Object.DestroyImmediate(visual.gameObject);
        car.visuals=new Transform[6];
        float[] axles={1.56f,-1.14f,-2.31f};
        for(int i=0;i<6;i++)
        {
            var center=new Vector3(i%2==0?-1.09f:1.09f,0.51f,axles[i/2]);
            var pivot=new GameObject("Wheel visual centered "+i).transform;pivot.SetParent(rig,false);pivot.localPosition=center;car.visuals[i]=pivot;
            var wheel=car.wheels[i];wheel.transform.localPosition=center;wheel.transform.localRotation=Quaternion.identity;wheel.radius=0.51f;
            wheel.suspensionDistance=0.24f;wheel.mass=100;
            var spring=wheel.suspensionSpring;spring.spring=95000;spring.damper=13000;spring.targetPosition=0.5f;wheel.suspensionSpring=spring;
            EditorUtility.SetDirty(wheel);
        }
        int spareGroups=0;
        foreach(var source in sourceFilters)
        {
            var mesh=source.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            var parents=Enumerable.Range(0,vertices.Length).ToArray();var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {var key=Vector3Int.RoundToInt(vertices[i]*10000);if(weld.TryGetValue(key,out int j))Union(parents,i,j);else weld[key]=i;}
            for(int i=0;i<triangles.Length;i+=3){Union(parents,triangles[i],triangles[i+1]);Union(parents,triangles[i],triangles[i+2]);}
            var mapping=new Dictionary<int,int>();
            foreach(var group in Enumerable.Range(0,vertices.Length).GroupBy(i=>Find(parents,i)))
            {
                var first=rig.InverseTransformPoint(source.transform.TransformPoint(vertices[group.First()]));var bounds=new Bounds(first,Vector3.zero);
                foreach(int i in group) bounds.Encapsulate(rig.InverseTransformPoint(source.transform.TransformPoint(vertices[i])));
                // The spare lies horizontally under the frame; keep all of its connected geometry static.
                bool spare=bounds.center.z>-0.3f && bounds.center.z<1f && Mathf.Abs(bounds.center.x)<0.95f;
                int axle=Enumerable.Range(0,3).OrderBy(a=>Mathf.Abs(axles[a]-bounds.center.z)).First();
                // Only the full-size outer rim belongs to wheel spin. Hub fittings,
                // half-shaft housings, pins and suspension hardware stay on the chassis.
                bool outerRim=Mathf.Abs(bounds.center.x)>0.95f && bounds.size.y>0.60f && bounds.size.z>0.60f;
                bool axleDetail=source.name.EndsWith("_Wheels") && !outerRim;
                mapping[group.Key]=spare?6:axleDetail?7:axle*2+(bounds.center.x>0?1:0);
                if(spare)spareGroups++;
            }
            var bins=new List<int>[8];for(int i=0;i<8;i++) bins[i]=new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                int bin=mapping[Find(parents,triangles[i])];
                if(source.name.EndsWith("_Wheels"))
                {
                    var pts=new[]{triangles[i],triangles[i+1],triangles[i+2]}.Select(j=>rig.InverseTransformPoint(source.transform.TransformPoint(vertices[j]))).ToArray();
                    var center=(pts[0]+pts[1]+pts[2])/3;
                    int axle=Enumerable.Range(0,3).OrderBy(a=>Mathf.Abs(axles[a]-center.z)).First();
                    // Never spin any metal on the inboard side: steering links and
                    // brake/axle housings are interleaved with rims in this source.
                    bool exterior=pts.All(p=>Mathf.Abs(p.x)>=1.10f);
                    bin=exterior?axle*2+(center.x>0?1:0):7;
                }
                bins[bin].AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
            }
            for(int i=0;i<8;i++)
            {
                if(bins[i].Count==0)continue;
                Transform parent=i>=6?rig:car.visuals[i];
                var vs=new List<Vector3>();var ns=new List<Vector3>();var uvs=new List<Vector2>();var ids=new List<int>();
                var normals=mesh.normals;var uv=mesh.uv;
                foreach(int index in bins[i])
                {vs.Add(parent.InverseTransformPoint(source.transform.TransformPoint(vertices[index])));ns.Add(parent.InverseTransformDirection(source.transform.TransformDirection(normals[index])));uvs.Add(uv[index]);ids.Add(ids.Count);}
                var result=new Mesh();result.name=source.name+"_Fixed_"+i;result.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                result.SetVertices(vs);result.SetNormals(ns);result.SetUVs(0,uvs);result.SetTriangles(ids,0);result.RecalculateBounds();
                string path=Root+"/"+result.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null){EditorUtility.CopySerialized(result,existing);UnityEngine.Object.DestroyImmediate(result);result=existing;}else AssetDatabase.CreateAsset(result,path);
                string name=i==6?"Spare fixed "+source.name:i==7?"Fixed axle details":source.name;
                var previous=parent.Find(name);if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=result;
                go.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponent<MeshRenderer>().sharedMaterial;
            }
            source.GetComponent<MeshRenderer>().enabled=false;
        }
        int added=0;int samples=0;int gaps=0;
        var walkable=new HashSet<string>{"Forest_Dirt_Road","Forest_Dirt_Road_Bare","Forest_Dirt_Road_Trails","Forest_Road_Edge_Gravel_Dusty","Forest_Grass_Close","Forest_Ground_Dirt","Forest_Aerial_Grass","Forest_Terrain_Far","Forest_Cobblestone","Forest_Sloped_Rock","Forest_Tall_Cliff","Forest_Mud_Pile"};
        foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var renderer=filter.GetComponent<Renderer>();if(renderer==null || !walkable.Contains(renderer.sharedMaterial.name)) continue;
            var collider=filter.GetComponent<MeshCollider>();if(collider==null){collider=filter.gameObject.AddComponent<MeshCollider>();added++;}
            // Use precisely the visible surface, including the formerly non-physical road-edge / grass overlay.
            collider.sharedMesh=filter.sharedMesh;collider.enabled=true;collider.isTrigger=false;
        }
        Physics.SyncTransforms();
        foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var renderer=filter.GetComponent<Renderer>();if(renderer==null || !walkable.Contains(renderer.sharedMaterial.name))continue;
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var tris=mesh.triangles;
            var collider=filter.GetComponent<MeshCollider>();
            for(int i=0;i<tris.Length;i+=Math.Max(3,((tris.Length/300)/3)*3))
            {
                var a=filter.transform.TransformPoint(vertices[tris[i]]);var b=filter.transform.TransformPoint(vertices[tris[i+1]]);var c=filter.transform.TransformPoint(vertices[tris[i+2]]);
                if(Vector3.Cross(b-a,c-a).normalized.y<0.2f)continue;
                Vector3 p=(a+b+c)/3;samples++;
                if(!collider.Raycast(new Ray(p+Vector3.up*0.3f,Vector3.down),out var hit,0.6f))gaps++;
            }
        }
        car.motorTorqueNm=2400;car.brakeTorqueNm=8500;car.testControl=false;car.testThrottle=0;car.testBrake=false;
        var body=car.GetComponent<Rigidbody>();body.mass=11000;body.solverIterations=12;body.solverVelocityIterations=6;body.maxAngularVelocity=5;
        body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        EditorUtility.SetDirty(car);EditorUtility.SetDirty(body);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new Exception("Save failed.");
        return "Rebuilt wheel groups; static spare groups="+spareGroups+" added visible-surface colliders="+added+" sampled surfaces="+samples+" gaps="+gaps;
    }
    static int Find(int[] p,int a){while(p[a]!=a){p[a]=p[p[a]];a=p[a];}return a;}
    static void Union(int[] p,int a,int b){p[Find(p,a)]=Find(p,b);}
}
