using System;using System.Linq;using UnityEngine;using UnityEditor;
public static class ValidateExplorationMechanics {
 public static object Main(){if(!Application.isPlaying)throw new Exception("Requires Play Mode");var world=UnityEngine.Object.FindAnyObjectByType<ExplorationWorld>();var p=world.palette;
 int missing=new[]{p.forest,p.road,p.mud,p.shore,p.water,p.lakeWater}.Count(m=>m==null);int errors=0;foreach(var name in new[]{"Game/Living Forest","Game/Exploration Lake Water","Game/Exploration Distant Ground","Game/Exploration Distant Trees"})errors+=ShaderUtil.GetShaderMessages(Shader.Find(name)).Count(m=>m.severity.ToString()=="Error");
 var calm=ExplorationClimate.Sample(Vector2.zero,0,0);var storm=ExplorationClimate.Sample(Vector2.zero,1,0);var wind=ExplorationClimate.Sample(Vector2.zero,2,0);var mud=ExplorationClimate.Sample(Vector2.zero,3,0);
 var trees=world.GetComponentsInChildren<ForestTreeWind>();int badTrees=trees.Count(t=>!t.useMaterialSettings||t.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial.shader.name!="Game/Living Forest"));
 if(missing!=0||errors!=0||storm.x<.78f||wind.y<1.8f||mud.x<.35f||badTrees>0||world.weather.profile.automaticWeather||world.replacedRoots.Any(g=>g.activeSelf))throw new Exception("Mechanics/material configuration failed");
 return new{success=true,loadedTiles=world.LoadedTiles,activeTrees=trees.Length,materialSlots=6,missingMaterials=missing,shaderErrors=errors,rainNormal=calm.x,rainDownpour=storm.x,strongWind=wind.y,mudRain=mud.x,paletteTrees=p.trees.Length,paletteRocks=p.rocks.Length};
 }
}

