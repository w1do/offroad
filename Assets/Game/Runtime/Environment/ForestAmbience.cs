using UnityEngine;

public sealed class ForestAmbience : MonoBehaviour
{
    public AudioSource birds;
    public AudioSource wind;
    public ForestWind forestWind;
    public DriveCamera drivingCamera;
    [Range(0,1)]public float birdVolume=0.13f;
    [Range(0,1)]public float windVolume=0.07f;
    private void OnEnable()
    {if(!Application.isPlaying)return;if(birds!=null)birds.Play();if(wind!=null)wind.Play();}
    private void OnDisable(){if(birds!=null)birds.Stop();if(wind!=null)wind.Stop();}
    private void Update()
    {
        float cabin=drivingCamera!=null&&drivingCamera.hood?0.5f:1;
        float gust=forestWind==null?0.8f:forestWind.strength*(0.85f+0.15f*Mathf.Sin(forestWind.WindTime*0.4f));
        if(birds!=null)birds.volume=Mathf.Lerp(birds.volume,birdVolume*cabin,1-Mathf.Exp(-2*Time.deltaTime));
        if(wind!=null)wind.volume=Mathf.Lerp(wind.volume,windVolume*gust*cabin,1-Mathf.Exp(-2*Time.deltaTime));
    }
}
