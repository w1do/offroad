using UnityEngine;

[RequireComponent(typeof(VehiclePhysics))]
public sealed class VehicleTyrePressure : MonoBehaviour
{
    public float PressureBar {get;private set;}
    public float TargetBar {get;private set;}
    public bool Adjusting {get;private set;}
    public bool WindowOpen {get;private set;}
    public bool ServiceParkingBrake {get;private set;}
    public float RemainingSeconds=>Config==null||!Adjusting?0:Mathf.Abs(TargetBar-PressureBar)/Mathf.Max(.001f,TargetBar>PressureBar?Config.inflationBarPerSecond:Config.deflationBarPerSecond);
    public string Message {get;private set;}="3: давление в шинах";
    VehiclePhysics vehicle;float selected;
    VehicleEquipmentProfile Config=>vehicle==null||vehicle.profile==null?null:vehicle.profile.equipment;
    public float Deflation=>Config==null?0:Mathf.InverseLerp(Config.roadPressureBar,Config.minimumTyrePressureBar,PressureBar);
    public float SoftGripMultiplier=>Config==null?1:Mathf.Lerp(1,Config.lowPressureSoftGripMultiplier,Deflation);
    public float SoilLoadMultiplier=>Config==null?1:Mathf.Lerp(1,Config.lowPressureSoilLoadMultiplier,Deflation);
    public float RollingResistance=>Config==null?0:Config.lowPressureRollingResistance*Deflation;
    void Awake(){vehicle=GetComponent<VehiclePhysics>();if(Config!=null)PressureBar=TargetBar=selected=Config.roadPressureBar;}
    public void ToggleWindow(){if(Config==null||!Config.pressureAdjustmentAvailable){Message="Регулировка давления недоступна";return;}if(WindowOpen){CloseWindow();return;}WindowOpen=true;selected=PressureBar;}
    public void CloseWindow()
    {
        WindowOpen=false;
        if(!Adjusting)ServiceParkingBrake=false;
    }
    public void CancelAdjustment()
    {
        Adjusting=false;TargetBar=PressureBar;ServiceParkingBrake=WindowOpen&&ServiceParkingBrake;
        Message=$"Регулировка остановлена: {PressureBar:F2} бар";
    }
    // Same command is used by the UI and its integration test. The UI owns only
    // a parking-brake request; VehiclePhysics remains the owner of brake forces.
    public bool ApplyFromWindow(float bar)
    {
        if(!WindowOpen||Config==null||!Config.pressureAdjustmentAvailable)return false;
        if(float.IsNaN(bar)||float.IsInfinity(bar)||bar<Config.minimumTyrePressureBar||bar>Config.maximumTyrePressureBar)return RequestPressure(bar);
        if(vehicle.SpeedKmh>Config.maximumPressureAdjustmentSpeedKmh){Message="Остановитесь: регулировка доступна только на стоянке";return false;}
        ServiceParkingBrake=true;
        var input=vehicle.Input;input.throttle=0;input.handbrake=true;vehicle.SetInput(input);
        bool accepted=RequestPressure(bar);
        if(accepted&&Adjusting)Message="Ручник включён. Давление меняется — дождитесь завершения";
        return accepted;
    }
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
        if(vehicle.SpeedKmh>Config.maximumPressureAdjustmentSpeedKmh||!vehicle.ParkingBrakeApplied){Adjusting=false;TargetBar=PressureBar;if(!WindowOpen)ServiceParkingBrake=false;Message="Регулировка остановлена: удерживайте машину на ручнике";return;}
        float rate=TargetBar>PressureBar?Config.inflationBarPerSecond:Config.deflationBarPerSecond;
        PressureBar=Mathf.MoveTowards(PressureBar,TargetBar,Mathf.Max(.001f,rate)*seconds);
        if(Mathf.Abs(PressureBar-TargetBar)<.005f){PressureBar=TargetBar;Adjusting=false;if(!WindowOpen)ServiceParkingBrake=false;Message=$"Давление установлено: {PressureBar:F1} бар";}
    }
    TyreWheelShape[] shapes;
    public int DeformedMeshCount { get { int count=0;if(shapes!=null)foreach(var shape in shapes)count+=shape.MeshCount;return count;} }
    public void BindWheels()
    {
        ReleaseShapes();
        if(vehicle==null)vehicle=GetComponent<VehiclePhysics>();
        if(!Application.isPlaying||Config==null||!vehicle.Ready)return;
        shapes=new TyreWheelShape[vehicle.Wheels.Length];
        for(int i=0;i<shapes.Length;i++){
            var axle=vehicle.axles[i/2];var setting=vehicle.profile.axles[i/2];
            float radius=setting.tire==null?setting.wheelRadius:setting.tire.radiusMetres;
            shapes[i]=new TyreWheelShape(vehicle.Wheels[i],i%2==0?axle.leftVisual:axle.rightVisual,radius);
        }
    }
    public void SimulateShape(float seconds)
    {
        if(Config==null||shapes==null)return;
        float referenceLoad=GetComponent<Rigidbody>().mass*Physics.gravity.magnitude/Mathf.Max(1,shapes.Length);
        foreach(var shape in shapes)shape.Simulate(Config,Deflation,referenceLoad,seconds);
    }
    void LateUpdate()
    {
        if(vehicle==null)vehicle=GetComponent<VehiclePhysics>();
        if(shapes==null&&vehicle.Ready)BindWheels();
        if(shapes==null||Config==null||!vehicle.Ready)return;
        // VehiclePhysics poses the hubs separately; use the collider pose here so
        // deformation does not depend on the order of the two LateUpdates.
        for(int i=0;i<shapes.Length;i++){vehicle.GetWheelPose(i,out var hub,out var rotation);shapes[i].Draw(Config,Deflation,hub,rotation);}
    }
    void ReleaseShapes(){if(shapes!=null)foreach(var shape in shapes)shape.Dispose();shapes=null;}
    void OnDisable(){ReleaseShapes();}
    void OnDestroy(){ReleaseShapes();}
    void OnGUI()
    {
        if(!WindowOpen||Config==null)return;float x=Mathf.Max(5,(Screen.width-440)*.5f),y=Mathf.Max(5,(Screen.height-310)*.5f);
        var oldColor=GUI.color;GUI.color=new Color(0,0,0,.3f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=new Color(.045f,.055f,.06f,.97f);GUI.DrawTexture(new Rect(x,y,440,310),Texture2D.whiteTexture);GUI.color=oldColor;
        GUI.Box(new Rect(x,y,440,310),"ДАВЛЕНИЕ ВО ВСЕХ ШИНАХ");
        GUI.Label(new Rect(x+18,y+30,405,24),$"ФАКТИЧЕСКИ: {PressureBar:F2} бар");
        GUI.Label(new Rect(x+18,y+58,405,24),$"Выбранная цель: {selected:F1} бар");
        selected=Mathf.Round(GUI.HorizontalSlider(new Rect(x+20,y+90,400,20),selected,Config.minimumTyrePressureBar,Config.maximumTyrePressureBar)*10)/10;
        if(GUI.Button(new Rect(x+18,y+119,126,28),"Мягкий грунт"))selected=Config.softGroundPressureBar;
        if(GUI.Button(new Rect(x+156,y+119,126,28),"Дорожное"))selected=Config.roadPressureBar;
        if(GUI.Button(new Rect(x+294,y+119,126,28),"Применить"))ApplyFromWindow(selected);
        GUI.Label(new Rect(x+18,y+157,405,42),Adjusting?$"{(TargetBar<PressureBar?"СПУСК":"ПОДКАЧКА")} до {TargetBar:F1} бар. Осталось {RemainingSeconds:F0} с":"Применить включает ручник и запускает регулировку.");
        GUI.Label(new Rect(x+18,y+196,405,40),Message);
        GUI.Label(new Rect(x+18,y+237,405,28),Adjusting?"Форма шин меняется вместе с фактическим давлением.":"Сначала остановитесь. Удерживать Space в окне не нужно.");
        if(Adjusting&&GUI.Button(new Rect(x+156,y+275,126,24),"Остановить"))CancelAdjustment();
        if(GUI.Button(new Rect(x+294,y+275,126,24),"Закрыть"))CloseWindow();
    }
}
