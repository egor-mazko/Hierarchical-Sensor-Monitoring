﻿using HSMCommon.Extensions;
using HSMDataCollector.Core;
using HSMDataCollector.Options;
using HSMDataCollector.PublicInterface;
using HSMServer.Core.Cache;
using HSMServer.Core.DataLayer;
using HSMServer.Core.StatisticInfo;
using HSMServer.ServerConfiguration.Monitoring;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HSMServer.BackgroundServices
{
    public sealed class DatabaseStatistics : DatabaseBase
    {
        private readonly TimeSpan _periodicity = TimeSpan.FromDays(1); // TODO: should be initialized from optionsMonitor

        private readonly ITreeValuesCache _cache;

        private readonly IFileSensor _dbStatistics;
        private readonly IInstantValueSensor<double> _heaviestSensors;

        private DateTime _nextStart;


        public DatabaseStatistics(IDataCollector collector, IDatabaseCore database,
            IOptionsMonitor<MonitoringOptions> optionsMonitor, ITreeValuesCache cache)
            : base(collector, database, optionsMonitor)
        {
            _cache = cache;

            _dbStatistics = CreateFileSensor();
            _heaviestSensors = CreateDoubleSensor();

            UpdateNextStart();
        }


        internal override void SendInfo()
        {
            if (DateTime.UtcNow < _nextStart)
                return;

            UpdateNextStart();

            _ = BuildStatistics();
        }


        private IFileSensor CreateFileSensor()
        {
            var options = new FileSensorOptions
            {
                Alerts = [],
                TTL = TimeSpan.MaxValue,
                KeepHistory = TimeSpan.FromDays(7),
                Description = $"File with extended statistics of sensors history database memory.",

                DefaultFileName = "Database statistics",
                Extension = "csv",
            };

            return _collector.CreateFileSensor($"{NodeName}/Sensors statistics", options);
        }

        private IInstantValueSensor<double> CreateDoubleSensor()
        {
            var options = new InstantSensorOptions // TODO: Grafana??
            {
                Alerts = [], // TODO: alert '> 800 mb' should be added
                TTL = TimeSpan.MaxValue,
                SensorUnit = HSMSensorDataObjects.SensorRequests.Unit.MB,
                Description = $"The heaviest sensors.",
            };

            return _collector.CreateDoubleSensor($"{NodeName}/The heaviest sensors", options);
        }

        private void UpdateNextStart() => _nextStart = DateTime.UtcNow.Ceil(_periodicity);

        private async Task BuildStatistics()
        {
            var tempFilePath = Path.GetTempFileName(); // TODO: or use Environment.CurrentDirectory??

            await using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite))
            {
                await using var writer = new StreamWriter(stream);

                await writer.WriteLineAsync("Product,Path,Total size (bytes),Values size (bytes),Data count");

                foreach (var product in _cache.GetProducts())
                    await WriteStats(_cache.GetNodeHistoryInfo(product.Id), writer);
            }

            await _dbStatistics.SendFile(tempFilePath);

            File.Delete(tempFilePath);
        }

        private async Task WriteStats(NodeHistoryInfo nodeInfo, StreamWriter writer)
        {
            foreach (var (sensorId, sensorInfo) in nodeInfo.SensorsInfo)
            {
                var sensor = _cache.GetSensor(sensorId);

                if (sensor is not null)
                    await writer.WriteLineAsync($"{sensor.RootProductName},{sensor.Path},{sensorInfo.TotalSizeBytes},{sensorInfo.ValuesSizeBytes},{sensorInfo.DataCount}");
            }

            foreach (var (_, subnodeInfo) in nodeInfo.SubnodesInfo)
                await WriteStats(subnodeInfo, writer);
        }
    }
}
