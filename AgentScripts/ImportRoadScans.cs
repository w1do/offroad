using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ImportRoadScans
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit Mode");
        string root="Assets/Game/Art/RoadScans";Directory.CreateDirectory(root);
        string[] names={"muddy-road-with-tire-tracks","tire-tracks-in-mud","gravel-road-2","traktore-markak"};
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(string name in names)
            {
                string folder=root+"/"+name;Directory.CreateDirectory(folder);
                using(var input=File.OpenRead("Models/Road/"+name+".zip"))
                using(var zip=new ZipArchive(input,ZipArchiveMode.Read))
                {
                    var source=zip.Entries.First(e=>e.FullName.StartsWith("source/")&&e.FullName.EndsWith(".zip"));
                    using(var buffer=new MemoryStream())
                    {
                        using(var stream=source.Open())stream.CopyTo(buffer);buffer.Position=0;
                        using(var nested=new ZipArchive(buffer,ZipArchiveMode.Read))foreach(var entry in nested.Entries)
                        {
                            if(string.IsNullOrEmpty(entry.Name))continue;
                            string extension=Path.GetExtension(entry.Name).ToLowerInvariant();
                            if(!new[]{".obj",".mtl",".fbx",".jpg",".png",".jpeg",".txt"}.Contains(extension))continue;
                            string path=folder+"/"+entry.Name;if(!File.Exists(path))using(var output=File.Create(path))using(var stream=entry.Open())stream.CopyTo(output);
                        }
                    }
                }
                File.WriteAllText(folder+"/SOURCE.txt","User supplied Models/Road/"+name+".zip; nested source archive extracted unchanged. Original archive retained.\n"+(name=="traktore-markak"?"Author/license not established by archive; do not redistribute without checking provenance.\n":"See archive credit text and source listing. SPLEEN VISION scanned road asset; attribution required.\n"));
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.Refresh();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{root}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Compressed;
                importer.textureType=path.Contains("_norm")||path.Contains("_normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=!(path.Contains("roughness")||path.Contains("occl")||path.Contains("_AO")||path.Contains("specular"));
                importer.SaveAndReimport();
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        var clod=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/VFX/Mud/Wet earth clods.mat");
        string mud=root+"/tire-tracks-in-mud/";
        clod.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(mud+"textured_0_sj1VXpy9.jpg"));
        clod.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(mud+"textured_0_norm_sj1VXpy9.jpg"));
        clod.EnableKeyword("_NORMALMAP");clod.SetFloat("_BumpScale",.8f);clod.SetColor("_BaseColor",new Color(.75f,.65f,.52f));
        EditorUtility.SetDirty(clod);AssetDatabase.SaveAssets();
        return "Imported 4 user-supplied road scans with original models/maps and 2048px texture import limit. Shared clods now use tire-tracks-in-mud photo texture + normal. No replacement of authored forest geometry.";
    }
}
