using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public sealed class TruckChains : MonoBehaviour
{
    public float installationSeconds=4,maximumInstallSpeedKmh=.3f;
    public GameObject[] wheelChainVisuals;
    public bool Installed {get;private set;}
    public bool Working {get;private set;}
    public float Progress {get;private set;}
    public float Integrity {get;private set;}=1;
    public float TemperatureC {get;private set;}=-10;
    public bool Broken {get;private set;}
    public float GripFactor=>Installed&&!Broken?Mathf.Lerp(.35f,1,Integrity):0;
    public string Message {get;private set;}="1: цепи на все колёса — остановитесь и держите Space";
    ForestKamazController truck;VehiclePhysics vehicle;
    VehicleEquipmentProfile Config=>vehicle!=null&&vehicle.profile!=null?vehicle.profile.equipment:null;
    float Speed=>vehicle!=null&&vehicle.enabled?vehicle.SpeedKmh:truck!=null?truck.SpeedKmh:0;
    bool Parked=>vehicle!=null&&vehicle.enabled?vehicle.ParkingBrakeApplied:truck!=null&&truck.ParkingBrakeApplied;
    bool Testing=>vehicle!=null&&vehicle.enabled?vehicle.externalControl:truck!=null&&truck.testControl;
    void Awake(){truck=GetComponent<ForestKamazController>();vehicle=GetComponent<VehiclePhysics>();if(Config!=null)TemperatureC=Config.ambientTemperatureC;}
    void OnEnable(){truck=GetComponent<ForestKamazController>();vehicle=GetComponent<VehiclePhysics>();EnsureVisuals();RefreshVisuals();}
    void EnsureVisuals()
    {
        if(!Application.isPlaying||Config==null||!Config.chainsAvailable||vehicle.axles==null)return;
        int count=vehicle.axles.Length*2;if(wheelChainVisuals==null)wheelChainVisuals=new GameObject[count];
        if(wheelChainVisuals.Length!=count)System.Array.Resize(ref wheelChainVisuals,count);
        for(int a=0;a<vehicle.axles.Length;a++)for(int side=0;side<2;side++){
            int index=a*2+side;if(wheelChainVisuals[index]!=null)continue;
            var hub=side==0?vehicle.axles[a].leftVisual:vehicle.axles[a].rightVisual;if(hub==null)continue;
            var root=new GameObject("Wheel chains "+index);root.transform.SetParent(hub,false);
            int links=Mathf.Max(8,Config.chainLinkCount);
            for(int j=0;j<links;j++){float angle=j*Mathf.PI*2/links;var link=GameObject.CreatePrimitive(PrimitiveType.Cube);var collider=link.GetComponent<Collider>();collider.enabled=false;Destroy(collider);link.transform.SetParent(root.transform,false);link.transform.localPosition=new Vector3(0,Mathf.Cos(angle)*vehicle.profile.axles[a].wheelRadius,Mathf.Sin(angle)*vehicle.profile.axles[a].wheelRadius);link.transform.localRotation=Quaternion.Euler(angle*Mathf.Rad2Deg,0,0);link.transform.localScale=new Vector3(Config.chainWidthMetres,.018f,.025f);if(Config.chainMaterial!=null)link.GetComponent<Renderer>().sharedMaterial=Config.chainMaterial;}
            root.SetActive(false);wheelChainVisuals[index]=root;
        }
    }
    public bool RequestToggle()
    {
        if(Working)return false;
        if(Config!=null&&!Config.chainsAvailable){Message="Цепи не предусмотрены профилем машины";return false;}
        if(Broken){Message="Цепи порваны. Снятие и установка не восстанавливают комплект";return false;}
        if(Speed>(Config==null?maximumInstallSpeedKmh:Config.maximumInstallSpeedKmh)||!Parked){Message="Остановитесь и держите Space, затем нажмите 1";return false;}
        Working=true;Progress=0;Message=Installed?"Снимаем цепи — держите Space":"Ставим цепи на все колёса — держите Space";return true;
    }
    public void Advance(float seconds)
    {
        if(!Working||seconds<=0)return;
        if(Speed>(Config==null?maximumInstallSpeedKmh:Config.maximumInstallSpeedKmh)||!Parked){Working=false;Progress=0;Message="Установка отменена: машина должна стоять на ручнике";return;}
        Progress=Mathf.Clamp01(Progress+seconds/Mathf.Max(.1f,Config==null?installationSeconds:Config.installationSeconds));
        if(Progress>=1){Installed=!Installed;Working=false;Message=Installed?"Цепи установлены на все колёса":"Цепи сняты — остывают";RefreshVisuals();}
    }
    public void AdvanceCondition(float seconds,float slipMetresPerSecond)
    {
        if(Config==null||seconds<=0)return;var c=Config;
        TemperatureC=Mathf.Max(c.ambientTemperatureC,TemperatureC-c.chainCoolingCPerSecond*seconds);
        if(!Installed||Broken)return;
        float slipMetres=Mathf.Max(0,slipMetresPerSecond)*seconds;
        TemperatureC+=slipMetres*c.chainHeatingCPerSlipMetre;
        float hot=Mathf.InverseLerp(c.chainWarningTemperatureC,c.chainBreakTemperatureC,TemperatureC);
        Integrity=Mathf.Clamp01(Integrity-slipMetres*c.chainWearPerSlipMetre*Mathf.Lerp(1,c.hotChainWearMultiplier,hot));
        if(Integrity<=0||TemperatureC>=c.chainBreakTemperatureC){Broken=true;Installed=false;Working=false;Integrity=0;Message="ЦЕПИ ПОРВАНЫ: перегрев или полный износ";RefreshVisuals();}
        else if(TemperatureC>=c.chainWarningTemperatureC)Message="ЦЕПИ ПЕРЕГРЕВАЮТСЯ — сбросьте газ и дайте им остыть";
        else if(Message.StartsWith("ЦЕПИ ПЕРЕГРЕВАЮТСЯ"))Message="Цепи остыли — можно продолжать";
    }
    void Update(){if(Testing)return;if(vehicle==null&&Keyboard.current!=null&&Keyboard.current.digit1Key.wasPressedThisFrame)RequestToggle();Advance(Time.deltaTime);}
    public void RefreshVisuals(){if(wheelChainVisuals!=null)foreach(var visual in wheelChainVisuals)if(visual!=null)visual.SetActive(Installed&&!Broken);}
}
