using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateTruckAudio
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode.");
        Application.runInBackground=true;
        var truck=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var audio=truck.GetComponent<VehicleAudio>();
        if(audio==null || audio.profile==null)throw new Exception("Missing audio configuration.");
        var clips=new[]{audio.profile.idle,audio.profile.load,audio.profile.road,audio.profile.slip,audio.profile.chains,audio.profile.brakeAir,audio.profile.gear,audio.profile.chainFitting,audio.profile.horn};
        float minimumRms=float.MaxValue;
        foreach(var clip in clips)
        {
            if(clip==null || clip.samples==0)throw new Exception("Missing clip.");
            clip.LoadAudioData();var data=new float[clip.samples*clip.channels];if(!clip.GetData(data,0))throw new Exception("Cannot decode "+clip.name);
            double sum=0;foreach(float value in data){if(float.IsNaN(value)||Mathf.Abs(value)>=0.999f)throw new Exception("Invalid/clipped PCM "+clip.name);sum+=value*value;}
            float rms=Mathf.Sqrt((float)(sum/data.Length));if(rms<0.002f)throw new Exception("Silent clip: "+clip.name);minimumRms=Mathf.Min(minimumRms,rms);
        }
        var body=truck.GetComponent<Rigidbody>();var pos=body.position;var rotation=body.rotation;
        bool previousTest=truck.testControl,previousBrake=truck.testBrake,previousLow=truck.lowRange;float previousThrottle=truck.testThrottle;
        try
        {
            truck.testControl=true;truck.testBrake=true;truck.testThrottle=0;truck.lowRange=false;
            truck.SendMessage("Update");truck.SendMessage("FixedUpdate");audio.UpdateAudio(1);
            float idle=audio.EngineRpm,idlePitch=audio.load.pitch;
            if(!audio.idle.isPlaying || !audio.load.isPlaying || audio.idle.volume<0.01f)throw new Exception("Engine loops not playing.");
            truck.testThrottle=1;truck.testBrake=false;truck.SendMessage("Update");truck.SendMessage("FixedUpdate");audio.UpdateAudio(1);
            float revved=audio.EngineRpm;
            if(revved<idle+300 || audio.load.pitch<idlePitch+0.2f)throw new Exception("Throttle did not change engine RPM/pitch.");
            truck.testThrottle=0;truck.testBrake=true;truck.SendMessage("Update");truck.SendMessage("FixedUpdate");audio.UpdateAudio(0.1f);
            int air=audio.AirReleaseCount;truck.testBrake=false;truck.SendMessage("Update");truck.SendMessage("FixedUpdate");audio.UpdateAudio(1);
            if(audio.AirReleaseCount!=air+1)throw new Exception("Pneumatic release missing.");
            int gear=audio.GearSoundCount;truck.lowRange=true;audio.UpdateAudio(0.1f);audio.UpdateAudio(0.1f);
            if(audio.GearSoundCount!=gear+1)throw new Exception("Duplicate/missing gear event.");
            audio.testHorn=true;audio.UpdateAudio(0.2f);if(audio.horn.volume<0.1f || !audio.horn.isPlaying)throw new Exception("Horn did not start.");
            audio.testHorn=false;audio.UpdateAudio(1);if(audio.horn.volume>0.001f)throw new Exception("Horn did not fade out.");
            var chainSystem=truck.GetComponent<TruckChains>();if(!chainSystem.Installed && audio.chains.volume>0.001f)throw new Exception("Chains audible without installation.");
            truck.testBrake=true;truck.SendMessage("Update");truck.SendMessage("FixedUpdate");
            var output=new float[1024];audio.idle.GetOutputData(output,0);
            double dspStart=AudioSettings.dspTime;await Task.Delay(700);
            audio.idle.GetOutputData(output,0);double energy=0;foreach(float value in output)energy+=value*value;
            float outputRms=Mathf.Sqrt((float)(energy/output.Length));
            if(AudioSettings.dspTime<=dspStart)throw new Exception("Audio DSP clock did not advance.");
            if(outputRms<0.00001f)throw new Exception("No decoded engine output; check Editor mute/audio output.");
            return "PASS 9 decoded non-clipping WAV clips, minimum clip RMS="+minimumRms.ToString("F3")+"; engine "+idle.ToString("F0")+"->"+revved.ToString("F0")+"rpm; engine loops playing, output RMS="+outputRms.ToString("F4")+"; brake release, single gear event, horn fade and no-chain silence passed";
        }
        finally
        {
            audio.testHorn=false;truck.testControl=previousTest;truck.testBrake=previousBrake;truck.testThrottle=previousThrottle;truck.lowRange=previousLow;
            body.position=pos;body.rotation=rotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        }
    }
}
