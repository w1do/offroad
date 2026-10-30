using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class CreateLandscape
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new System.Exception("Stop Play mode before generating terrain.");
        string folder = "Assets/GeneratedLandscape";
        if (AssetDatabase.IsValidFolder(folder)) folder = AssetDatabase.GenerateUniqueAssetPath(folder);
        AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var data = new TerrainData { heightmapResolution = 513, size = new Vector3(1000, 230, 1000), alphamapResolution = 256 };
        var heights = new float[513,513];
        for (int z=0; z<513; z++) for (int x=0; x<513; x++)
        {
            float u=x/512f, v=z/512f;
            float broad=Mathf.PerlinNoise(u*3.6f+17.3f,v*3.6f+41.7f);
            float ridge=1f-Mathf.Abs(2f*Mathf.PerlinNoise(u*6f+73f,v*6f+12f)-1f);
            float detail=Mathf.PerlinNoise(u*20f+8f,v*20f+29f);
            float valley=Mathf.Exp(-Mathf.Pow((u-(0.48f+0.12f*Mathf.Sin(v*7f)))/0.14f,2f));
            heights[z,x]=Mathf.Clamp01(0.08f+broad*0.46f+ridge*ridge*0.24f+detail*0.035f-valley*0.21f);
        }
        data.SetHeights(0,0,heights);
        AssetDatabase.CreateAsset(data,folder+"/LandscapeTerrain.asset");
        Color[] colors={new Color(0.27f,0.37f,0.13f),new Color(0.40f,0.37f,0.29f),new Color(0.54f,0.55f,0.51f)};
        var layers=new TerrainLayer[3];
        for(int i=0;i<3;i++)
        {
            var tex=new Texture2D(128,128,TextureFormat.RGB24,false);
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++) for(int x=0;x<128;x++) pixels[y*128+x]=colors[i]*(0.82f+Mathf.PerlinNoise(x*0.17f+i*37,y*0.17f+9)*0.34f);
            tex.SetPixels(pixels);tex.Apply();
            string path=folder+"/Surface"+i+".png";
            System.IO.File.WriteAllBytes(path,tex.EncodeToPNG());
            Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);
            layers[i]=new TerrainLayer {diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path),tileSize=new Vector2(15,15), metallic=0,smoothness=0.05f};
            AssetDatabase.CreateAsset(layers[i],folder+"/Surface"+i+".terrainlayer");
        }
        data.terrainLayers=layers;
        var map=new float[256,256,3];
        for(int z=0;z<256;z++)for(int x=0;x<256;x++)
        {
            float u=x/255f,v=z/255f;
            float rock=Mathf.SmoothStep(0,1,Mathf.InverseLerp(19,42,data.GetSteepness(u,v)));
            float high=Mathf.SmoothStep(0,1,Mathf.InverseLerp(105,165,data.GetInterpolatedHeight(u,v)));
            map[z,x,0]=(1-rock)*(1-high);map[z,x,1]=rock;map[z,x,2]=(1-rock)*high;
        }
        data.SetAlphamaps(0,0,map);
        var go=Terrain.CreateTerrainGameObject(data);go.name="Landscape - Hills and Valley";
        Undo.RegisterCreatedObjectUndo(go,"Create landscape");
        var terrain=go.GetComponent<Terrain>();terrain.heightmapPixelError=5;terrain.basemapDistance=1500;
        var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if(shader!=null){var mat=new Material(shader);AssetDatabase.CreateAsset(mat,folder+"/Terrain.mat");terrain.materialTemplate=mat;}
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.6f;sun.color=new Color(1f,0.94f,0.83f);sun.transform.rotation=Quaternion.Euler(38,-32,0);sun.shadows=LightShadows.Soft;
        RenderSettings.sun=sun;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.52f,.66f,.8f);RenderSettings.ambientEquatorColor=new Color(.36f,.42f,.44f);RenderSettings.ambientGroundColor=new Color(.2f,.23f,.16f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.66f,.76f,.84f);RenderSettings.fogDensity=.00065f;
        var camera=new GameObject("Landscape Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(480,250,-100);camera.transform.LookAt(new Vector3(500,65,530));camera.farClipPlane=2200;camera.fieldOfView=58;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.60f,.74f,.85f);camera.gameObject.AddComponent<AudioListener>();
        Selection.activeGameObject=go;
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(new Vector3(500,70,500),Quaternion.Euler(40,0,0),700);
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        string scenePath=folder+"/Landscape.unity";EditorSceneManager.SaveScene(scene,scenePath);
        return "Created "+scenePath+"; terrain 1000 x 1000 m, heightmap 513, three surface layers, camera and sunlight.";
    }
}
