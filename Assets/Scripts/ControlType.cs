using System;
using UnityEngine;

namespace PWRISimulator
{
    /// <summary>
    /// ����̎w�ߒl�̎�ށB
    /// </summary>
    [Serializable]
    public enum ControlType
    {
        /// <summary>
        /// �ʒu�^�p�x��Constraint�𐧌䂷��BAGXUnity��LockController�𗘗p�B
        /// </summary>
        Position,

        /// <summary>
        /// ���x�^�����x��Constraint�𐧌䂷��BAGXUnity��TargetSpeedController�𗘗p�B
        /// </summary>
        Speed,

        /// <summary>
        /// �́^�g���N��Constraint�𐧌䂷��BAGXUnity��TargetSpeedController�𗘗p(���x�𖳌��ɐݒ肵�AForceRange�𐧌�)�B
        /// </summary>
        Force
    };
}
