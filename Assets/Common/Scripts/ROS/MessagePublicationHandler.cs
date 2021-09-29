using System;
using UnityEngine;
using RosSharp.RosBridgeClient;

namespace PWRISimulator.ROS
{
    public interface IMessagePublicationHandler
    {
        void UpdateAndSendMessage();

        void UnAdvertise();

    };

    /// <summary>
    /// �w�������f�[�^�擾���\�b�h���g���āA�I�[�i�[���Ǘ���������ɂ��ROS��topic��publish����B
    /// </summary>
    public class MessagePublicationHandler<T> : IMessagePublicationHandler where T : Message
    {
        RosConnector rosConnector;
        Func<T> getMessageFunction;
        string publicationId;

        public MessagePublicationHandler(RosConnector rosConnector, string topicName, Func<T> getMessageFunction)
        {
            this.rosConnector = rosConnector;
            this.getMessageFunction = getMessageFunction;

            if (getMessageFunction == null)
            {
                Debug.LogError($"Failed to advertise topic \"{topicName}\" because getMessageFunction null.");
                return;
            }

            if (rosConnector?.RosSocket == null)
            {
                Debug.LogError($"Failed to advertise topic \"{topicName}\" because RosConnector or RosSocket is null.");
                return;
            }

            publicationId = rosConnector.RosSocket.Advertise<T>(topicName);

            Debug.Log($"Advertised topic \"{topicName}\".");

        }

        public void UnAdvertise()
        {
            if (rosConnector?.RosSocket == null || publicationId == null)
                return;

            Debug.Log($"UnAdvertise topic \"{publicationId}\".");

            rosConnector.RosSocket.Unadvertise(publicationId);
            publicationId = null;
        }

        /// <summary>
        /// �w�������f�[�^�擾���\�b�h���Ăяo���A�߂�l��publish����B
        /// </summary>
        public void UpdateAndSendMessage()
        {
            if (rosConnector?.RosSocket == null || publicationId == null || getMessageFunction == null)
                return;

            T msg = getMessageFunction();
            rosConnector.RosSocket.Publish(publicationId, msg);
        }
    }
}
