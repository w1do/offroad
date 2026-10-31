using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(VehiclePhysics))]
public sealed class VehiclePlayerInput : MonoBehaviour
{
    VehiclePhysics vehicle;TruckChains chains;VehicleWinch winch;VehicleTyrePressure tyres;
    void OnEnable(){vehicle=GetComponent<VehiclePhysics>();chains=GetComponent<TruckChains>();winch=GetComponent<VehicleWinch>();tyres=GetComponent<VehicleTyrePressure>();}
    void Update()
    {
        if(vehicle==null||vehicle.externalControl||Keyboard.current==null)return;var k=Keyboard.current;var e=vehicle.profile==null?null:vehicle.profile.equipment;
        if(e!=null&&e.pressureKey!=Key.None&&k[e.pressureKey].wasPressedThisFrame&&tyres!=null)tyres.ToggleWindow();
        bool pressureWindow=tyres!=null&&(tyres.WindowOpen||tyres.ServiceParkingBrake&&tyres.Adjusting);
        if(k.eKey.wasPressedThisFrame)vehicle.Shift(1);if(k.qKey.wasPressedThisFrame)vehicle.Shift(-1);if(k.nKey.wasPressedThisFrame)vehicle.SetNeutral(!vehicle.Neutral);
        if(vehicle.SpeedKmh<1.5f){if(k.vKey.wasPressedThisFrame)vehicle.SetTransmission(!vehicle.ReverseGear,vehicle.LowRange);if(k.lKey.wasPressedThisFrame)vehicle.SetTransmission(vehicle.ReverseGear,!vehicle.LowRange);}
        if(k.rKey.wasPressedThisFrame)vehicle.DebugRecover();
        var lighting=vehicle.Lighting!=null?vehicle.Lighting:GetComponent<VehicleLighting>();
        var lightProfile=vehicle.profile==null?null:vehicle.profile.lighting;
        if(lighting!=null&&lightProfile!=null){if(lightProfile.modeKey!=Key.None&&k[lightProfile.modeKey].wasPressedThisFrame)vehicle.CycleHeadlightMode();if(lightProfile.lowBeamKey!=Key.None&&k[lightProfile.lowBeamKey].wasPressedThisFrame)vehicle.SetLowBeam(!vehicle.LowBeamOn);if(lightProfile.highBeamKey!=Key.None&&k[lightProfile.highBeamKey].wasPressedThisFrame)vehicle.SetHighBeam(!vehicle.HighBeamOn);if(lightProfile.fogLampsKey!=Key.None&&k[lightProfile.fogLampsKey].wasPressedThisFrame)vehicle.SetFogLamps(!lighting.FogLampsRequested);}
        vehicle.SetInput(new VehicleControlInput{throttle=!pressureWindow&&(k.wKey.isPressed||k.upArrowKey.isPressed)?1:0,brake=k.sKey.isPressed||k.downArrowKey.isPressed?1:0,steering=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),handbrake=k.spaceKey.isPressed||tyres!=null&&tyres.ServiceParkingBrake,precision=k.leftShiftKey.isPressed||k.rightShiftKey.isPressed});
        if(e!=null){if(e.differentialLockKey!=Key.None&&k[e.differentialLockKey].wasPressedThisFrame)vehicle.SetDifferentialLock(!vehicle.DifferentialLocked);if(e.driveKey!=Key.None&&k[e.driveKey].wasPressedThisFrame)vehicle.SetFourWheelDrive(!vehicle.FourWheelDrive);if(e.chainsKey!=Key.None&&k[e.chainsKey].wasPressedThisFrame&&chains!=null)chains.RequestToggle();if(e.winchKey!=Key.None&&k[e.winchKey].wasPressedThisFrame&&winch!=null)winch.Toggle();}
        else if(k.digit1Key.wasPressedThisFrame&&chains!=null)chains.RequestToggle();
    }
    string KeyName(Key key)=>key.ToString().Replace("Digit","");
    void OnGUI()
    {
        if(vehicle==null||vehicle.profile==null)return;var e=vehicle.profile.equipment;
        GUI.Box(new Rect(18,18,750,148),vehicle.profile.displayName);GUI.Label(new Rect(30,43,725,116),$"{vehicle.SpeedKmh:F0} км/ч    {vehicle.EngineRpm:F0} об/мин    Передача: {(vehicle.Neutral?"N":vehicle.ReverseGear?"R":vehicle.Gear.ToString())}   {(vehicle.LowRange?"LOW":"HIGH")}   {(vehicle.FourWheelDrive?"4WD":"2WD")}\n{(vehicle.DrivenSlip>.35f?"ПРОБУКСОВКА":"")}  {(vehicle.Sliding?"ЗАНОС":"")}   Угол: {vehicle.SlipAngle:F0}°\nW: газ   Shift: малый газ   S: тормоз   Space: ручник\nQ/E: передачи   N: нейтраль   L: диапазон   V: задний ход\n{(e==null?"H":KeyName(e.driveKey))}: привод   {(e==null?"1":KeyName(e.chainsKey))} + Space: цепи   {(e==null?"2":KeyName(e.winchKey))}: лебёдка   {(e==null?"B":KeyName(e.hornKey))}: сигнал   R: вернуть машину");
        if(tyres!=null&&e!=null)GUI.Label(new Rect(435,192,410,42),$"{KeyName(e.pressureKey)}: шины {tyres.PressureBar:F1} бар {(tyres.Adjusting?$"→ {tyres.TargetBar:F1}":"\n"+tyres.Message)}");
        var lighting=vehicle.Lighting!=null?vehicle.Lighting:GetComponent<VehicleLighting>();var lp=vehicle.profile.lighting;
        if(lighting!=null&&lp!=null){
            float lightX=Mathf.Max(18,Screen.width-448);
            GUI.Box(new Rect(lightX,18,430,100),"СВЕТ МАШИНЫ");
            GUI.Label(new Rect(lightX+12,40,406,40),$"{KeyName(lp.modeKey)}: {(lighting.Mode==VehicleHeadlightMode.Automatic?"АВТО":lighting.Mode==VehicleHeadlightMode.On?"ВКЛ":"ВЫКЛ")}\n{KeyName(lp.lowBeamKey)}: ближний {(vehicle.LowBeamOn?"вкл":"выкл")}   {KeyName(lp.highBeamKey)}: дальний {(vehicle.HighBeamOn?"вкл":"выкл")}   {KeyName(lp.fogLampsKey)}: туманки {(vehicle.FogLampsOn?"вкл":"выкл")}");
            bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled&&!vehicle.externalControl;
            if(GUI.Button(new Rect(lightX+12,82,94,24),"Ближний"))vehicle.SetLowBeam(!vehicle.LowBeamOn);
            if(GUI.Button(new Rect(lightX+116,82,94,24),"Дальний"))vehicle.SetHighBeam(!vehicle.HighBeamOn);
            if(GUI.Button(new Rect(lightX+220,82,94,24),"Туманки"))vehicle.SetFogLamps(!lighting.FogLampsRequested);
            if(GUI.Button(new Rect(lightX+324,82,94,24),"Авто"))vehicle.SetHeadlightMode(VehicleHeadlightMode.Automatic);
            GUI.enabled=wasEnabled;
        }
        var water=vehicle.GetComponent<VehicleWaterInteraction>();
        if(water!=null&&water.IntakeFlooded)GUI.Box(new Rect(Screen.width-400,180,380,55),"ДВИГАТЕЛЬ ЗАТОПЛЕН\nТяга недоступна — требуется эвакуация");
        else if(water!=null&&water.IntakeExposureSeconds>0)GUI.Box(new Rect(Screen.width-400,180,380,55),"ВОДА У ВОЗДУХОЗАБОРНИКА\nСнизьте глубину погружения");
        if(e==null)return;float y=Mathf.Max(245,Screen.height-225);GUI.Box(new Rect(18,y,580,210),"ОСНАЩЕНИЕ МАШИНЫ");GUI.Label(new Rect(30,y+22,550,22),vehicle.DriveMessage+"   "+KeyName(e.differentialLockKey)+": "+vehicle.DifferentialMessage);
        if(chains!=null&&e.chainsAvailable){GUI.Label(new Rect(30,y+45,550,22),$"Цепи: {(chains.Broken?"ПОРВАНЫ":chains.Working?$"установка {chains.Progress:P0}":chains.Installed?"на всех колёсах":"сняты")}   Износ {(1-chains.Integrity)*100:F0}%   {chains.TemperatureC:F0} °C");Bar(30,y+69,260,1-chains.Integrity,Color.red);Bar(302,y+69,260,Mathf.InverseLerp(e.ambientTemperatureC,e.chainBreakTemperatureC,chains.TemperatureC),chains.TemperatureC>=e.chainWarningTemperatureC?Color.red:Color.yellow);GUI.Label(new Rect(30,y+86,550,22),chains.Message);}
        if(winch!=null&&e.winchAvailable){GUI.Label(new Rect(30,y+113,550,22),$"Лебёдка: {(winch.Broken?"ТРОС ОБОРВАН":winch.Pulling?"ТЯНЕТ":"выключена")}   Износ троса {(1-winch.CableIntegrity)*100:F0}%   Тяга {winch.TensionNewtons/1000:F1} кН");Bar(30,y+137,260,1-winch.CableIntegrity,Color.red);Bar(302,y+137,260,winch.Heat,winch.Heat>.75f?Color.red:Color.yellow);GUI.Label(new Rect(30,y+154,550,22),"Слева: износ троса. Справа: длительность нагрузки / риск обрыва");GUI.Label(new Rect(30,y+177,550,22),winch.Message);}
    }
    static void Bar(float x,float y,float width,float value,Color color){GUI.Box(new Rect(x,y,width,12),GUIContent.none);var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x+2,y+2,(width-4)*Mathf.Clamp01(value),8),Texture2D.whiteTexture);GUI.color=old;}
}
