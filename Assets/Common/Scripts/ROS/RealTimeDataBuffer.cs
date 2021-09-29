using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace PWRISimulator.ROS
{
    public enum RealTimeDataAccessType { Closest, Previous, Next, Interpolate /*, Extrapolate*/ };

    /// <summary>
    /// ���n��̃��A���^�C���f�[�^���Ǘ�����N���X�B�Ⴆ�΁A�Q�̊O���^�C�����C���̊ԃf�[�^�����L�ł���悤�ɂ���B
    /// 
    /// �f�[�^�v���W���[�T�[��Add(data, time)���\�b�h�ŐV�����f�[�^�����n��o�b�t�@�ɓ���A�f�[�^�R���V���[�}��Get(time)
    /// ���\�b�h�Ńf�[�^���擾����g�����ƂȂ�BAdd��Get�͕ʁX�ȃX���b�h����Ăяo����BGet�����f�[�^���Â��f�[�^�������I��
    /// �폜�����B
    /// </summary>
    /// <typeparam name="T">�f�[�^�̃^�C�v</typeparam>
    public class RealTimeDataBuffer<T>
    {
        public delegate T Interpolator(T a, T b, double t);

        struct Entry
        {
            public double time;
            public T data;
            public Entry(double time, T data)
            {
                this.time = time;
                this.data = data;
            }

            public override string ToString()
            {
                return $"time = {time}, data = {data}";
            }
        };

        readonly LinkedList<Entry> buffer;
        readonly object bufferLock = new object();
        readonly int maxBufferSize = 1000;
        readonly Interpolator interpolator = null;
        readonly RealTimeDataAccessType accessType = RealTimeDataAccessType.Previous;

        bool removeHistoryWhenReading = true;
        bool clampToFirst = true;
        bool clampToLast = true;
        
        public RealTimeDataBuffer(int maxBufferSize, RealTimeDataAccessType accessType, 
            Interpolator interpolator = null)
        {
            if (accessType == RealTimeDataAccessType.Interpolate && interpolator == null)
                Debug.LogError($"{GetType().Name} : Cannot use Interpolate access type without an interpolator.");

            buffer = new LinkedList<Entry>();
            this.maxBufferSize = maxBufferSize;
            this.accessType = accessType;
            this.interpolator = interpolator;
        }

        public bool Add(T data, double time)
        {
            Entry entry = new Entry(time, data);
            int errorCode = 0;
            lock (bufferLock)
            {
                if(buffer.Count == 0 || time > buffer.Last.Value.time)
                {
                    buffer.AddLast(entry);
                    if (buffer.Count > maxBufferSize)
                        buffer.RemoveFirst();
                }
                else if(time == buffer.Last.Value.time)
                    buffer.Last.Value = entry;
                else /*if(time < buffer.Last.Value.time)*/
                    errorCode = 1;
            }

            if (errorCode == 1)
            {
                Debug.LogError($"{GetType().Name} : Trying to insert a value with a timestamp older ({time}) than " +
                               $"the most recent value's timestamp. The value will not be added.");
                return false;
            }
            else
            {
                return true;
            }
        }

        public T Get(double time)
        {
            T data = default(T);
            lock (bufferLock)
            {
                // �T�C�Y�͂O�̏ꍇ
                if (buffer.Count == 0)
                {
                    //Debug.Log("Trying to get value from an empty RealTimeDataBuffer. Will return default value.");
                    return default(T);
                }
                // �w����time�͌��݂̎��n����Â��ꍇ
                else if (time < buffer.First.Value.time)
                {
                    if (clampToFirst)
                        return buffer.First.Value.data;
                    else
                    {
                        Debug.LogWarning($"{GetType().Name} : Trying to get a value from  with a timestamp older " + 
                                         "than the oldest value. Default value will be returned.");
                        return default(T);
                    }
                }
                // �w����time�͌��݂̎��n����V�����ꍇ
                else if (time > buffer.Last.Value.time)
                {
                    // �̂̃f�[�^���폜
                    if (removeHistoryWhenReading)
                        while (buffer.Last.Previous != null)
                            buffer.Remove(buffer.Last.Previous);

                    if (clampToLast)
                        return buffer.Last.Value.data;
                    else
                    {
                        Debug.LogWarning($"{GetType().Name} : Trying to get a value with a timestamp newer than the " + 
                                          "most recent value. Default value will be returned.");
                        return default(T);
                    }
                }
                else
                {
                    // ��L�ȊO�Atime�����킹�ėv�f������
                    LinkedListNode<Entry> node = buffer.First;
                    while (node != null)
                    {
                        // �w����time�̊����ȃ}�b�`�̏ꍇ
                        if (time == node.Value.time)
                        {
                            data = node.Value.data;
                            break;
                        }
                        // �w����time���O�̗v�f���傫���Ď��̗v�f��菬�����ꍇ
                        else if (node.Next != null && time < node.Next.Value.time)
                        {
                            // AccessType�ɂ���ėׂ̗v�f����f�[�^���擾
                            if (accessType == RealTimeDataAccessType.Previous)
                                data = node.Value.data;
                            else if (accessType == RealTimeDataAccessType.Next)
                                data = node.Next.Value.data;
                            else if (accessType == RealTimeDataAccessType.Closest)
                                data = Math.Abs(time - node.Next.Value.time) < Math.Abs(time - node.Previous.Value.time) ?
                                    node.Next.Value.data :
                                    node.Value.data;
                            else if (accessType == RealTimeDataAccessType.Interpolate)
                            {
                                double t = (time - node.Value.time) / (node.Next.Value.time - node.Value.time);
                                data = interpolator(node.Value.data, node.Next.Value.data, t);
                            }

                            // �̂̃f�[�^���폜
                            if (removeHistoryWhenReading)
                                while (node.Previous != null)
                                    buffer.Remove(node.Previous);
                            // �f�[�^���������̂Ń��[�v����߂�
                            break;
                        }
                        else
                        {
                            // �f�[�^���܂������Ă��Ȃ��̂Ń��[�v�𑱂���
                            node = node.Next;
                            Debug.Assert(node != null, $"{GetType().Name} : Unexpected code reached.");
                        }
                    }
                }
            }
            return data;
        }

        void Clear()
        {
            lock (bufferLock)
            {
                buffer.Clear();
            }
        }
    }
}