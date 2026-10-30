using UnityEngine;

[RequireComponent(typeof(VehiclePhysics))]
public sealed class VehicleTyrePressure : MonoBehaviour
{
    public float PressureBar {get;private set;}
    public float TargetBar {get;private set;}
    public bool Adjusting {get;private set;}
    public bool WindowOpen {get;private set;}
    public string Message {get;private set;}="3: давление в шинах";
    VehiclePhysics vehicle;float selected;
    VehicleEquipmentProfile Config=>vehicle==null||vehicle.profile==null?null:vehicle.profile.equipment;
    public float Deflation=>Config==null?0:Mathf.InverseLerp(Config.roadPressureBar,Config.minimumTyrePressureBar,PressureBar);
    public float SoftGripMultiplier=>Config==null?1:Mathf.Lerp(1,Config.lowPressureSoftGripMultiplier,Deflation);
    public float SoilLoadMultiplier=>Config==null?1:Mathf.Lerp(1,Config.lowPressureSoilLoadMultiplier,Deflation);
    public float RollingResistance=>Config==null?0:Config.lowPressureRollingResistance*Deflation;
    void Awake(){vehicle=GetComponent<VehiclePhysics>();if(Config!=null)PressureBar=TargetBar=selected=Config.roadPressureBar;}
    public void ToggleWindow(){if(Config==null||!Config.pressureAdjustmentAvailable){Message="Регулировка давления недоступна";return;}WindowOpen=!WindowOpen;selected=PressureBar;}
    public bool RequestPressure(float bar)
    {
        if(Config==null||!Config.pressureAdjustmentAvailable)return false;
        if(float.IsNaN(bar)||float.IsInfinity(bar)||bar<Config.minimumTyrePressureBar||bar>Config.maximumTyrePressureBar){Message="Давление за пределами профиля машины";return false;}
        if(vehicle.SpeedKmh>Config.maximumPressureAdjustmentSpeedKmh||!vehicle.ParkingBrakeApplied){Message="Остановитесь и удерживайте Space для регулировки давления";return false;}
        TargetBar=bar;Adjusting=Mathf.Abs(PressureBar-bar)>.005f;Message=Adjusting?"Держите Space: давление меняется во всех шинах":"Выбранное давление уже установлено";return true;
    }
    public void Advance(float seconds)
    {
        if(Config==null||seconds<=0||!Adjusting)return;
        if(vehicle.SpeedKmh>Config.maximumPressureAdjustmentSpeedKmh||!vehicle.ParkingBrakeApplied){Adjusting=false;TargetBar=PressureBar;Message="Регулировка остановлена: удерживайте машину на ручнике";return;}
        float rate=TargetBar>PressureBar?Config.inflationBarPerSecond:Config.deflationBarPerSecond;
        PressureBar=Mathf.MoveTowards(PressureBar,TargetBar,Mathf.Max(.001f,rate)*seconds);
        if(Mathf.Abs(PressureBar-TargetBar)<.005f){PressureBar=TargetBar;Adjusting=false;Message=$"Давление установлено: {PressureBar:F1} бар";}
    }
    void OnGUI()
    {
        if(!WindowOpen||Config==null)return;float x=Mathf.Max(5,(Screen.width-440)*.5f),y=Mathf.Max(5,(Screen.height-270)*.5f);
        var oldColor=GUI.color;GUI.color=new Color(0,0,0,.3f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=new Color(.045f,.055f,.06f,.97f);GUI.DrawTexture(new Rect(x,y,440,270),Texture2D.whiteTexture);GUI.color=oldColor;
        GUI.Box(new Rect(x,y,440,270),"ДАВЛЕНИЕ ВО ВСЕХ ШИНАХ");
        GUI.Label(new Rect(x+18,y+30,405,24),$"Сейчас {PressureBar:F2} бар   Цель {TargetBar:F1} бар");
        GUI.Label(new Rect(x+18,y+58,405,24),$"Выбрать: {selected:F1} бар");
        selected=Mathf.Round(GUI.HorizontalSlider(new Rect(x+20,y+90,400,20),selected,Config.minimumTyrePressureBar,Config.maximumTyrePressureBar)*10)/10;
        if(GUI.Button(new Rect(x+18,y+119,126,28),"Мягкий грунт"))selected=Config.softGroundPressureBar;
        if(GUI.Button(new Rect(x+156,y+119,126,28),"Дорожное"))selected=Config.roadPressureBar;
        if(GUI.Button(new Rect(x+294,y+119,126,28),"Применить"))RequestPressure(selected);
        GUI.Label(new Rect(x+18,y+157,405,42),"Остановитесь и держите Space. Спуск и подкачка требуют времени.");
        GUI.Label(new Rect(x+18,y+196,405,40),Message);
        if(GUI.Button(new Rect(x+294,y+235,126,24),"Закрыть"))WindowOpen=false;
    }
}
