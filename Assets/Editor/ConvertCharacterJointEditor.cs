using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConvertCharacterJoint))]
public class ConvertCharacterJointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ConvertCharacterJoint script = (ConvertCharacterJoint)target;
        if (GUILayout.Button("Convert CharacterJoint to ConfigurableJoint"))
        {
            script.Convert();
        }

        if (GUILayout.Button("Convert All Child Joints"))
        {
            int i = 0;
            foreach (var cj in script.GetComponentsInChildren<CharacterJoint>())
            {
                i++;
                var converter = cj.gameObject.GetComponent<ConvertCharacterJoint>();
                if (converter == null)
                    converter = cj.gameObject.AddComponent<ConvertCharacterJoint>();
                converter.Convert();

            }
            Debug.Log($"{i} joints found");
        }
        if (GUILayout.Button("Remove converter scrippts"))
        {
            int i = 0;
            int j = 0;
            foreach (var cj in script.GetComponentsInChildren<ConfigurableJoint>())
            {

                var converter = cj.gameObject.GetComponent<ConvertCharacterJoint>();
                if (converter == null)
                    j++;
                else
                    i++;
                DestroyImmediate(converter);

            }
            Debug.Log($"{i} converter scripts removed, {j} config joints found with no converter scripts attached");
        }
    }
}

