using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateDriving
{
    public static async Task<string> Main()
    {
        Application.runInBackground = true;
        var car = UnityEngine.Object.FindAnyObjectByType<MountainSUV>();
        var body = car.GetComponent<Rigidbody>();
        int firstFrame = Time.frameCount;
        var results = new List<string>();
        Vector3 original = car.transform.position;
        Quaternion rotation = car.transform.rotation;
        try
        {
            car.testControl = true;
            foreach (int index in new[] { 65, 420, 765 })
            {
                car.testThrottle = 0; car.testSteer = 0; car.testBrake = true;
                body.position = car.route.points[index] + Vector3.up * .4f;
                body.rotation = Quaternion.LookRotation(car.route.points[index + 2] - car.route.points[index]);
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                await Wait(1.2f);
                string surface = car.surface.ToString();
                int grounded = car.groundedWheels;
                float grip = car.wheels[0].sidewaysFriction.stiffness;
                Vector3 start = body.position;
                car.testBrake = false; car.testThrottle = 1;
                await Wait(3.0f);
                float accelerated = car.SpeedKmh;
                float travel = Vector3.Distance(start, body.position);
                car.testThrottle = 0; car.testBrake = true;
                await Wait(2.2f);
                float braked = car.SpeedKmh;
                if (grounded < 3 || accelerated < 3 || travel < 2)
                    throw new Exception("Driving failed on " + surface + ": grounded=" + grounded + ", speed=" + accelerated + ", travel=" + travel);
                if (braked > accelerated + 2) throw new Exception("Braking failed on " + surface);
                results.Add(surface + ": wheels=" + grounded + ", lateral grip=" + grip.ToString("F2") + ", acceleration=" + accelerated.ToString("F1") + " km/h, travel=" + travel.ToString("F1") + "m, after brake=" + braked.ToString("F1") + " km/h");
            }
            car.testBrake = true;car.testThrottle=0;
            body.position=car.route.points[50]+Vector3.up*.4f;
            body.rotation=Quaternion.LookRotation(car.route.points[52]-car.route.points[50]);
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            await Wait(1);
            car.testBrake=false;car.testThrottle=-1;
            await Wait(1.8f);
            float reverse=Vector3.Dot(body.linearVelocity,car.transform.forward);
            if(reverse>-.5f)throw new Exception("Reverse failed: "+reverse);
            results.Add("Reverse: "+reverse.ToString("F2")+" m/s");
            car.testBrake=true;car.testThrottle=0;
            body.position=car.route.points[65]+Vector3.up*.4f;
            body.rotation=Quaternion.LookRotation(car.route.points[67]-car.route.points[65]);
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            await Wait(1);
            Quaternion heading=body.rotation;
            car.testBrake=false;car.testThrottle=1;car.testSteer=.35f;
            await Wait(2);
            float turned=Quaternion.Angle(heading,body.rotation);
            if(turned<2)throw new Exception("Steering failed: "+turned);
            results.Add("Steering: "+turned.ToString("F1")+" degrees");
            car.testSteer=0;car.testThrottle=0;car.testBrake=true;
            car.ResetToRoad();await Wait(.8f);
            if(car.groundedWheels<3)throw new Exception("Recovery failed.");
            results.Add("Recovery: grounded on road");
            if(Time.frameCount-firstFrame<150)throw new Exception("Game frames did not advance sufficiently.");
            results.Add("Frames advanced: "+(Time.frameCount-firstFrame));
            return string.Join("\n",results);
        }
        finally
        {
            car.testThrottle=0;car.testSteer=0;car.testBrake=false;car.testControl=false;
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            body.position=original;body.rotation=rotation;
        }
    }

    private static async Task Wait(float duration)
    {
        float start=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-start<duration)await Task.Delay(20);
    }
}
