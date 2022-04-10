using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Unity.Profiling;
using RosSharp;
using RosSharp.RosBridgeClient;
using PointCloud2Msg = RosSharp.RosBridgeClient.MessageTypes.Sensor.PointCloud2;
using PointFieldMsg = RosSharp.RosBridgeClient.MessageTypes.Sensor.PointField;

namespace PWRISimulator.ROS
{
    /// <summary>
    /// �w������PointCloudGenerator�I�u�W�F�N�g����_�Q�f�[�^���擾��ROS�֒ʐM����B
    /// </summary>
    public class PointCloudPublisher : SingleMessagePublisher<PointCloud2Msg>
    {
        #region Inspector Properties

        public PointCloudGenerator pointCloudGenerator;
        public string frameId = "";
        public int frequency = 1;
        public bool includeTimeInMessage = false;
        public bool publishFromThread = false;

        #endregion

        #region Private Variables

        PointCloud2Msg message = null;
        Vector3[] points = new Vector3[0];
        float[] pointsAsFloats = new float[0];
        byte[] pointsAsBytes = new byte[0];

        // �ʓr��publish�X���b�h�p�F

        Thread publishThread;
        ManualResetEventSlim publishResetEvent = new ManualResetEventSlim(false);
        CancellationTokenSource cancellationTokenSource = null;

        // �v���t�@�C�����O�p�F

        static readonly ProfilerMarker profileMarker_GetPoints = new ProfilerMarker(
            ProfilerCategory.Scripts, nameof(PointCloudPublisher) + ".GetPoints");

        static readonly ProfilerMarker profileMarker_ConvertPoints = new ProfilerMarker(
            ProfilerCategory.Scripts, nameof(PointCloudPublisher) + ".ConvertPoints");

        static readonly ProfilerMarker profileMarker_CreateMessage = new ProfilerMarker(
            ProfilerCategory.Scripts, nameof(PointCloudPublisher) + ".CreateMessage");

        static readonly ProfilerMarker profileMarker_SendMessage = new ProfilerMarker(
            ProfilerCategory.Scripts, nameof(PointCloudPublisher) + ".SendMessage");

        #endregion

        #region Private Methods

        protected override void OnAdvertised()
        {
            base.OnAdvertised();
            
            // �w���������g����publish����Coroutine���J�n
            StartCoroutine(PublishCoroutine());
        }

        protected override void OnUnadvertised()
        {
            base.OnUnadvertised();
            
            // publish����Coroutine�𒆎~
            StopCoroutine(nameof(PublishCoroutine));
            
            // �ʓr�ȃX���b�h����publish���s���o�C�A�͂��̃X���b�h�𒆎~
            if (publishThread != null)
                StopPublishThread();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (publishThread != null)
                StopPublishThread();
        }

