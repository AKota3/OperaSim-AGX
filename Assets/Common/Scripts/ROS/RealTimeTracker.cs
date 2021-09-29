using System;
using RosSharp.RosBridgeClient;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PWRISimulator.ROS
{
    /// <summary>
    /// Unity�̃��C�����[�v�̎d�g�݂̐�����AUnity��GameTime��FixedTime�����A���^�C���ƊO��Ă���BUnity��GameTime��
    /// FixedTime�͌p���I�ȃ��A���^�C���ɑ΂��Ēx��A���U�I�ȃX�e�b�v�Ői�߂���B���̃X�N���v�g��Unity��GameTime�ƃ��A���^�C��
    /// �̊֌W�𑪂�A�Q�̃^�C�����C���̊ԕϊ��ł���@�\��񋟂���B
    /// 
    /// �O���̃V�X�e������l�b�g���[�N�f�[�^�������I�ɓ͂��A���̃f�[�^�𐳂���GameTime�Ɏg�p�������ꍇ�ɖ��ɗ��B
    /// RealTimeDataBuffer�ƈꏏ�Ɏg�p������ƁA���A���^�C���̎��_�ɓ͂����l�b�g���[�N�f�[�^�����̎��_�ɑ΂��ēK����
    /// GameTime���_����擾���邱�Ƃ��ł���悤�ɂȂ�B
    /// 
    /// ��F
    /// 1. �������ɁA�e�f�[�^���Ƃ�RealTimeDataBuffer�Ƃ����o�b�t�@�I�u�W�F�N�g���쐬(generic�d�g�݂�K�p�̂ŁA�^�͂Ȃ�ł�OK�j
    /// 2. �ʓr�ȃX���b�h�Ƀl�b�g���[�N�f�[�^���͂��ƁARealTimeTracker.Realtime�̃^�C���X�^���v��RealTimeDataBuffer
    ///    �o�b�t�@�Ƀf�[�^��}���B
    /// 3. ���C���X���b�h��FixedUpdate���\�b�h����ARealTimeTracker.ConvertUnityTimeToRealTime()���\�b�h���g���Č��݂�
    ///    FixedTime���_��΂��郊�A���^�C�����_�ɕϊ����A���̃^�C���X�^���v��RealTimeDataBuffer�o�b�t�@������f�[�^���擾��,
    ///    ���̃f�[�^�őΏۂ�Unity�p�����[�^�[���X�V����B�i��v����^�C���X�^���v�o�b�t�@�ɂȂ��ꍇ�́A�I�v�V�����ɂ���ėׂ�
    ///    �^�C���X�^���v�̃f�[�^���擾����A�܂��͎���̂Q�̃f�[�^�Ɋ�Â��ĕ�Ԃ���j
    /// </summary>
    [DefaultExecutionOrder(-32000)] // �eFrame�̊J�n�Ɏ��Ԃ𑪂邽�߂ɁA���̃X�N���v�g��葁�����s�����悤��Order���Œ�ɐݒ�
    public class RealTimeTracker : MonoBehaviour
    {
        /// <summary>
        /// �f�o�b�O�p�̃��b�Z�[�W���R���\�[���E�B���h�E�Ƀv�����g���邩�B
        /// </summary>
        public bool printToLog = false;

        /// <summary>
        /// ���A���^�C����Unity�̃^�C�����C���̎��ԍ��B0�ɂ��Ă����C�����[�v�̎d�g�݂�Unity�̃^�C�����C�������A���^�C�����P��
        /// ���C�����[�v�t���[���x���̂ŁA��ʓI��0���傫�����Ȃ��Ă�OK�B�������A�l�b�g���[�N�f�[�^���Ⴂ���g���œ͂��Ă���ꍇ�́A
        /// ��Ԃ��邽�߂̃f�[�^�͈͂��\���ɂȂ�悤�ɂO��菭���傫���ݒ肵�Ă��ǂ����A���R�I�ɂɒx������������B
        /// </summary>
        public double inputValueDelay = 0.0;

        /// <summary>
        /// ���Frame�̊J�n����|���������C���^�C���i�b�j�B
        /// </summary>
        public double RealTime
        {
            get { return realTimeWatch.Elapsed.TotalSeconds; }
        }

        /// <summary>
        /// ���Frame���猻�݂�Frame�̊J�n�܂ł����������C���^�C���i�b�j�B
        /// </summary>
        public double RealTimeAtStartOfFrame
        {
            get
            {
                if (!realtimeAtStartOfFrameIsUpToDate)
                    Debug.LogWarning("Reading RealTimeAtStartOfFrame at a time when it has not yet been updated.");
                return realtimeAtStartOfFrame;
            }
        }

        /// <summary>
        /// Unity���J�b�g����GameTime(�܂�A���A���^�C���ɑ΂��đ��ΓI�ȑ��v���Ԃ̍�)�B�eFrame�Ɉ��X�V�����(���Frame�ȊO)�B
        /// </summary>
        public double SkippedGameTime
        {
            get { return skippedGameTime; }
        }

        /// <summary>
        /// inputValueDelay�y��Unity���J�b�g����GameTime�ɑΉ����āAUnity��GameTime�̎��_�����A���^�C�����_�֕ϊ�����B
        /// </summary>
        /// <param name="gameTimeOrFixedTime">Unity��GameTime�܂���FixedTime</param>
        public double ConvertUnityTimeToRealTime(double gameTimeOrFixedTime)
        {
            return gameTimeOrFixedTime - inputValueDelay + skippedGameTime;
        }

        #region Private

        // �ŏ���Frame�̎n�߂���̃��A���^�C���𑪂�Watch
        System.Diagnostics.Stopwatch realTimeWatch = new System.Diagnostics.Stopwatch();

        // realTimeWatch�͊J�n������
        bool watchStarted = false;

        double realtimeAtStartOfFrame = 0.0;  // ���݂�Frame�̊J�n�ɑ�����realTimeWatch�l
        double realtimeAtStartOfFramePrev = 0.0;
        bool realtimeAtStartOfFrameIsUpToDate = false;

        double skippedGameTime = 0.0; // �p�t�H�[�}���X�Ȃǂ̂�����Unity���J�b�g����GameTime�i��FixedTime�j�B 
        double skippedGameTimePrev = 0.0;

        bool resyncRequested = true;  // unscaledTimeCorrection���܂��v�Z���čX�V���������ǂ���
        double unscaledTimeCorrection = 0.0; //�@Time.unscaledTime��realTimeWatch�̍�

        void Update()
        {
            // �����Frame��FixedUpdate���Ăяo����Ȃ������ꍇ�́AUpdate����J�n���Ԃ𑪂�B
            MeasureRealTimeAtBeginFrame();

            if (printToLog)
                Debug.Log($"{name} : Update() F{Time.frameCount} " +
                          $"DeltaTime = {Time.deltaTime: 0.####} " +
                          $"UnscaledDeltaTime = {Time.unscaledDeltaTime: 0.####} " +
                          $"unscaledTimeAsDouble = {Time.unscaledTimeAsDouble: 0.####} " +
                          $"GameTime = {Time.time: 0.####} " +
                          $"RealTime = {RealTime: 0.####} " +
                          $"RealTimeDeltaTime = {RealTimeAtStartOfFrame - realtimeAtStartOfFramePrev: 0.####} " +
                          $"RealTimeAtStartOfFrame = {RealTimeAtStartOfFrame: 0.####} " +
                          $"RealTimeDiffAtStartOfFrame = {RealTimeAtStartOfFrame - Time.time: 0.####} " +
                          $"SkippedGameTime (updated previous frame) = {skippedGameTime: 0.####} "
                          );
        }

        /// <summary>
        /// �܂��s���Ă��Ȃ��ꍇ�́A���݂�Frame�̊J�n����(���A���^�C���j�𑪂�B
        /// </summary>
        void MeasureRealTimeAtBeginFrame()
        {
            if (!watchStarted)
            {
                realTimeWatch.Start();
                watchStarted = true;
                realtimeAtStartOfFrame = 0.0;
                realtimeAtStartOfFrameIsUpToDate = true;
            }
            else if (!realtimeAtStartOfFrameIsUpToDate)
            {
                realtimeAtStartOfFramePrev = realtimeAtStartOfFrame;
                realtimeAtStartOfFrame = realTimeWatch.Elapsed.TotalSeconds;
                realtimeAtStartOfFrameIsUpToDate = true;
            }
        }

        void LateUpdate()
        {
            // skippedGameTime���X�V
            UpdateSkippedGameTime();

            // realtimeAtStartOfFrame������Frame�̊J�n�ɑ�����悤��
            realtimeAtStartOfFrameIsUpToDate = false;
        }

        /// <summary>
        /// skippedGameTime���v�Z����BLateUpdate�܂���Update����Ăяo���K�v�i���FixedUpdate������s���Ȃ��j�B
        /// </summary>
        void UpdateSkippedGameTime()
        {
            skippedGameTimePrev = skippedGameTime;

            // ���Frame�ɁA���A���^�C���Ɣ�ׂ�unscaledTime���������������Ă���̂Ŏg���Ȃ��BFrame�Q���烊�A���^�C���Ɠ���
            // �悤�ɑ�����̂ŁAFrame2����unscaledTime��time���ׂ�skippedGameTime���v�Z�ł���B
            if (Time.frameCount >= 2)
            {
                // ���Frame��unscaledTime���������������Ă����̂ŁAFrame2��unscaledTime�ƃ��A���^�C���̍���ۑ����āA���ꂩ��
                // unscaledTime��time���ׂ�̂ɂ��̍����܂ށB
                if (resyncRequested)
                {
                    unscaledTimeCorrection = Time.unscaledTimeAsDouble - RealTimeAtStartOfFrame;
                    resyncRequested = false;
                }
                skippedGameTime = (Time.unscaledTimeAsDouble - unscaledTimeCorrection) - Time.timeAsDouble;
            }
            
            if (Math.Abs(skippedGameTime - skippedGameTimePrev) >= 1e-04)
                Debug.Log($"{name} : Unity has skipped {skippedGameTime - skippedGameTimePrev: 0.####}s game time " +
                          $"(at frame {Time.frameCount - 1}). Total skipped game time: {skippedGameTime: 0.####}s");

            if (skippedGameTime - skippedGameTimePrev <= -1e-04)
                Debug.LogError("Critical Timing Problem: SkippedGameTime has decreased.");

            if (printToLog)
                Debug.Log($"{name} : skippedGameTime = {skippedGameTime}");
        }

        void FixedUpdate()
        {
            MeasureRealTimeAtBeginFrame();

            if (printToLog)
                Debug.Log($"{name} : FixedUpdate() F{Time.frameCount} fixedTime = {Time.fixedTime: 0.###}");
        }

        void OnDestroy()
        {
            realTimeWatch.Stop();
        }

        #endregion
    }
}
