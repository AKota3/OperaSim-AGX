using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PWRISimulator
{
    /// <summary>
    /// ����bool�^��field�ɂ���āAInspector�Ƀv���p�e�B�𖳌����^�B��Attribute�B
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class |
     AttributeTargets.Struct, Inherited = true)]
    public class ConditionalHideAttribute : PropertyAttribute
    {
        // bool�v����field�̖��O�B
        public string conditionalSourceField = "";

        // �������������(false)�ɁA�B��(true�j���ǂ���bool�B 
        public bool hideCompletely = false;

        // bool�v����true�Ȃ̂ɁA�v���p�e�B���ҏW�����Ȃ��ăv���p�e�B��\�����邾���B
        public bool alwaysReadOnly = false;
        
        public ConditionalHideAttribute(string conditionalSourceField, bool hideCompletely = false,
            bool alwaysReadOnly = false)
        {
            this.conditionalSourceField = conditionalSourceField;
            this.hideCompletely = hideCompletely;
            this.alwaysReadOnly = alwaysReadOnly;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
    public class ConditionalHidePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
            bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

            bool wasEnabled = GUI.enabled;
            try
            {
                GUI.enabled = enabled && !condHAtt.alwaysReadOnly;
                if (!condHAtt.hideCompletely || enabled)
                {
                    EditorGUI.PropertyField(position, property, label, true);
                }
            }
            finally
            {
                GUI.enabled = wasEnabled;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
            bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

            if (!condHAtt.hideCompletely || enabled)
            {
                return EditorGUI.GetPropertyHeight(property, label);
            }
            else
            {
                return -EditorGUIUtility.standardVerticalSpacing;
            }
        }

        bool GetConditionalHideAttributeResult(ConditionalHideAttribute condHAtt, SerializedProperty property)
        {
            // �L�����^�������������v���p�e�B�̃p�X
            string propertyPath = property.propertyPath;
            // �L�����̏�����\���v���p�e�B�̃p�X
            string conditionPath = propertyPath.Replace(property.name, condHAtt.conditionalSourceField);
            // �L�����̏����̒l
            SerializedProperty sourcePropertyValue = property.serializedObject.FindProperty(conditionPath);

            if (sourcePropertyValue != null)
            {
                return sourcePropertyValue.boolValue;
            }
            else
            {
                Debug.LogWarning("Attempting to use a ConditionalHideAttribute but no matching SourcePropertyValue " +
                                 "found in object: " + condHAtt.conditionalSourceField);
                return true;
            }
        }
    }
#endif
}
