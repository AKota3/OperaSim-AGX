using UnityEngine;
using UnityEngine.InputSystem;

namespace PWRISimulator
{
    /// <summary>
    /// Excavator�p��InputActionMap��Actions��Excavator.cs�̃R���X�g���C���g�C���^�t�F�[�X�ƌq���X�N���v�g�B
    /// 
    /// �eUpdate�Ɏw������InputActionMap��Action���Ƃ̒l���擾����A�Ȃ̂ŁAUnity��PlayerInput�R���|�l���g�͕s�v�B
    /// </summary>
    public class ExcavatorPlayerInputHandler2 : MonoBehaviour
    {
        [Header("Target Machine")]

        /// <summary>
        /// �Ώۂ�Excvator�R���|�l���g�B
        /// </summary>
        public Excavator excavator;

        [Header("Input Action Map")]
        public InputActionAsset inputActionAsset;
        public string inputMapName = "Excavator";

        [Header("Input Action Names")]
        public string leftSprocketActionName = "LeftSprocket";
        public string rightSprocketActionName = "RightSprocket";
        public string swingActionName = "Swing";
        public string boomTiltActionName = "BoomTilt";
        public string armTiltActionName = "ArmTilt";
        public string bucketTiltActionName = "BucketTilt";

        InputActionMap excavatorInputMap = null;
        InputAction leftSprocketAction = null;
        InputAction rightSprocketAction = null;
        InputAction swingAction = null;
        InputAction boomTiltAction = null;
        InputAction armTiltAction = null;
        InputAction bucketTiltAction = null;

        public void Start()
        {
            excavatorInputMap = inputActionAsset?.FindActionMap(inputMapName);
            if (excavatorInputMap != null)
            {
                excavatorInputMap.Enable();
                leftSprocketAction = excavatorInputMap.FindAction(leftSprocketActionName);
                rightSprocketAction = excavatorInputMap.FindAction(rightSprocketActionName);
                swingAction = excavatorInputMap.FindAction(swingActionName);
                boomTiltAction = excavatorInputMap.FindAction(boomTiltActionName);
                armTiltAction = excavatorInputMap.FindAction(armTiltActionName);
                bucketTiltAction = excavatorInputMap.FindAction(bucketTiltActionName);

                if (excavator != null)
                {
                    SetExcavatorConstraintVelocityControl(excavator.leftSprocket);
                    SetExcavatorConstraintVelocityControl(excavator.rightSprocket);
                    SetExcavatorConstraintVelocityControl(excavator.swing);
                    SetExcavatorConstraintVelocityControl(excavator.boomTilt);
                    SetExcavatorConstraintVelocityControl(excavator.armTilt);
                    SetExcavatorConstraintVelocityControl(excavator.bucketTilt);
                }
            }
        }

        public void Update()
        {
            if (excavator == null)
                return;

            if (leftSprocketAction != null)
                SetExcavatorInputValue(excavator.leftSprocket, leftSprocketAction.ReadValue<float>());

            if (rightSprocketAction != null)
                SetExcavatorInputValue(excavator.rightSprocket, rightSprocketAction.ReadValue<float>());

            if (swingAction != null)
                SetExcavatorInputValue(excavator.swing, swingAction.ReadValue<float>());

            if (boomTiltAction != null)
                SetExcavatorInputValue(excavator.boomTilt, boomTiltAction.ReadValue<float>());

            if (armTiltAction != null)
                SetExcavatorInputValue(excavator.armTilt, armTiltAction.ReadValue<float>());

            if (bucketTiltAction != null)
                SetExcavatorInputValue(excavator.bucketTilt, bucketTiltAction.ReadValue<float>());
        }

        protected void SetExcavatorInputValue(ConstraintControl constraintControl, double value)
        {
            if (constraintControl != null)
                constraintControl.controlValue = value;
        }

        protected void SetExcavatorConstraintVelocityControl(ConstraintControl constraintControl)
        {
            if (constraintControl != null)
                constraintControl.controlType = ControlType.Speed;
        }
    }
}