﻿using HSMDataCollector.DefaultSensors;
using HSMDataCollector.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace HSMDataCollector.Core
{
    internal sealed class SensorsStorage : ConcurrentDictionary<string, MonitoringSensorBase>, IDisposable
    {
        private readonly IValuesQueue _valuesQueue;
        private readonly LoggerManager _logManager;


        internal SensorsStorage(IValuesQueue queue, LoggerManager logManager)
        {
            _valuesQueue = queue;
            _logManager = logManager;
        }


        public void Dispose()
        {
            foreach (var value in Values)
            {
                value.ReceiveSensorValue -= _valuesQueue.EnqueueData;

                value.Dispose();
            }
        }

        internal Task Start() => Task.WhenAll(Values.Select(s => s.Start()));

        internal void Register(string key, MonitoringSensorBase value)
        {
            if (TryAdd(key, value))
            {
                value.ReceiveSensorValue += _valuesQueue.EnqueueData;

                _logManager.Logger?.Info($"Added new default sensor {key}");
            }
        }
    }
}