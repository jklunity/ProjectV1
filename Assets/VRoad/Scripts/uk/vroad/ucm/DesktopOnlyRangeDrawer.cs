using uk.vroad.pac;
using UnityEngine;

#if UNITY_EDITOR
using uk.vroad.api.enums;
using UnityEditor;
#endif

namespace uk.vroad.ucm
{
    public class DesktopOnlyRangeAttribute : PropertyAttribute
    {
        public float min;
        public float max;
        public bool forPro;
        public DesktopOnlyRangeAttribute(float _min, float _max, bool _pro)
        {
            this.min = _min;
            this.max = _max;
            this.forPro = _pro;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(DesktopOnlyRangeAttribute))]
    public class DesktopOnlyRangeDrawer : PropertyDrawer
    {
        private static readonly bool gotPro = VRoad.GotPro();
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Get the attribute which contains the range for the slider
            DesktopOnlyRangeAttribute range = attribute as DesktopOnlyRangeAttribute;

            bool isDesktop = BuildFlag.GenerateForMobile.IsFalse();
            
            if (isDesktop && (!range.forPro || gotPro))
            {
                // Now draw the property as a Slider or an IntSlider based on whether it's a float or integer.
                if (property.propertyType == SerializedPropertyType.Float)
                    EditorGUI.Slider(position, property, range.min, range.max, label);
                else if (property.propertyType == SerializedPropertyType.Integer)
                    EditorGUI.IntSlider(position, property, (int)range.min, (int)range.max, label);
                else
                    EditorGUI.LabelField(position, label.text, "Use Range with float or int.");
                //  EditorGUI.PropertyField(position, property, label, true);
            }
            else
            {
                GUI.enabled = false;
                string prefix = range.forPro ? SA.LITE_PREFIX : SA.DESKTOP_PREFIX;
                EditorGUI.LabelField(position, new GUIContent(prefix+label.text, label.tooltip));
                GUI.enabled = true;
            }
            
            
        }
    }
#endif
}