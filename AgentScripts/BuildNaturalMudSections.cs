using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class BuildNaturalMudSections
{
    const string Folder="Assets/Game/Generated/NaturalMud";
    const string Art="Assets/Game/Art/RoadScans/";
    static readonly string[] Scans={"muddy-road-with-tire-tracks","tire-tracks-in-mud","gravel-road-2","traktore-markak","forest-mud-puddle"};
    static string ModelPath(string scan)=>scan=="forest-mud-puddle"?"Assets/Game/Art/RoadMud/forest-puddle.fbx":Art+scan+"/"+(scan.StartsWith("muddy")?"textured.obj":scan=="tire-tracks-in-mud"?"matsch.obj":scan=="gravel-road-2"?"schotter2.obj":"model.fbx");
    static string ColourPath(string scan)=>scan=="forest-mud-puddle"?"Assets/Game/Art/RoadMud/forest-puddle.jpg":Art+scan+"/"+(scan.StartsWith("muddy")?"textured_0_7PO5vCBU.jpg":scan=="tire-tracks-in-mud"?"textured_0_sj1VXpy9.jpg":scan=="gravel-road-2"?"schotter2_diffuse.jpg":"model.jpg");
    struct Sample { public Vector3 p;public Vector2 uv,scanUV;public Material material;public float scanHeight; }
    static MudRoadProfile roadProfile;
    static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
    static readonly Dictionary<Material,Material> originals=new Dictionary<Material,Material>();
    static Texture2D Bake(IEnumerable<(Mesh mesh,Material[] materials)> items,string token)
    {
        var objects=new List<GameObject>();var owned=new List<Material>();var cameraRoot=new GameObject("TEMP scan bake camera");
        var camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1;
        camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.19f,.13f);
        camera.transform.SetPositionAndRotation(new Vector3(0,-9990,0),Quaternion.LookRotation(Vector3.down,Vector3.forward));camera.nearClipPlane=.1f;camera.farClipPlane=30;
        var rt=RenderTexture.GetTemporary(1024,1024,24);var active=RenderTexture.active;Texture2D texture=null;
        try
        {
            foreach(var item in items)
            {
                var go=new GameObject("TEMP texture bake mesh");go.layer=31;go.transform.position=new Vector3(0,-10000,0);objects.Add(go);go.AddComponent<MeshFilter>().sharedMesh=item.mesh;
                var mats=item.materials.Select(source=>{var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));mat.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white);mat.SetFloat("_Cull",0);owned.Add(mat);return mat;}).ToArray();
                go.AddComponent<MeshRenderer>().sharedMaterials=mats;
            }
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture=new Texture2D(1024,1024,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1024,1024),0,0);texture.Apply();
            string path=Folder+"/Baked "+token+".png";File.WriteAllBytes(path,texture.EncodeToPNG());AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=1024;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        finally{RenderTexture.active=active;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(cameraRoot);foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);foreach(var mat in owned)UnityEngine.Object.DestroyImmediate(mat);}
    }
    static Mesh Save(Mesh mesh,string name)
    {
        string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;
    }
    static Material Original(Material wet)
    {
        if(originals.TryGetValue(wet,out var cached))return cached;
        string name=wet.name.StartsWith("Wet ")?wet.name.Substring(4):wet.name;
        var result=AssetDatabase.FindAssets(name+" t:Material").Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(m=>m.name==name&&m.shader.name=="Game/Living Forest")??wet;
        originals[wet]=result;return result;
    }
    static Material BlendMaterial(Material source,string scan,Vector2 half)
    {
        string key=source.name+" "+scan+" "+half.x+" "+half.y;if(materials.TryGetValue(key,out var result))return result;
        string path=Folder+"/"+key.Replace('/','_')+".mat";result=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(result==null){result=new Material(source);AssetDatabase.CreateAsset(result,path);}
        result.shader=Shader.Find("Game/Living Forest");result.CopyPropertiesFromMaterial(source);
        result.SetFloat("_MudBlend",1);result.SetVector("_MudHalfSize",new Vector4(half.x,half.y,roadProfile.edgeBlendMetres,0));result.SetColor("_MudTint",new Color(.86f,.79f,.68f));
        string dir=Art+scan+"/";string suffix=scan.StartsWith("muddy")?"textured_0_7PO5vCBU.jpg":"textured_0_sj1VXpy9.jpg";
        string normal=scan.StartsWith("muddy")?"textured_0_norm_7PO5vCBU.jpg":"textured_0_norm_sj1VXpy9.jpg";
        result.SetTexture("_MudMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+suffix));result.SetTexture("_MudNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+normal));
        EditorUtility.SetDirty(result);materials[key]=result;return result;
    }
    static Mesh ScanMesh(string scan)
    {
        string path=ModelPath(scan);
        var original=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().First();var mesh=UnityEngine.Object.Instantiate(original);
        var vertices=mesh.vertices;bool upright=scan.StartsWith("muddy");
        for(int i=0;i<vertices.Length;i++){var v=vertices[i];vertices[i]=upright?v:scan=="gravel-road-2"?new Vector3(v.x,v.z,v.y):scan=="forest-mud-puddle"?new Vector3(v.z,v.y,v.x):new Vector3(v.y,v.z,v.x);}
        mesh.vertices=vertices;mesh.RecalculateBounds();var bounds=mesh.bounds;
        for(int i=0;i<vertices.Length;i++){var v=vertices[i]-bounds.center;vertices[i]=new Vector3(v.x/Mathf.Max(.01f,bounds.extents.x),v.y/Mathf.Max(.01f,bounds.size.y),v.z/Mathf.Max(.01f,bounds.extents.z));}
        mesh.vertices=vertices;for(int sub=0;sub<mesh.subMeshCount;sub++){var t=mesh.GetTriangles(sub);var both=new List<int>(t);for(int i=0;i<t.Length;i+=3)both.AddRange(new[]{t[i],t[i+2],t[i+1]});mesh.SetTriangles(both,sub);}mesh.RecalculateBounds();return mesh;
    }
    static int Section(Transform root,int number,string token)
    {
        if(root.Find("Natural scanned mud")!=null){UnityEngine.Object.DestroyImmediate(root.Find("Natural scanned mud").gameObject);foreach(Transform old in root)old.gameObject.SetActive(true);}
        var soils=root.GetComponentsInChildren<SoftGroundRuts>().Where(s=>s.enabled&&s.GetComponent<MeshCollider>().enabled).ToArray();
        var baseSoils=soils.Where(s=>!s.name.Contains("forest-puddle")).ToArray();if(baseSoils.Length==0)return 0;
        Vector2 half=soils[0].fieldHalfSize;if(half.x<=0)return 0;
        string scan=Scans[number%Scans.Length];bool gravel=scan=="gravel-road-2";
        var sampling=new GameObject("TEMP scanned mesh sample");sampling.transform.position=new Vector3(0,-10000,0);var scanMesh=ScanMesh(scan);var scanCollider=sampling.AddComponent<MeshCollider>();scanCollider.sharedMesh=scanMesh;
        var go=new GameObject("Natural scanned mud");go.transform.SetParent(root,false);
        var section=go.AddComponent<MudRoadSection>();section.profile=roadProfile;section.halfSize=half;
        section.sourceArchive="Models/Road/"+scan+".zip";
        section.laneCentreMetres=token=="main0"?-.7f:0;section.mudHalfWidthMetres=token=="main0"?1.8f:Mathf.Min(2.15f,half.x);
        // RequireComponent supplies the existing shared deformation component.
        var ruts=go.GetComponent<SoftGroundRuts>();ruts.profile=soils[0].profile;ruts.fieldHalfSize=half;ruts.maximumDepth=ruts.profile.maximumDepthMetres;
        ruts.persistentId=token.StartsWith("main")?token+"/natural":"";
        if(gravel){string gp=Folder+"/Firm gravel soil.asset";var firm=AssetDatabase.LoadAssetAtPath<SoftGroundProfile>(gp);if(firm==null){firm=UnityEngine.Object.Instantiate(ruts.profile);firm.maximumDepthMetres=.018f;firm.settlementDepthMetres=.006f;firm.diggingMetresPerSecond=.001f;firm.rollingCompactionMetresPerSecond=.001f;AssetDatabase.CreateAsset(firm,gp);}ruts.profile=firm;ruts.maximumDepth=.018f;}
        const float spacing=.13f;int nx=Mathf.CeilToInt(half.x*2/spacing),nz=Mathf.CeilToInt(half.y*2/spacing);
        var samples=new Sample[(nx+1)*(nz+1)];var verts=new Vector3[samples.Length];var uv=new Vector2[samples.Length];var scanUV=new Vector2[samples.Length];var local=new Vector2[samples.Length];
        var sourceMats=new List<Material>();int missing=0;
        try
        {
            string scanPath=ColourPath(scan);
            var colourMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));colourMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(scanPath));
            Texture2D bakedScan;
            var scanMaterials=Enumerable.Repeat(colourMat,scanMesh.subMeshCount).ToArray();
            var extra=new List<Material>();if(scan=="forest-mud-puddle"){int[] order={2,0,1};for(int k=0;k<scanMaterials.Length;k++){var mat=new Material(colourMat);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/RoadMud/forest-puddle"+(order[k]==0?"":order[k].ToString())+".jpg"));scanMaterials[k]=mat;extra.Add(mat);}}
            try{bakedScan=Bake(new[]{(scanMesh,scanMaterials)},scan);}finally{UnityEngine.Object.DestroyImmediate(colourMat);foreach(var mat in extra)UnityEngine.Object.DestroyImmediate(mat);}
            var baselineMeshes=new List<Mesh>();var baselineItems=new List<(Mesh mesh,Material[] materials)>();
            foreach(var soil in baseSoils){var source=soil.GetComponent<MeshFilter>().sharedMesh;var clone=UnityEngine.Object.Instantiate(source);var v=clone.vertices;for(int i=0;i<v.Length;i++){var p=root.InverseTransformPoint(soil.transform.TransformPoint(v[i]));v[i]=new Vector3(p.x/half.x,p.y,p.z/half.y);}clone.vertices=v;clone.RecalculateBounds();baselineMeshes.Add(clone);baselineItems.Add((clone,soil.GetComponent<MeshRenderer>().sharedMaterials.Select(Original).ToArray()));}
            Texture2D bakedBase;try{bakedBase=Bake(baselineItems,"base "+token);}finally{foreach(var temporaryMesh in baselineMeshes)UnityEngine.Object.DestroyImmediate(temporaryMesh);}
            Physics.SyncTransforms();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int i=z*(nx+1)+x;Vector3 p=new Vector3(Mathf.Lerp(-half.x,half.x,(float)x/nx),0,Mathf.Lerp(-half.y,half.y,(float)z/nz));
                var query=new Vector3(Mathf.Clamp(p.x,-half.x+.004f,half.x-.004f),0,Mathf.Clamp(p.z,-half.y+.004f,half.y-.004f));
                var start=root.TransformPoint(query)+Vector3.up*12;RaycastHit best=default;SoftGroundRuts owner=null;float top=float.MinValue;
                foreach(var soil in baseSoils)if(soil.GetComponent<MeshCollider>().Raycast(new Ray(start,Vector3.down),out var hit,30)&&hit.point.y>top){best=hit;top=hit.point.y;owner=soil;}
                if(owner==null){missing++;continue;}
                var mat=Original(owner.GetComponent<MeshRenderer>().sharedMaterials[0]);if(!sourceMats.Contains(mat))sourceMats.Add(mat);
                Vector3 scanStart=sampling.transform.position+new Vector3(p.x/half.x*.98f,3,p.z/half.y*.98f);
                Vector2 tex=new Vector2((float)x/nx,(float)z/nz);float height=0;
                if(scanCollider.Raycast(new Ray(scanStart,Vector3.down),out var sh,6)){tex=sh.textureCoord;height=sh.point.y-sampling.transform.position.y;}
                p.y=root.InverseTransformPoint(best.point).y;
                float weight=MudRoadSection.EdgeWeight(new Vector2(p.x,p.z),half,roadProfile.edgeBlendMetres);
                p.y+=height*(gravel?.035f:roadProfile.scanReliefMetres)*section.WeightAt(root.TransformPoint(p))-(gravel?0:section.RutAt(p));
                samples[i]=new Sample{p=p,uv=best.textureCoord,scanUV=tex,material=mat,scanHeight=height};verts[i]=p;uv[i]=new Vector2((float)x/nx,(float)z/nz);scanUV[i]=new Vector2((p.x-section.laneCentreMetres)/section.mudHalfWidthMetres*.5f+.5f,(float)z/nz);local[i]=new Vector2(p.x-section.laneCentreMetres,p.z);
            }
            if(missing>0)throw new Exception(token+": missing baseline samples="+missing+"; no artificial bridges created");
            var triangles=new[]{new List<int>()};
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;int sub=0;
                triangles[sub].AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh{name="Blended scanned mud "+token,indexFormat=IndexFormat.UInt32};mesh.vertices=verts;mesh.uv=uv;mesh.uv2=scanUV;mesh.SetUVs(3,local.ToList());mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();mesh=Save(mesh,"Road "+token);
            var baseline=sourceMats.FirstOrDefault(m=>m.name=="Forest Dirt_Road")??sourceMats[0];var blended=BlendMaterial(baseline,scan,half);
            var final=new Material(blended);final.SetTexture("_BaseMap",bakedBase);final.SetTexture("_MudMap",bakedScan);final.SetTexture("_MudNormal",Texture2D.normalTexture);final.SetFloat("_UseWorldTextures",0);final.SetFloat("_BumpScale",0);final.SetTextureScale("_BaseMap",Vector2.one);final.SetTextureOffset("_BaseMap",Vector2.zero);
            final.SetColor("_MudTint",new Color(.55f,.43f,.30f));final.SetVector("_MudHalfSize",new Vector4(section.mudHalfWidthMetres,half.y,roadProfile.edgeBlendMetres,0));
            if(gravel)final.SetColor("_MudTint",new Color(.95f,.92f,.86f));
            string finalPath=Folder+"/Surface "+token+".mat";var finalAsset=AssetDatabase.LoadAssetAtPath<Material>(finalPath);if(finalAsset==null){AssetDatabase.CreateAsset(final,finalPath);finalAsset=final;}else{EditorUtility.CopySerialized(final,finalAsset);EditorUtility.SetDirty(finalAsset);UnityEngine.Object.DestroyImmediate(final);}
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=finalAsset;
            var collision=UnityEngine.Object.Instantiate(mesh);collision.name="Matching scanned mud collision";var front=mesh.triangles;var both=new List<int>(front);for(int i=0;i<front.Length;i+=3)both.AddRange(new[]{front[i],front[i+2],front[i+1]});collision.subMeshCount=1;collision.SetTriangles(both,0);go.GetComponent<MeshCollider>().sharedMesh=Save(collision,"Collision "+token);
            var surface=go.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Mud;surface.forwardGrip=.4f;surface.lateralGrip=.42f;surface.rollingResistance=.045f;surface.chainsImproveGrip=true;surface.chainedForwardGrip=.64f;surface.chainedLateralGrip=.65f;
            if(gravel){surface.cover=ForestDrivingSurface.Cover.Gravel;surface.forwardGrip=.78f;surface.lateralGrip=.76f;surface.rollingResistance=.025f;surface.chainsImproveGrip=false;}
            // Old layers are retained disabled for review; no support remains below new ruts.
            foreach(var child in root.Cast<Transform>().ToArray())if(child!=go.transform)child.gameObject.SetActive(false);
            if(!gravel)Puddles(go,mesh,half,token);
            return 1;
        }
        catch{UnityEngine.Object.DestroyImmediate(go);throw;}
        finally{UnityEngine.Object.DestroyImmediate(sampling);UnityEngine.Object.DestroyImmediate(scanMesh);}
    }
    static void Puddles(GameObject go,Mesh road,Vector2 half,string token)
    {
        var vertices=road.vertices;var waterVertices=new List<Vector3>();var waterTriangles=new List<int>();
        foreach(float side in new[]{-.8f,.8f})
        {
            float min=float.MaxValue;foreach(var p in vertices)if(Mathf.Abs(p.x-side)<.35f&&Mathf.Abs(p.z)<half.y*.42f)min=Mathf.Min(min,p.y);
            float level=min+roadProfile.puddleDepthMetres;
            var triangles=road.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var polygon=new List<Vector3>{vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]};
                if(polygon.Any(p=>Mathf.Abs(p.x-side)>.48f||Mathf.Abs(p.z)>half.y*.55f))continue;
                var clipped=new List<Vector3>();var previous=polygon[polygon.Count-1];
                foreach(var p in polygon){if((p.y<level)!=(previous.y<level))clipped.Add(Vector3.Lerp(previous,p,(level-previous.y)/(p.y-previous.y)));if(p.y<level)clipped.Add(p);previous=p;}
                for(int j=1;j<clipped.Count-1;j++){int start=waterVertices.Count;foreach(var p in new[]{clipped[0],clipped[j],clipped[j+1]})waterVertices.Add(new Vector3(p.x,level+.002f,p.z));waterTriangles.AddRange(new[]{start,start+1,start+2});}
            }
        }
        if(waterVertices.Count==0)return;var mesh=new Mesh{name="Water in scanned ruts"};mesh.SetVertices(waterVertices);mesh.SetTriangles(waterTriangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var puddles=new GameObject("Water in low ruts");puddles.transform.SetParent(go.transform,false);puddles.AddComponent<MeshFilter>().sharedMesh=Save(mesh,"Water "+token);
        var renderer=puddles.AddComponent<MeshRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Generated/DeformableMud/Muddy water.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit Mode");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();materials.Clear();originals.Clear();
        var scene=EditorSceneManager.GetActiveScene();string backup="Assets/_Recovery/BeforeNaturalMud.unity";
        if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        string path=Folder+"/Forest mud lanes.asset";roadProfile=AssetDatabase.LoadAssetAtPath<MudRoadProfile>(path);
        if(roadProfile==null){roadProfile=ScriptableObject.CreateInstance<MudRoadProfile>();AssetDatabase.CreateAsset(roadProfile,path);}
        int count=0;
        var roots=UnityEngine.Object.FindObjectsByType<SoftGroundRuts>().Where(s=>s.enabled).Select(s=>s.transform.parent).Distinct().Where(t=>t.name.StartsWith("Deformable mud main")).ToArray();
        for(int i=0;i<roots.Length;i++){string id=roots[i].name.Replace("Deformable mud ","");count+=Section(roots[i],id=="main0"?0:1,id);}
        var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();int module=0;
        foreach(var prefab in route.arms.SelectMany(a=>a.modules).Distinct())
        {
            string prefabPath=AssetDatabase.GetAssetPath(prefab);string copy="work/BeforeNaturalMud-"+Path.GetFileName(prefabPath);if(!File.Exists(copy))File.Copy(prefabPath,copy);
            var contents=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var groups=contents.GetComponentsInChildren<SoftGroundRuts>().Where(s=>s.enabled).Select(s=>s.transform.parent).Distinct().ToArray();
                foreach(var group in groups)count+=Section(group,module++%5,"module "+prefab.name);
                PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        EditorUtility.SetDirty(roadProfile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Saved "+count+" cohesive scanned mud sections, feathered original road UV at edges, matching collision, asymmetric ruts, local water, soil lane profile. Backup "+backup;
    }
}
