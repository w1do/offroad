using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(VehiclePhysics))]
public sealed class VehiclePhysicsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var vehicle=(VehiclePhysics)target;
        EditorGUILayout.HelpBox("Профиль хранит настройки конкретной машины. Axles связывает оси профиля с коллайдерами и визуальными ступицами этого prefab. Один класс работает с любым числом пар колёс.",MessageType.Info);
        if(GUILayout.Button("Создать отдельный профиль этой машины"))
        {
            string path=EditorUtility.SaveFilePanelInProject("Профиль машины",vehicle.name+" physics","asset","Выберите файл настроек машины");
            if(!string.IsNullOrEmpty(path)){Undo.RecordObject(vehicle,"Assign vehicle profile");var profile=vehicle.profile!=null?Instantiate(vehicle.profile):CreateInstance<VehiclePhysicsProfile>();profile.displayName=vehicle.name;AssetDatabase.CreateAsset(profile,path);vehicle.profile=profile;EditorUtility.SetDirty(vehicle);Selection.activeObject=profile;}
        }
        if(vehicle.profile==null)return;
        if(vehicle.profile.lighting!=null&&GUILayout.Button("Подключить общую систему фар")){
            if(vehicle.GetComponent<VehicleLighting>()==null)Undo.AddComponent<VehicleLighting>(vehicle.gameObject);
            EditorUtility.SetDirty(vehicle);EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);
        }
        if(vehicle.profile.equipment!=null&&GUILayout.Button("Подключить привод, цепи и лебёдку из профиля")){
            if(vehicle.GetComponent<TruckChains>()==null)Undo.AddComponent<TruckChains>(vehicle.gameObject);
            if(vehicle.GetComponent<VehicleWinch>()==null)Undo.AddComponent<VehicleWinch>(vehicle.gameObject);
            if(vehicle.GetComponent<VehicleTyrePressure>()==null)Undo.AddComponent<VehicleTyrePressure>(vehicle.gameObject);
            if(vehicle.GetComponent<VehiclePlayerInput>()==null)Undo.AddComponent<VehiclePlayerInput>(vehicle.gameObject);
            EditorUtility.SetDirty(vehicle);EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);
        }
        if(GUILayout.Button("Создать коллайдер кузова по модели")&&vehicle.GetComponent<Collider>()==null)
        {
            Bounds bounds=new Bounds();bool first=true;
            foreach(var renderer in vehicle.GetComponentsInChildren<Renderer>())
            {
                if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
                bool wheel=false;if(vehicle.axles!=null)foreach(var axle in vehicle.axles)if(axle!=null&&(axle.leftVisual!=null&&renderer.transform.IsChildOf(axle.leftVisual)||axle.rightVisual!=null&&renderer.transform.IsChildOf(axle.rightVisual)))wheel=true;if(wheel)continue;
                var b=renderer.localBounds;for(int i=0;i<8;i++){var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));p=vehicle.transform.InverseTransformPoint(renderer.transform.TransformPoint(p));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            if(!first){var collider=Undo.AddComponent<BoxCollider>(vehicle.gameObject);float bottom=Mathf.Max(bounds.min.y,.3f);bounds.SetMinMax(new Vector3(bounds.min.x+.05f,bottom,bounds.min.z+.05f),bounds.max-new Vector3(.05f,0,.05f));collider.center=bounds.center;collider.size=bounds.size;EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);}
        }
        if(GUILayout.Button("Подготовить оси по профилю"))
        {
            Undo.RecordObject(vehicle,"Create vehicle axle bindings");int count=vehicle.profile.axles.Length;var bindings=new VehicleAxleBinding[count];for(int i=0;i<count;i++)bindings[i]=vehicle.axles!=null&&i<vehicle.axles.Length?vehicle.axles[i]:new VehicleAxleBinding();vehicle.axles=bindings;EditorUtility.SetDirty(vehicle);
        }
        if(GUILayout.Button("Создать недостающие коллайдеры по ступицам"))
        {
            Undo.RecordObject(vehicle,"Create wheel colliders");if(vehicle.axles!=null)for(int i=0;i<vehicle.axles.Length;i++){var axle=vehicle.axles[i];if(axle==null)continue;if(axle.left==null&&axle.leftVisual!=null)axle.left=Wheel(vehicle.transform,axle.leftVisual,"Left wheel "+i);if(axle.right==null&&axle.rightVisual!=null)axle.right=Wheel(vehicle.transform,axle.rightVisual,"Right wheel "+i);}EditorUtility.SetDirty(vehicle);
        }
        if(GUILayout.Button("Применить профиль к кузову и подвеске"))
        {
            Undo.RecordObject(vehicle.GetComponent<Rigidbody>(),"Apply chassis profile");foreach(var w in vehicle.GetComponentsInChildren<WheelCollider>())Undo.RecordObject(w,"Apply axle profile");vehicle.Initialize();EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);
        }
    }
    static WheelCollider Wheel(Transform root,Transform visual,string name){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create wheel collider");go.transform.SetParent(root,false);go.transform.position=visual.position;return go.AddComponent<WheelCollider>();}
}
