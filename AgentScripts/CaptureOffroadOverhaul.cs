using System.IO;
using System.Linq;
using UnityEngine;

public static class CaptureOffroadOverhaul
{
    public static string Main()
    {
        var sections=Object.FindObjectsByType<MudRoadSection>().Where(s=>s.GetComponent<SoftGroundRuts>().persistentId.StartsWith("main")).OrderBy(s=>s.GetComponent<SoftGroundRuts>().persistentId).ToArray();
        var camera=Camera.main;var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;var target=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1400,900,24);
        try
        {
            for(int i=0;i<sections.Length;i++)
            {
                var section=sections[i];var centre=section.GetComponent<Renderer>().bounds.center;
                camera.transform.position=centre+section.transform.right*5.5f-section.transform.forward*7.5f+Vector3.up*6;
                camera.transform.LookAt(centre);camera.fieldOfView=55;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(1400,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();
                File.WriteAllBytes("work/OffroadOverhaul"+i+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            return "Captured "+sections.Length+" natural mud sections";
        }
        finally{camera.transform.SetPositionAndRotation(pos,rot);camera.fieldOfView=fov;camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
    }
}
