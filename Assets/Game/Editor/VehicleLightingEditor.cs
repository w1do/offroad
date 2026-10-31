using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(VehicleLighting))]
public sealed class VehicleLightingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var lighting = (VehicleLighting)target;
        EditorGUILayout.HelpBox("Каждая запись Lamps задаёт тип фары, Spot Light и необязательный отдельный рассеиватель. Количество произвольное. Позиции и наклоны задаются Transform ламп на этой модели; настройки света и клавиши — Lighting в VehiclePhysicsProfile.", MessageType.Info);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Состояние", lighting.Mode + " / " + (lighting.HeadlightsOn ? lighting.HighBeamOn ? "Дальний" : "Ближний" : "Выключено"));
            var vehicle = lighting.GetComponent<VehiclePhysics>();
            if (GUILayout.Button("Сменить АВТО / ВКЛ / ВЫКЛ")) vehicle.CycleHeadlightMode();
            if (GUILayout.Button("Ближний")) vehicle.SetLowBeam(!vehicle.LowBeamOn);
            if (GUILayout.Button("Дальний")) vehicle.SetHighBeam(!vehicle.HighBeamOn);
            if (GUILayout.Button("Противотуманки")) vehicle.SetFogLamps(!lighting.FogLampsRequested);
        }
        else if ((lighting.lamps == null || lighting.lamps.Length == 0) && GUILayout.Button("Создать шаблон фар по коллайдеру кузова"))
        {
            var box = lighting.GetComponent<BoxCollider>();
            if (box == null) { EditorGUILayout.HelpBox("Задайте Lamps вручную или добавьте BoxCollider кузова.", MessageType.Warning); return; }
            float halfWidth = box.size.x * 0.5f;
            float front = box.center.z + box.size.z * 0.5f + 0.06f;
            float height = box.center.y;
            Undo.RecordObject(lighting, "Create headlight template");
            lighting.lamps = new VehicleLampBinding[6];
            int index = 0;
            foreach (VehicleLampKind kind in System.Enum.GetValues(typeof(VehicleLampKind)))
                foreach (int side in new[] { -1, 1 })
                {
                    var node = new GameObject(kind + (side < 0 ? " left" : " right"));
                    Undo.RegisterCreatedObjectUndo(node, "Create headlight");
                    node.transform.SetParent(lighting.transform, false);
                    node.transform.localPosition = new Vector3(side * halfWidth * (kind == VehicleLampKind.HighBeam ? 0.55f : 0.8f), height - (kind == VehicleLampKind.Fog ? 0.35f : 0), front);
                    node.transform.localRotation = Quaternion.Euler(kind == VehicleLampKind.HighBeam ? 1f : kind == VehicleLampKind.Fog ? 8f : 4f, 0, 0);
                    var light = Undo.AddComponent<Light>(node);
                    light.type = LightType.Spot;
                    light.enabled = false;
                    lighting.lamps[index++] = new VehicleLampBinding { kind = kind, light = light };
                }
            EditorUtility.SetDirty(lighting);
            EditorSceneManager.MarkSceneDirty(lighting.gameObject.scene);
        }
    }
}
