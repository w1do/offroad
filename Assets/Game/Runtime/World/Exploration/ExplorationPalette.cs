using UnityEngine;

[CreateAssetMenu(menuName = "Game/World/Exploration palette")]
public sealed class ExplorationPalette : ScriptableObject
{
    public Material forest, road, mud, shore, water, lakeWater, bog, grassDetail, shrubDetail;
    public Mesh[] grassClumps, shrubClumps;
    public GameObject[] trees, rocks, fallenTrees;
    public SoftGroundProfile roadSoil;
    public ForestWeatherProfile weather;
    public Mesh puddleDisk;
}


