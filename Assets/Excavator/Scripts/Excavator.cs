using System;
using UnityEngine;
using UnityEngine.InputSystem;
using AGXUnity;
using AGXUnity.Model;
using AGXUnity.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PWRISimulator
{
    /// <summary>
    /// �ȒP�ȋ��ʂ̃C���^�t�F�[�X�Ŗ����V���x���̃R���X�g���C���g�𐧌䂵����A���������肷�邱�Ƃł���悤�ɂ���N���X�B
    /// </summary>
    [RequireComponent(typeof(ExcavationData))]
    public class Excavator : ConstructionMachine
    {
        [Header("Constraint Controls")]
        public ConstraintControl leftSprocket;
        public ConstraintControl rightSprocket;
        public ConstraintControl swing;
        public ConstraintControl boomTilt;
        public ConstraintControl armTilt;
        public ConstraintControl bucketTilt;

        public ExcavationData excavationData { get; private set; }

        protected override bool Initialize()
        {
            bool success = base.Initialize();

            excavationData = GetComponentInChildren<ExcavationData>();

            RegisterConstraintControl(leftSprocket);
            RegisterConstraintControl(rightSprocket);
            RegisterConstraintControl(swing);
            RegisterConstraintControl(boomTilt);
            RegisterConstraintControl(armTilt);
            RegisterConstraintControl(bucketTilt);
            
            return success;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Excavator))]
    public class ExcavatorEditor : ConstructionMachineEditor
    {
        public override void OnInspectorGUI()
        {
            // ConstructionMachineEditor��GUI��\��
            base.OnInspectorGUI();
        }
    }
#endif
}