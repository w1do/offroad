using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
public sealed class VehicleEngineAudioBand
{
    public string name;
    [Min(1)]public float referenceRpm=2000;
    public AudioClip coast,loaded;
    [Range(0,1)]public float gain=.8f;
}
[MovedFrom(true,"","Assembly-CSharp","TruckAudioProfile")]
[CreateAssetMenu(menuName="Game/Vehicles/Audio profile")]
public sealed class VehicleAudioProfile : ScriptableObject
{
    [Header("Recorded engine layers, ascending RPM")]
    public VehicleEngineAudioBand[] engineBands;
    public AudioClip engineStart;
    [Min(0)]public float startupBlendSeconds=1.8f;
    public Vector3 enginePosition=new Vector3(0,1,1.2f);
    [Range(0,1)]public float engineSpatialBlend=.8f;
    [Min(.01f)]public float rpmResponse=18,loadResponse=7;
    public float minimumRecordedPitch=.65f,maximumRecordedPitch=1.65f;
    [Header("Road / mechanics and legacy engine fallback")]
    public AudioClip idle, load, road, slip, chains, brakeAir, gear, chainFitting, horn;
    [Range(0, 1)] public float masterVolume = 0.8f;
    [Range(0, 1)] public float engineVolume = 0.42f;
    [Range(0, 1)] public float roadVolume = 0.14f;
    [Range(0, 1)] public float slipVolume = 0.18f;
    [Range(0, 1)] public float chainVolume = 0.15f;
    [Range(0, 1)] public float hornVolume = 0.28f;
    [Range(0, 1)] public float effectVolume = 0.3f;
    public float idleRpm = 600;
    public float maximumRpm = 2400;
    public float engineResponse = 3.5f;
    public float wheelToEngineRatio = 9f;
    public float idleReferenceRpm=600,loadReferenceRpm=1600,maximumEnginePitch=4.5f;
    public float minDistance = 6;
    public float maxDistance = 90;
}
