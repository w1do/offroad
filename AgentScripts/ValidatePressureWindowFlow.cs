using System;
using UnityEngine;
public static class ValidatePressureWindowFlow
{
    static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    public static string Main()
    {
        var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var clone=UnityEngine.Object.Instantiate(source.gameObject,source.transform.position+Vector3.up*200,Quaternion.identity);
        try{
            var v=clone.GetComponent<VehiclePhysics>();v.automaticSimulation=false;v.externalControl=true;
            clone.GetComponent<VehiclePlayerInput>().enabled=false;
            var rb=clone.GetComponent<Rigidbody>();rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
            var p=clone.GetComponent<VehicleTyrePressure>();
            v.SetInput(default);p.ToggleWindow();Require(p.WindowOpen,"Window closed");
            Require(p.ApplyFromWindow(1.2f),p.Message);
            Require(p.ServiceParkingBrake&&v.ParkingBrakeApplied&&v.Input.throttle==0&&p.Adjusting,"Apply did not hold parking brake/start");
            float start=p.PressureBar;p.Advance(2);Require(p.PressureBar<start&&p.PressureBar>1.2f,"Not gradual");
            p.CloseWindow();Require(!p.WindowOpen&&p.Adjusting&&p.ServiceParkingBrake,"Closing interrupted service");
            p.Advance(100);Require(Mathf.Abs(p.PressureBar-1.2f)<.001f&&!p.Adjusting&&!p.ServiceParkingBrake,"Did not finish/release service brake");
            p.ToggleWindow();Require(p.ApplyFromWindow(2.2f),p.Message);p.Advance(3);float partial=p.PressureBar;
            p.CancelAdjustment();Require(!p.Adjusting&&p.PressureBar==partial&&p.TargetBar==partial,"Cancellation lost actual pressure");
            p.CloseWindow();Require(!p.ServiceParkingBrake,"Service brake retained after close");
            p.ToggleWindow();rb.linearVelocity=Vector3.forward*5;Require(!p.ApplyFromWindow(1.2f)&&!p.ServiceParkingBrake,"Accepted while moving");
            rb.linearVelocity=Vector3.zero;Require(!p.ApplyFromWindow(float.NaN)&&!p.ApplyFromWindow(99),"Invalid request accepted");
            string report="PASS window Apply without Space starts gradual deflation with parking brake; close continues; 1.2 bar reached and service brake released; cancel preserves actual pressure; moving/invalid rejected.";
            System.IO.File.WriteAllText("docs/reports/pressure-window-flow.txt",report);return report;
        }finally{UnityEngine.Object.DestroyImmediate(clone);}
    }
}
