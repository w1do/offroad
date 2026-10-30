using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(VehiclePhysics))]
public sealed class VehicleDiagnostics : MonoBehaviour
{
    public bool visible;
    VehiclePhysics vehicle;
    readonly StringBuilder text = new StringBuilder(2048);
    void OnEnable() { vehicle = GetComponent<VehiclePhysics>(); }
    void Update()
    {
        var equipment = vehicle == null || vehicle.profile == null ? null : vehicle.profile.equipment;
        if(equipment!=null&&equipment.diagnosticsKey!=Key.None&&Keyboard.current!=null&&Keyboard.current[equipment.diagnosticsKey].wasPressedThisFrame)visible=!visible;
    }
    void OnGUI()
    {
        if(!visible||vehicle==null||!vehicle.Ready)return;
        text.Clear();text.AppendLine($"RPM {vehicle.EngineRpm:F0}   load {vehicle.EngineLoad:P0}   speed {vehicle.SpeedKmh:F1} km/h   diff {(vehicle.DifferentialLocked?"LOCK":"OPEN/LSD")}");
        text.AppendLine($"Position {transform.position:F2}   wheelspin {vehicle.WheelspinSpeed:F2} m/s");
        text.AppendLine($"Edge supports {vehicle.ShoulderSupports}, footprint height gain {vehicle.MaximumShoulderHeightGainMetres*1000:F1} mm");
        text.AppendLine($"Arc supports {vehicle.ArcSupports}, longitudinal normal reaction {vehicle.ArcNormalReactionNewtons:F0} N");
        text.AppendLine($"Rut shoulders {vehicle.RutWallContacts}, lateral reaction {vehicle.RutWallForceNewtons:F0} N, soil/collider gap {vehicle.MaximumSoilContactErrorMetres*1000:F1} mm");
        for(int i=0;i<vehicle.Wheels.Length;i++)
        {
            var wheel=vehicle.Wheels[i];if(!wheel.GetGroundHit(out var hit)){text.AppendLine($"Wheel {i}: airborne, {wheel.rpm:F0} rpm");continue;}
            var soil=hit.collider.GetComponent<SoftGroundRuts>();
            text.AppendLine($"Wheel {i}: {wheel.rpm:F0} rpm, load {hit.force:F0} N, slip {hit.forwardSlip:F2}/{hit.sidewaysSlip:F2}, brake {wheel.brakeTorque:F0} Nm");
            text.AppendLine($"  {hit.collider.name}, mu {wheel.forwardFriction.stiffness:F2}/{wheel.sidewaysFriction.stiffness:F2}"+(soil==null?"":$", rut {soil.DepthAt(hit.point):F3} m, pressure {soil.LastPressurePascal/1000:F1} kPa, compaction {soil.CompactionAt(hit.point):F2}, cooks {soil.CommitCount}"));
        }
        var water=GetComponent<VehicleWaterInteraction>();if(water!=null)text.AppendLine($"Water {water.MaximumDepthMetres:F2} m, force {water.LastForceNewtons:F0} N, intake {water.IntakeExposureSeconds:F2} s, flooded {water.IntakeFlooded}");
        GUI.Box(new Rect(Screen.width-720,260,700,330),text.ToString());
    }
}