        /// <summary>
        /// �w���������g����publish����B
        /// </summary>
        System.Collections.IEnumerator PublishCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1.0f / Math.Max(1, frequency));
                UpdateMessageAndPublish();
            }
        }

        /// <summary>
        /// PointCloudGenerator����_�Q�f�[�^���擾���āAROS Message���X�V����publish����B
        /// publishFromThread��true�̏ꍇ�́A���ڂ��̃X���b�h����publish���Ȃ��ApublishResetEvent��set���ĕʓr��PublishThread
        /// ��publish������B
        /// </summary>
        void UpdateMessageAndPublish()
        {
            if (rosConnector?.RosSocket == null || publicationId == null)
                return;

            if (publishFromThread && publishResetEvent.IsSet)
            {
                Debug.LogWarning($"{name} : Attempting to generate point cloud while Publish Thread is not done yet." +
                                 " Will skip point cloud generation and wait until occurance.");
                return;
            }

            using (profileMarker_GetPoints.Auto())
            {
                // �|�C���g�f�[�^���擾
                if (pointCloudGenerator != null)
                    pointsAsFloats = pointCloudGenerator.GeneratePointCloud(flipX: true);
                else
                    Debug.LogWarning($"{name} Cannot generate point cloud because Point Cloud Generator is null.");
            }

            using (profileMarker_ConvertPoints.Auto())
            {
                // �|�C���g�f�[�^��byte�z��֕ϊ�
                ConvertFloatsToRosByteArray(pointsAsFloats, ref pointsAsBytes);
            }

            using (profileMarker_CreateMessage.Auto())
            {
                // ��񂾂��Ďg�p�ł��郁�b�Z�[�W�x�[�X���쐬
                if (message == null)
                {
                    message = new PointCloud2Msg();
                    message.header = MessageUtil.ToHeaderMessage(0, frameId);
                    message.data = new byte[0];
                    message.width = 0;
                    message.height = 1;
                    message.fields = CreatePointFields(reorder: true);
                    message.is_bigendian = false;
                    message.point_step = 3 * sizeof(float);
                    message.row_step = 1;
                    message.is_dense = true;
                }

                // �ς��f�[�^��������������
                message.header = MessageUtil.ToHeaderMessage(includeTimeInMessage ? Time.timeAsDouble : 0, frameId);
                message.data = pointsAsBytes;
                message.width = (uint)pointsAsBytes.Length / message.point_step;
            }

            using (profileMarker_SendMessage.Auto())
            {
                // �ʐM������
                if (publishFromThread)
                {
                    if (publishThread == null || !publishThread.IsAlive)
                    {
                        if (cancellationTokenSource == null)
                            cancellationTokenSource = new CancellationTokenSource();
                        var token = cancellationTokenSource.Token;
                        publishThread = new Thread(() => PublishThread(token));
                        publishThread.Name = nameof(PointCloudPublisher) + ".PublishThread";
                        publishThread.Start();
                    }
                    publishResetEvent.Set();
                }
                else
                {
                    Publish(message);
                }
            }
        }

        /// <summary>
        /// publish����X���b�h�BpublishResetEvent��set�ɂȂ��publish�����āApublish���I�������publishResetEvent��reset�B
        /// </summary>
        /// <param name="cancellationToken"></param>
        void PublishThread(CancellationToken cancellationToken)
        {
            Debug.Log("Publish thread is starting.");
            int timeoutMs = 10000; // timeout�ɂȂ�Ȃ��R�[�h�����������ǔN�̖���timeout�g�p
            while (publicationId != null)
            {
                try
                {
                    if (!publishResetEvent.Wait(timeoutMs, cancellationToken))
                        continue;
                }
                catch (Exception ex)
                {
                    if (ex is ObjectDisposedException || ex is ObjectDisposedException ||
                        ex is OperationCanceledException || ex is InvalidOperationException)
                        break; // �킴��CancellationToken���L�����Z������������Exception
                    else
                        throw;
                }
                if (publicationId != null)
                {
                    Publish(message);
                    publishResetEvent.Reset();
                }
            }
            publishResetEvent.Reset();
            Debug.Log("Publish thread is exiting.");
        }

        void StopPublishThread(int timeoutUntilAbort = 2000)
        {
            Debug.Log($"{name} : Stopping publish thread.");
            publicationId = null;

            if (cancellationTokenSource != null)
            {
                Debug.Log($"{name} : Cancelling the cancellation token source.");
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            if (publishThread != null && !publishThread.Join(timeoutUntilAbort))
            {
                Debug.Log($"{name} : Failed to stop the publish thread. Aborting it.");
                publishThread.Abort();
                publishThread = null;
            }
            Debug.Log($"{name} : Publish thread stopped.");
        }

        /// <summary>
        /// �P��Point�̒�`���쐬�B
        /// </summary>
        /// <returns></returns>
        static PointFieldMsg[] CreatePointFields(bool reorder)
        {
            // ROS X = Unity Z
            // ROS Y = Unity -X
            // ROS Z = Unity Y
            // �܂�A
            // Unity X = ROS -Y
            // Unity Y = ROS Z
            // Unity Z = ROS X

            // �� x���W�͊��Ƀf�[�^�𐶐��̂Ƃ��ɋt�ɂ��Ă������̂ŕ�����t���Ȃ��irviz�͕�����Field�L�ڂ�Ή��������Ȃ��悤������j
            PointFieldMsg[] pointFields = {
                new PointFieldMsg(reorder ? "y" : "x", 0 * sizeof(float), PointFieldMsg.FLOAT32, 1),
                new PointFieldMsg(reorder ? "z" : "y", 1 * sizeof(float), PointFieldMsg.FLOAT32, 1),
                new PointFieldMsg(reorder ? "x" : "z", 2 * sizeof(float), PointFieldMsg.FLOAT32, 1)};

            return pointFields;
        }

        /// <summary>
        /// float�z���byte�z��֕ϊ��B���W�n�ϊ��͍s��Ȃ��B
        /// </summary>
        /// <param name="floats">���͂�float�z��</param>
        /// <param name="bytes">�o�͂�byte�z��</param>
        void ConvertFloatsToRosByteArray(float[] floats, ref byte[] bytes)
        {
            if (bytes == null || bytes.Length != floats.Length * sizeof(float))
            {
                bytes = new byte[floats.Length * sizeof(float)];
                //Debug.Log($"{name} : Resized byte array to {bytes.Length} ({floats.Length} floats).");
            }
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Vector3�z���byte�z��֕ϊ��BUnity����ROS�ւ̍��W�n�ϊ����s���B
        /// </summary>
        /// <param name="floats">���͂�Vector3�z��</param>
        /// <param name="bytes">�o�͂�byte�z��</param>
        void ConvertPointsToRosByteArray(Vector3[] points, ref byte[] bytes)
        {
            if (bytes == null || bytes.Length != points.Length * 3 * sizeof(float))
            {
                bytes = new byte[points.Length * 3 * sizeof(float)];
                Debug.Log($"{name} : Resized byte array to {bytes.Length} ({points.Length} points).");
            }

            float[] comp = new float[1];
            for (int i = 0, j = 0; i < points.Length; i++, j += 12)
            {
                // �ȍ~�̃R�[�h�́Afloat��bytes�֕ϊ�����Ƃ��ɁA������ROS���W�n�֕ϊ��ix = z, y = -x, z = y�j
                Vector3 p = points[i];

                // X���W��float��byte�Ƃ��ď�������
                comp[0] = p.z;
                Buffer.BlockCopy(comp, 0, bytes, j + 0, sizeof(float));

                // Y���W��float��byte�Ƃ��ď�������
                comp[0] = -p.x;
                Buffer.BlockCopy(comp, 0, bytes, j + 4, sizeof(float));

                // Z���W��float��byte�Ƃ��ď�������
                comp[0] = p.y;
                Buffer.BlockCopy(comp, 0, bytes, j + 8, sizeof(float));
            }
        }

        #endregion
    }
}
