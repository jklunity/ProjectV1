using UnityEngine;
#if UNITY_EDITOR
using uk.vroad.api.enums;
using UnityEditor;
#endif

namespace uk.vroad.ucm
{
    public class DesktopOnlyAttribute : PropertyAttribute
    {
 
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(DesktopOnlyAttribute))]
    public class DesktopOnlyDrawer: PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool isDesktop = BuildFlag.GenerateForMobile.IsFalse();
            GUI.enabled = isDesktop;
            
            if (isDesktop) EditorGUI.PropertyField(position, property, label, true);
            else           EditorGUI.LabelField(position, new GUIContent(SA.DESKTOP_PREFIX +label.text, label.tooltip));

            GUI.enabled = true;
        }
    }
#endif
}