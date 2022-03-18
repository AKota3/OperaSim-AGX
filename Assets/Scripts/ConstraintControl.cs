using System;
using UnityEngine;
using AGXUnity;
using AGXUnity.Utils;

namespace PWRISimulator
{
    /// <summary>
    /// ���ȒP�ȃC���^�t�F�[�X��Constraint�𐧌䂵����A�͂Ȃǂ�����������ł���悤��Consraint�v���N�V�N���X�B
    /// </summary>
    [Serializable]
    public class ConstraintControl
    {
        #region Public

        /// <summary>
        /// �Ώۂ�Constraint�B
        /// </summary>
        [InspectorLabel("Target Constraint")]
        public Constraint constraint;

        /// <summary>
        /// �R���X�g���C���g�𐧌䂷�邩�Btrue�̏ꍇ�́AcontrolType�ɂ��R���X�g���C���g��TargetSpeedController��
        /// LockController�𐧌䂷��Bfalse�̏ꍇ�́A�Ώۂ�Constraint�̐ݒ��G��Ȃ��BPlay���Ă���ԂɕύX���邱�Ƃ��ł��Ȃ��B
        /// </summary>
        public bool controlEnabled = false;

        /// <summary>
        /// controlValue�A�܂萧��̎w�ߒl�A�̎�ށB������������ɕύX���邱�Ƃ��ł��Ȃ��B
        /// </summary>
        /// <seealso cref="ControlType"/>
        [ConditionalHide("controlEnabled", true)]
        public ControlType controlType = ControlType.Speed;

        /// <summary>
        /// Constraint����̎w�ߒl�BcontrolType�ɂ���Ĉʒu�E�p�x���A���x�E�p���x���A�́E�g���N�B
        /// </summary>
        [ConditionalHide("controlEnabled", true)]
        public double controlValue = 0.0f;

        /// <summary>
        /// Constraint�̐�����@��RigidBody�ɂ�������ő�̗́^�g���N�B
        /// </summary>
        [ConditionalHide("controlEnabled", true)]
        public double controlMaxForce = double.PositiveInfinity;

        public double currentPosition
        {
            get { return nativeConstraint != null ? nativeConstraint.getAngle() : 0.0; }
        }
        public double currentSpeed
        {
            get { return nativeConstraint != null ? nativeConstraint.getCurrentSpeed() : 0.0; }
        }
        public double currentForce
        {
            get { return (lockController != null ? lockController.getCurrentForce() : 0.0) +
                         (targetSpeedController != null ? targetSpeedController.getCurrentForce() : 0.0); }
        }

        /// <summary>
        /// ���ۂ�AGXUnity�̃R���X�g���C���g�𐧌���@�ɂ���ď�������B
        /// </summary>
        public void Initialize()
        {
            if (constraint?.GetInitialized<Constraint>() != null)
            {
                nativeConstraint = agx.Constraint1DOF.safeCast(constraint.Native);

                lockController = agx.LockController.safeCast(
                    constraint.GetController<LockController>()?.Native);

                targetSpeedController = agx.TargetSpeedController.safeCast(
                    constraint.GetController<TargetSpeedController>()?.Native);

                if (controlEnabled)
                {
                    UpdateControlType();
                    UpdateMaxForce();
                    UpdateControlValue();
                }
            }
        }

        /// <summary>
        /// controlValue�����ۂ�AGXUnity�̃R���X�g���C���g�ɐݒ肷��B
        /// </summary>
        public void UpdateConstraintControl()
        {
            if (!controlEnabled)
                return;

            if (controlType != controlTypePrev)
                UpdateControlType();

            if (controlMaxForce != controlMaxForcePrev)
                UpdateMaxForce();

            if (controlValue != controlValuePrev)
                UpdateControlValue();
        }

        #endregion

        #region Private

        ControlType? controlTypePrev = null;
        double? controlValuePrev = null;�@// controlValue���ς���������m���邽�߂̒l�B
        double? controlMaxForcePrev = null;

        agx.Constraint1DOF nativeConstraint;
        agx.LockController lockController;
        agx.TargetSpeedController targetSpeedController;
        agx.ElementaryConstraint activeController;

        /// <summary>
        /// controlType�ɂ���āAlockController��targetSpeedController��Enable
        /// </summary>
        void UpdateControlType()
        {
            activeController = controlType == ControlType.Position ?
                (agx.ElementaryConstraint) lockController : targetSpeedController;

            if (lockController != null)
                lockController.setEnable(activeController == lockController);

            if (targetSpeedController != null)
                targetSpeedController.setEnable(activeController == targetSpeedController);

            controlTypePrev = controlType;
            controlValuePrev = null;
            controlMaxForcePrev = null;
        }

        void UpdateMaxForce()
        {
            if(activeController != null && controlType != ControlType.Force)
                activeController.setForceRange(new agx.RangeReal(controlMaxForce));

            controlMaxForcePrev = controlMaxForce;
        }

        void UpdateControlValue()
        {
            switch (controlType)
            {
                case ControlType.Position:
                    if (lockController != null)
                        lockController.setPosition(controlValue);
                    break;
                case ControlType.Speed:
                    if (targetSpeedController != null)
                        targetSpeedController.setSpeed(controlValue);
                    break;
                case ControlType.Force:
                    if (targetSpeedController != null)
                    {
                        double dir = controlValue > 0.0 ? 1.0 : (controlValue < 0.0 ? -1.0 : 0.0);
                        targetSpeedController.setSpeed(dir * float.PositiveInfinity);
                        targetSpeedController.setForceRange(controlValue, controlValue);
                    }
                    break;
            }
            controlValuePrev = controlValue;
        }

        #endregion
    }
}