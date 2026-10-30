using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class PolishForestFords
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var wood=new Material(Shader.Find("Universal Render Pipeline/Lit"));wood.name="Ford warning board";wood.SetColor("_BaseColor",new Color(0.12f,0.095f,0.065f));wood.SetFloat("_Smoothness",0.12f);AssetDatabase.CreateAsset(wood,"Assets/Game/Generated/LivingForest/FordBoard.mat");int removed=0;
        foreach(var river in UnityEngine.Object.FindObjectsByType<RiverCurrent>())
        {
            river.transform.Find("Gravel ford bed - physical").GetComponent<ForestDrivingSurface>().forwardGrip=0.7f;
            foreach(var t in river.GetComponentsInChildren<Transform>().Where(t=>t.name=="Riverbank stone").ToArray())if(Mathf.Abs(t.localPosition.x)<5.5f){UnityEngine.Object.DestroyImmediate(t.gameObject);removed++;}
            var text=river.GetComponentInChildren<TextMesh>();float width=text.GetComponent<Renderer>().localBounds.size.x;
            text.transform.localScale=Vector3.one*(2.65f/Mathf.Max(0.1f,width));
            text.color=new Color(0.96f,0.87f,0.62f);
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.name="Timber ford warning board";board.transform.SetParent(river.transform,false);board.transform.SetPositionAndRotation(text.transform.position+text.transform.forward*0.045f,text.transform.rotation);board.transform.localScale=new Vector3(3.0f,1.45f,0.08f);board.GetComponent<Renderer>().sharedMaterial=wood;
            for(int i=0;i<2;i++)
            {
                var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);post.name="Warning sign post";post.transform.SetParent(river.transform,false);
                post.transform.position=text.transform.position+text.transform.right*(i==0?-1.15f:1.15f)-Vector3.up*0.75f;post.transform.localScale=new Vector3(0.10f,1.35f,0.10f);post.GetComponent<Renderer>().sharedMaterial=wood;
            }
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return "Cleared ford lanes: removed "+removed+" bank rocks; built timber warning boards; persisted wet-gravel traction.";
    }
}
