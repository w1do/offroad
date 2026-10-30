using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ValidateForestKamaz
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying) throw new Exception("Requires Play mode.");
        Application.runInBackground=true;
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();
        var body=car.GetComponent<Rigidbody>();
        var start=new Vector3(-20.59f,43.46f,42.08f);
        var original=car.transform.rotation;
        int frame=Time.frameCount;
        try
        {
            car.testControl=true;car.testThrottle=0;car.testBrake=true;
            body.position=start;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            await Wait(1);
            int contacts=car.wheels.Count(w=>w.isGrounded);
            if(contacts<4) throw new Exception("Insufficient grounded wheels: "+contacts);
            var settled=body.position;
            car.testBrake=false;car.testThrottle=0.7f;
            await Wait(2);
            float speed=car.SpeedKmh, travel=Vector3.Distance(settled,body.position);
            if(speed<2 || travel<0.5f) throw new Exception("Acceleration failed: speed="+speed+" travel="+travel);
            car.testThrottle=0;car.testBrake=true;
            await Wait(1.5f);
            float stopped=car.SpeedKmh;
            if(stopped>speed || stopped>2) throw new Exception("Braking failed: "+stopped);
            car.testBrake=false;car.testThrottle=0;car.testSteer=0.5f;
            await Wait(0.2f);
            if(car.wheels[0].steerAngle<10 || Mathf.Abs(car.wheels[2].steerAngle)>0.01f) throw new Exception("Steer axle assignment failed.");
            if(Time.frameCount-frame<20) throw new Exception("Frames did not advance.");
            return "PASS grounded="+contacts+" acceleration="+speed.ToString("F2")+"km/h travel="+travel.ToString("F2")+"m braked="+stopped.ToString("F2")+"km/h steering=front axle frames="+(Time.frameCount-frame);
        }
        finally
        {
            car.testThrottle=0;car.testSteer=0;car.testBrake=false;car.testControl=false;
            body.position=start;body.rotation=original;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        }
    }
    static async Task Wait(float seconds)
    {float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<seconds) await Task.Delay(20);}
}
