using System;
using System.Linq;
using UnityEngine;
using AGXUnity;
using AGXUnity.Model;

namespace PWRISimulator
{
    public static class TrackUtil
    {
        /// <summary>
        /// �w�������Q��Track�̂��ꂼ���sprocket�z�C�[�����擾���Aseparation�Ƃ����o�͂�sprocket�z�C�[�����m�̋����ɐݒ肵�A
        /// radius�Ƃ����o�͂�sprocket���a�{Track�����ɐݒ肵�ATrue��Ԃ��B���s�̏ꍇ�́Aseparation�Aradius���[���ɐݒ肵False
        /// ��Ԃ��B
        /// </summary>
        public static bool GetSeparationAndTractionRadius(Track trackLeft, Track trackRight, out double separation,
                                                                                             out double radius)
        {
            TrackWheel sprocketLeft = trackLeft?.Wheels.First(x => x.Model == TrackWheelModel.Sprocket);
            TrackWheel sprocketRight = trackRight?.Wheels.First(x => x.Model == TrackWheelModel.Sprocket);
            if (sprocketLeft != null && sprocketRight != null)
            {
                separation = Vector3.Distance(sprocketLeft.Frame.Position, sprocketRight.Frame.Position);
                radius = sprocketLeft.Radius + trackLeft.Thickness;
                return true;
            }
            else
            {
                separation = 0.0;
                radius = 0.0;
                return false;
            }
        }

        /// <summary>
        /// Constraint��ReferenceObject�܂���ConnectedObject�ɒ��ڂɑ}������TrackWheel�R���|�l���g��T���A�Ԃ��B
        /// �����Ȃ��ꍇ�́AsearchInChildren=True��������AReferenceObject������ConnectedObject�̊K�w��
        /// TrackWheel�R���|�l���g���܂��T���A�Ԃ��B
        /// </summary>
        public static TrackWheel GetTrackWheel(Constraint wheelConstraint, TrackWheelModel? model, bool searchChildren)
        {
            AttachmentPair pair = wheelConstraint?.AttachmentPair;
            if (pair == null)
                return null;

            // �Q��T���Ă݂�F�P��ڂ͂Q��GameObject�̒��ڂ̃R���|�l���g�����T�����A�Q��ڂ͂Q��GameObject�̎q�K�w�ɂ��T���B
            for (int i = 0; i < (searchChildren ? 2 : 1); ++i)
            {
                // Constraint���q���Q��GameObject��TrackWheel��T��
                foreach (var obj in new GameObject[]{ pair.ReferenceObject, pair.ConnectedObject })
                {
                    if (obj == null)
                        continue;

                    Func<TrackWheel, bool> condition = w =>
                        w != null && w.RigidBody.gameObject == obj && (!model.HasValue || w.Model == model);
                    
                    // gameObject�̃R���|�l���g�����T��
                    if (i == 0)
                    {
                        TrackWheel wheel = obj.GetComponent<TrackWheel>();
                        if (condition(wheel))
                            return wheel;
                    }
                    // gameObject�̎q���̃R���|�l���g���T��
                    else
                    {
                        TrackWheel wheel = obj.GetComponentsInChildren<TrackWheel>().First(condition);
                        if (wheel != null)
                            return wheel;
                    }
                }
            }
            return null;
        }
    }
}
