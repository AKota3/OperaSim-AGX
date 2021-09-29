using System;
using UnityEngine;
using RosSharp.RosBridgeClient;
using Float64Msg = RosSharp.RosBridgeClient.MessageTypes.Std.Float64;

namespace PWRISimulator.ROS
{
    /// <summary>
    ///  �p���I�Ȏ����ɓ͂����b�Z�[�W�́A�I�[�i�[���Ǘ���������ɓK�p�ł���悤�ɂ���X�N���v�g�B
    /// </summary>
    public interface IMessageSubscriptionHandler
    {
        /// <summary>
        /// ���b�Z�[�W��Unity�̑Ώۂ̃p�����[�^�Ƃ��ɐݒ肷�郁�\�b�h�B�I�[�i�[�������I�ɌĂяo���͂����B
        /// </summary>
        /// <param name="time">���݂�GameTime��FixedTime��V�~�����[�V����(�ǂ�����̎g�����ɂ����)</param>
        void ExecuteMessageAction(double time);
    };

    /// <summary>
    /// ���b�Z�[�W���g�������Ƃ��ɁA�ŏI�ɓ͂������b�Z�[�W���g�p����IMessageSubscriptionHandler�X�N���v�g�B
    /// �ڍׁF
    /// * ���b�Z�[�W���͂���OnReceivedMessage���ʓr�ȃX���b�h����Ăяo����A���b�Z�[�W��ۑ�����i�ȑO�̃��b�Z�[�W���폜�j�B
    /// * �I�[�i�[��ExecuteMessageAction�������I�Ƀ��C���X���b�h������s���āA�ŏI�ɓ͂������b�Z�[�W��K�p����B
    /// </summary>
    /// <typeparam name="T">ROS���b�Z�[�W�N���X</typeparam>
    public class MessageSubscriptionHandler<T> : IMessageSubscriptionHandler where T : Message
    {
        RosSharp.RosBridgeClient.SubscriptionHandler<T> messageAction;
        T lastReceivedValue = null;

        public MessageSubscriptionHandler(RosConnector rosConnector, string topicName,
            RosSharp.RosBridgeClient.SubscriptionHandler<T> messageAction, int throttleRate = 0)
        {
            this.messageAction = messageAction;
            if(rosConnector?.RosSocket == null)
            {
                Debug.LogError($"Failed to subscribe to topic \"{topicName}\" because RosConnector or RosSocket is null.");
                return;
            }
            rosConnector.RosSocket.Subscribe<T>(topicName, OnReceivedMessage, throttleRate);
        }

        /// <summary>
        /// ���b�Z�[�W���͂����Ƃ���ROSBridgeClient����Ăяo�����R�[���o�b�N���\�b�h�B���C���X���b�h�ȊO�X���b�h������s�����B
        /// </summary>
        void OnReceivedMessage(T msg)
        {
            lastReceivedValue = msg;
        }

        /// <summary>
        /// �ŏI�ɓ͂������b�Z�[�W��K�p����B�I�[�i�[�������I�ɌĂяo���͂����B
        /// </summary>
        /// <param name="time">���݂�GameTime��FixedTime��V�~�����[�V����(�ǂ�����̎g�����ɂ����)</param>
        public void ExecuteMessageAction(double time)
        {
            T msg = lastReceivedValue;
            if (msg != null)
                messageAction(msg);
        }
    }

    /// <summary>
    /// ���b�Z�[�W���g�������ɁA���݂�Game���_�ɑΉ��������A���^�C�����_�ɓ͂������b�Z�[�W���g�p����IMessageSubscriptionHandler�B
    /// �ڍׁF
    /// * ���b�Z�[�W���͂���OnReceivedMessage���ʓr�ȃX���b�h����Ăяo����A���b�Z�[�W��RealTimeDataBuffer�ɑ}������B
    /// * ExecuteMessageAction�����s����ƁA���݂�FixedTime���_��Ή����郊�A���^�C�����_�ɕϊ����A���̎��_�ɓ͂������b�Z�[�W��
    ///   �K�p����B���A���^�C�����_�����m�Ɉ�v���Ȃ������������߂ɁA�Q�ׂ̗̓͂����f�[�^�Ɋ�Â��ĕ�Ԃ���d�g�݂�񋟂���B
    /// </summary>
    /// <typeparam name="T">ROS���b�Z�[�W�N���X</typeparam>
    public class TimeCorrectedMessageSubscriptionHandler<T> : IMessageSubscriptionHandler where T : Message
    {
        RosSharp.RosBridgeClient.SubscriptionHandler<T> messageAction;
        RealTimeTracker realTimeTracker;
        RealTimeDataBuffer<T> realTimeDataBuffer;

        public TimeCorrectedMessageSubscriptionHandler(RosConnector rosConnector, string topicName,
            RosSharp.RosBridgeClient.SubscriptionHandler<T> messageAction,�@RealTimeTracker synchronizer,
            RealTimeDataBuffer<T>.Interpolator interpolator = null, int throttleRate = 0, int maxBufferSize = 200)
        {
            this.messageAction = messageAction;
            if (rosConnector?.RosSocket == null)
            {
                Debug.LogError($"Failed to subscribe to topic \"{topicName}\" because RosConnector or RosSocket is null.");
                return;
            }
            
            rosConnector?.RosSocket.Subscribe<T>(topicName, OnReceivedMessage, throttleRate);

            RealTimeDataAccessType accessType = interpolator != null ? 
                RealTimeDataAccessType.Interpolate : RealTimeDataAccessType.Previous;

            realTimeDataBuffer = new RealTimeDataBuffer<T>(maxBufferSize, accessType, interpolator);
            realTimeTracker = synchronizer;
        }

        /// <summary>
        /// ���b�Z�[�W���͂����Ƃ���ROSBridgeClient����Ăяo�����R�[���o�b�N���\�b�h�B���C���X���b�h�ȊO�X���b�h������s�����B
        /// </summary>
        void OnReceivedMessage(T msg)
        {
            double realTime = realTimeTracker.RealTime;
            if (realTimeTracker.printToLog)
                Debug.Log($"OnReceivedMessage() realTime = {realTime}, msg = {MessageUtil.MessageToString(msg)}");
            realTimeDataBuffer.Add(msg, realTime); 
        }

        /// <summary>
        /// ���݂�time���_��Ή����郊�A���^�C�����_�ɕϊ����A���̎��_�ɓ͂������b�Z�[�W��K�p����B�I�[�i�[�������I�ɌĂяo��
        /// �͂����B
        /// </summary>
        /// <param name="time">���݂�GameTime��FixedTime��V�~�����[�V����(�ǂ�����̎g�����ɂ����)</param>
        public void ExecuteMessageAction(double time)
        {
            double realTime = realTimeTracker.ConvertUnityTimeToRealTime(time);
            T msg = realTimeDataBuffer.Get(realTime);

            if (realTimeTracker.printToLog)
                Debug.Log($"ExecuteMessageAction() time = {time}, realTime = {realTime} " + 
                          $"msg = {MessageUtil.MessageToString(msg)}");

            if (msg != null)
                messageAction(msg);
        }
    }
}
