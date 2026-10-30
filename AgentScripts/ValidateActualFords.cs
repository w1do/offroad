using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateActualFords
{
    static void Place(Rigidbody body,Vector3 position,Quaternion rotation)
    {body.transform.SetPositionAndRotation(position,rotation);body.position=position;body.rotation=rotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play.");
        var rivers=UnityEngine.Object.FindObjectsByType<RiverCurrent>().OrderBy(r=>r.name).ToArray();var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var rb=car.GetComponent<Rigidbody>();
        var original=rb.position;var rotation=rb.rotation;var oldMode=Physics.simulationMode;bool paused=EditorApplication.isPaused;Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;
        string result="";
        try
        {
            car.testControl=true;car.testThrottle=0;car.testBrake=true;car.testSteer=0;car.lowRange=true;car.reverseGear=false;
            foreach(var river in rivers)
            {
                var entrance=river.transform.TransformPoint(new Vector3(-1,0,-river.halfWidth-7));
                var hits=Physics.RaycastAll(entrance+Vector3.up*10,Vector3.down,30,~0,QueryTriggerInteraction.Ignore).Where(h=>h.rigidbody==null).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0)throw new Exception("No entry ground for "+river.name);
                entrance.y=hits[0].point.y+0.35f;Place(rb,entrance,river.transform.rotation);
                for(int i=0;i<100;i++)Step(car,river);
                bool completed=false;float maxWet=0,maxSide=0;Vector3 local=Vector3.zero;
                for(int i=0;i<6000;i++)
                {
                    local=river.transform.InverseTransformPoint(rb.position);maxSide=Mathf.Max(maxSide,Mathf.Abs(local.x));maxWet=Mathf.Max(maxWet,river.LastSubmersion);
                    if(local.z>river.halfWidth+6){completed=true;break;}
                    var target=river.transform.TransformPoint(new Vector3(-1.4f,0,local.z+5));target.y=rb.position.y;
                    var goal=car.transform.InverseTransformPoint(target);car.testSteer=Mathf.Clamp(Mathf.Atan2(goal.x,Mathf.Max(1,goal.z))*Mathf.Rad2Deg/24f,-1,1);
                    car.testBrake=false;car.testThrottle=car.SpeedKmh<13?0.8f:0;Step(car,river);
                    if(rb.position.y<river.SurfaceHeight(local.x)-2.2f)throw new Exception("Fell through ford "+river.name);
                }
                if(!completed||maxWet<0.1f)
                {
                    string contacts="";foreach(var wheel in car.wheels)if(wheel.GetGroundHit(out var hit))contacts+=" | "+hit.collider.name+" p="+hit.point+" grip="+wheel.forwardFriction.stiffness;
                    SaveView(car);
                    throw new Exception("Ford traversal failed: "+river.name+" local="+local+" speed="+car.SpeedKmh+" maxWet="+maxWet+" side="+maxSide+" slip="+car.DrivenSlip+contacts);
                }
                result+=river.name+": crossed with wet fraction="+maxWet.ToString("F2")+", max side="+maxSide.ToString("F2")+"m; ";
                car.testThrottle=0;car.testBrake=true;car.testSteer=0;
            }
            return "PASS actual KAMAZ crossings: "+result;
        }
        finally{car.testControl=false;car.testThrottle=0;car.testBrake=false;car.testSteer=0;car.lowRange=false;Place(rb,original,rotation);Physics.simulationMode=oldMode;EditorApplication.isPaused=paused;}
    }
    static void Step(ForestKamazController car,RiverCurrent river)
    {car.SendMessage("Update");car.SendMessage("FixedUpdate");river.ApplyForces(car.GetComponent<Rigidbody>());Physics.Simulate(Time.fixedDeltaTime);car.SendMessage("LateUpdate");}
    static void SaveView(ForestKamazController car)
    {
        var camera=Camera.main;var pos=camera.transform.position;var rot=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1024,700,24);camera.transform.position=car.transform.position+car.transform.forward*5+car.transform.right*6+Vector3.up*3;camera.transform.LookAt(car.transform.position+Vector3.up);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1024,700,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1024,700),0,0);tex.Apply();System.IO.Directory.CreateDirectory("work");System.IO.File.WriteAllBytes("work/ford-diagnostic.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);camera.transform.SetPositionAndRotation(pos,rot);
    }
}
