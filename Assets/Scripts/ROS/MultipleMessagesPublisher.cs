using System;
using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using Float64Msg = RosSharp.RosBridgeClient.MessageTypes.Std.Float64;

namespace PWRISimulator.ROS
{
    /// <summary>
    /// ������ROS�g�s�b�N��publish����R���|�l���g�x�[�X�N���X�B��̓I�Ȏq�N���X�����̃x�[�X�N���X���p������OnAdvertise���\�b�h
    /// ����e�g�s�b�N���Ƃ�AddPublicationHandler()���Ăяo���悤�ɂ��Ă��������B���̃x�[�X�N���X�͎����I�ɒǉ�����
    /// PublicationHandler��Frequency�ɂ������I��Publish���Ă���B
    /// </summary>
    public abstract class MultipleMessagesPublisher : MonoBehaviour
    {
        public RosConnector rosConnector;
        public int frequency = 20;
        
        List<IMessagePublicationHandler> publicationHandlers = new List<IMessagePublicationHandler>();

        bool hasStarted = false;
        bool isQuitting = false;

        /// <summary>
        /// �q�N���X����I�[�o���C�h���郁�\�b�h�B�C���v���P�[�V�������ɂ́A�e�g�s�b�N��AddPublicationHandler()���Ăяo���͂����B
        /// </summary>
        protected abstract void OnAdvertise();

        protected virtual void Reset()
        {
            // rosConnector�̃f�t�H���g�l�ɂ̓V�[���ɂ���RosConnector��T���Đݒ肷��
            rosConnector = FindObjectOfType<RosConnector>(includeInactive: false);
        }

        protected virtual void Start()
        {
            hasStarted = true;
            StartCoroutine(UpdateAndPublishMessagesCoroutine());
            OnAdvertise();
        }

        protected virtual void OnEnable()
        {
            // �J�n��OnEnable��Start��葁���Ăяo����Ă��邪�A���̂Ƃ���RosConnector�͂܂�����������Ă��Ȃ��̂ŁA
            // Start()�܂�Advertise��҂�����B
            if (hasStarted)
            {
                StartCoroutine(UpdateAndPublishMessagesCoroutine());
                OnAdvertise();
            }
        }

        protected virtual void OnDisable()
        {
            if (!isQuitting)
            {
                StopCoroutine(nameof(UpdateAndPublishMessagesCoroutine));

                foreach (var handler in publicationHandlers)
                    handler.UnAdvertise();

                publicationHandlers.Clear();
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        protected void AddPublicationHandler<T>(string topicName, Func<T> getMessageFunction) where T : Message
        {
            if (string.IsNullOrWhiteSpace(topicName))
                return;

            var handler = new MessagePublicationHandler<T>(rosConnector, topicName, getMessageFunction);
            publicationHandlers.Add(handler);
        }

        void UpdateAndPublishMessages()
        {
            foreach (var handler in publicationHandlers)
                handler.UpdateAndSendMessage();
        }

        System.Collections.IEnumerator UpdateAndPublishMessagesCoroutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1.0f / Math.Max(1, frequency));
                UpdateAndPublishMessages();
            }
        }
    }
}
