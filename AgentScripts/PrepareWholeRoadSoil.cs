using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PrepareWholeRoadSoil
{
    const string Folder="Assets/Game/Generated/WholeRoadSoil";
    static SoftGroundProfile profile;
    static int count,vertexCount;
    struct V {public Vector3 p,n;public Vector2 uv;public V Mix(V b)=>new V{p=(p+b.p)*.5f,n=(n+b.n).normalized,uv=(uv+b.uv)*.5f};}
    sealed class DenseMesh
    {
        public List<Vector3> p=new List<Vector3>(),n=new List<Vector3>();public List<Vector2> uv=new List<Vector2>();public List<int> t=new List<int>();
        Dictionary<(Vector3Int,Vector2Int),int> ids=new Dictionary<(Vector3Int,Vector2Int),int>();
        int Add(V v){var key=(Vector3Int.RoundToInt(v.p*100000),Vector2Int.RoundToInt(v.uv*100000));if(ids.TryGetValue(key,out int i))return i;i=p.Count;ids[key]=i;p.Add(v.p);n.Add(v.n);uv.Add(v.uv);return i;}
        public void Tri(V a,V b,V c,int depth=0)
        {
            float ab=(a.p-b.p).sqrMagnitude,bc=(b.p-c.p).sqrMagnitude,ca=(c.p-a.p).sqrMagnitude;
            if(Mathf.Max(ab,bc,ca)<=.28f*.28f||depth>=18){if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<1e-14f)return;t.Add(Add(a));t.Add(Add(b));t.Add(Add(c));return;}
            if(ab>=bc&&ab>=ca){var m=a.Mix(b);Tri(a,m,c,depth+1);Tri(m,b,c,depth+1);}
            else if(bc>=ca){var m=b.Mix(c);Tri(a,b,m,depth+1);Tri(a,m,c,depth+1);}
            else{var m=c.Mix(a);Tri(a,b,m,depth+1);Tri(m,b,c,depth+1);}
        }
        public Mesh Build(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(p);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();m.RecalculateTangents();return m;}
    }
    static Mesh Save(Mesh mesh,string path){var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;}
    static void Process(GameObject root,string token,bool prefab)
    {
        var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null).ToArray();
        int ordinal=0;
        foreach(var f in filters)
        {
            var renderer=f.GetComponent<MeshRenderer>();var collider=f.GetComponent<MeshCollider>();
            if(renderer==null||collider==null||!renderer.enabled||!collider.enabled||!f.gameObject.activeInHierarchy||f.GetComponent<SoftGroundRuts>()!=null)continue;
            if(!prefab&&f.GetComponentInParent<AuthoredForestRoute>()!=null)continue;
            var mats=renderer.sharedMaterials;
            if(mats.Length!=1||mats[0]==null||!mats[0].name.Contains("Dirt_Road"))continue;
            if(f.sharedMesh.subMeshCount!=1)throw new Exception("Inspect road submeshes before converting: "+f.name);
            var group=root.transform.Find("Continuous road soil");
            if(group==null){var g=new GameObject("Continuous road soil");g.transform.SetParent(root.transform,false);group=g.transform;}
            string id=token+" "+ordinal++;
            var go=new GameObject("Deformable dry road "+f.name);go.transform.SetParent(group,false);
            // Bake imported FBX rotation and scale, so the road uses horizontal local X/Z.
            go.transform.rotation=Quaternion.identity;
            var source=f.sharedMesh;var p=source.vertices;var n=source.normals;var uv=source.uv;var t=source.triangles;
            Func<int,V> vertex=i=>new V{p=go.transform.InverseTransformPoint(f.transform.TransformPoint(p[i])),n=go.transform.InverseTransformDirection(f.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(n.Length==p.Length?n[i]:Vector3.up)).normalized,uv=uv.Length==p.Length?uv[i]:Vector2.zero};
            var dense=new DenseMesh();for(int i=0;i<t.Length;i+=3)dense.Tri(vertex(t[i]),vertex(t[i+1]),vertex(t[i+2]));
            var tiles=new Dictionary<Vector2Int,DenseMesh>();
            for(int i=0;i<dense.t.Count;i+=3)
            {
                int a=dense.t[i],b=dense.t[i+1],c=dense.t[i+2];var mid=(dense.p[a]+dense.p[b]+dense.p[c])/3;
                var key=new Vector2Int(Mathf.FloorToInt(mid.x/16),Mathf.FloorToInt(mid.z/16));
                if(!tiles.TryGetValue(key,out var tile))tiles[key]=tile=new DenseMesh();
                tile.Tri(new V{p=dense.p[a],n=dense.n[a],uv=dense.uv[a]},new V{p=dense.p[b],n=dense.n[b],uv=dense.uv[b]},new V{p=dense.p[c],n=dense.n[c],uv=dense.uv[c]});
            }
            foreach(var pair in tiles)
            {
            string tileId=id+" tile "+pair.Key.x+" "+pair.Key.y;
            var tileGo=new GameObject("Road soil tile "+pair.Key);tileGo.transform.SetParent(go.transform,false);
            var mesh=Save(pair.Value.Build("Original road with wheel-scale tessellation "+tileId),Folder+"/Road "+tileId+".asset");
            tileGo.AddComponent<MeshFilter>().sharedMesh=mesh;var mr=tileGo.AddComponent<MeshRenderer>();mr.sharedMaterials=mats;mr.shadowCastingMode=renderer.shadowCastingMode;mr.receiveShadows=renderer.receiveShadows;
            var physical=UnityEngine.Object.Instantiate(mesh);var both=new List<int>(mesh.triangles);// Back faces use the generated triangles.
            var front=mesh.triangles;for(int i=0;i<front.Length;i+=3)both.AddRange(new[]{front[i],front[i+2],front[i+1]});physical.SetTriangles(both,0);
            tileGo.AddComponent<MeshCollider>().sharedMesh=Save(physical,Folder+"/Collision "+tileId+".asset");
            var surface=tileGo.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Gravel;surface.forwardGrip=.65f;surface.lateralGrip=.7f;surface.rollingResistance=.025f;
            var ruts=tileGo.AddComponent<SoftGroundRuts>();ruts.profile=profile;ruts.fieldHalfSize=Vector2.zero;ruts.maximumDepth=profile.maximumDepthMetres;
            ruts.persistentId=prefab?"":("whole-road/"+tileId);
            var tracks=tileGo.AddComponent<TireTrackRenderer>();tracks.resolution=256;tracks.persistentId=ruts.persistentId;
            vertexCount+=mesh.vertexCount;
            }
            renderer.enabled=false;collider.enabled=false;
            EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(collider);
            if(!prefab){PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
            count++;
        }
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        foreach(var id in new[]{"dry_mud_field_001","brown_mud_leaves_01","brown_mud_03"})
        {
            var normal=(TextureImporter)AssetImporter.GetAtPath("Assets/Game/Art/Puddles/"+id+"_nor_gl_2k.jpg");normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();
            var rough=(TextureImporter)AssetImporter.GetAtPath("Assets/Game/Art/Puddles/"+id+"_rough_2k.jpg");rough.sRGBTexture=false;rough.SaveAndReimport();
        }
        profile=AssetDatabase.LoadAssetAtPath<SoftGroundProfile>(Folder+"/Dry road deformation.asset");
        if(profile==null){profile=ScriptableObject.CreateInstance<SoftGroundProfile>();AssetDatabase.CreateAsset(profile,Folder+"/Dry road deformation.asset");}
        var soil=AssetDatabase.LoadAssetAtPath<SoilProfile>(Folder+"/Dry road soil.asset");
        if(soil==null){soil=ScriptableObject.CreateInstance<SoilProfile>();AssetDatabase.CreateAsset(soil,Folder+"/Dry road soil.asset");}
        soil.soilType=SoilType.DryDirt;soil.moisture=.25f;soil.waterContent=.15f;soil.compaction=.2f;soil.bearingStrengthPascal=23000;soil.deformationResistancePascal=850000;
        soil.rutDepthLimitMetres=.075f;soil.longitudinalGrip=.65f;soil.lateralGrip=.7f;soil.rollingResistance=.025f;
        profile.soil=soil;profile.maximumDepthMetres=.075f;profile.settlementDepthMetres=.025f;profile.diggingMetresPerSecond=.015f;profile.rollingCompactionMetresPerSecond=.03f;
        profile.maximumBermHeightMetres=.018f;profile.collisionUpdateSeconds=.16f;
        var scene=EditorSceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects())Process(root,"main "+root.name,false);
        var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();
        foreach(var prefab in route.arms.SelectMany(a=>a.modules).Distinct())
        {
            string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try{Process(root,root.name,true);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        EditorUtility.SetDirty(soil);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Converted road render/collision layers="+count+"; dense vertices="+vertexCount+"; originals retained disabled; dry rut cap=.075 m";
    }
}



