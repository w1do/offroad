using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VehiclePhysicsProfile))]
public sealed class VehiclePhysicsProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var profile=(VehiclePhysicsProfile)target;
        if(GUILayout.Button("Создать отдельный профиль оснащения")){
            string path=EditorUtility.SaveFilePanelInProject("Оснащение машины",profile.name+" equipment","asset","Клавиши, привод, цепи и лебёдка этой машины");
            if(!string.IsNullOrEmpty(path)){Undo.RecordObject(profile,"Assign equipment profile");var equipment=profile.equipment==null?CreateInstance<VehicleEquipmentProfile>():Instantiate(profile.equipment);AssetDatabase.CreateAsset(equipment,path);profile.equipment=equipment;EditorUtility.SetDirty(profile);}
        }
        if(profile.equipment==null)return;
        EditorGUILayout.Space();EditorGUILayout.LabelField("Оснащение: управление, привод, цепи, лебёдка",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Настройки хранятся в назначенном Equipment asset. Для другой машины создайте отдельную копию. Primary Drive отмечает оси, работающие при выключенном полном приводе.",MessageType.Info);
        var equipmentSettings=new SerializedObject(profile.equipment);equipmentSettings.Update();DrawPropertiesExcluding(equipmentSettings,"m_Script");equipmentSettings.ApplyModifiedProperties();
    }
}
