﻿using HSMDataCollector.Core;
using HSMDataCollector.Logging;
using HSMSensorDataObjects.SensorValueRequests;
using System;
using System.Collections.Generic;

namespace HSMDataCollector.SyncQueue
{
    internal sealed class QueueManager : IQueueManager
    {
        private readonly List<SyncQueue> _queueList = new List<SyncQueue>();


        public ISyncQueue<SensorValueBase> Data { get; }

        public ICommandQueue Commands { get; }


        public event Action<PackageSendingInfo> PackageSendingInfo;
        public event Action<string, int> PackageValuesCountInfo;
        public event Action<string, int> OverflowInfo;


        internal QueueManager(CollectorOptions options, ILoggerManager logger) : base()
        {
            Commands = RegisterQueue(new CommandsQueue(options, logger));
            Data = RegisterQueue(new SensorDataQueue(options));
        }


        public void Init() => _queueList.ForEach(q => q.Init());

        public void Stop() => _queueList.ForEach(q => q.Stop());

        public void ThrowPackageSensingInfo(PackageSendingInfo info) => PackageSendingInfo?.Invoke(info);

        public void Dispose()
        {
            foreach (var queue in _queueList)
            {
                queue.SendValuesCnt -= ThrowPackageValuesCountInfo;
                queue.OverflowCnt -= ThrowOverflowInfo;
                queue.Dispose();
            }
        }


        private T RegisterQueue<T>(T queue) where T : SyncQueue
        {
            _queueList.Add(queue);

            queue.SendValuesCnt += ThrowPackageValuesCountInfo;
            queue.OverflowCnt += ThrowOverflowInfo;

            return queue;
        }


        private void ThrowOverflowInfo(string queue, int valuesCnt) => OverflowInfo?.Invoke(queue, valuesCnt);

        private void ThrowPackageValuesCountInfo(string queue, int valuesCnt) => PackageValuesCountInfo?.Invoke(queue, valuesCnt);
    }
}