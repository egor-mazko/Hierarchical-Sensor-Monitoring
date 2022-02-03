using HSMSensorDataObjects;
using HSMSensorDataObjects.FullDataObject;
using HSMServer.Core.Authentication;
using HSMServer.Core.Cache;
using HSMServer.Core.Configuration;
using HSMServer.Core.Model;
using HSMServer.Core.Model.Sensor;
using HSMServer.Core.MonitoringServerCore;
using HSMServer.Core.Products;
using HSMServer.Core.Tests.Infrastructure;
using HSMServer.Core.Tests.MonitoringCoreTests.Fixture;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HSMServer.Core.Tests.MonitoringDataReceiverTests
{
    public class MonitoringDataReceiverTests : IClassFixture<MonitoringDataReceiverFixture>
    {
        private const int SeveralSensorValuesCount = 3;

        private readonly MonitoringCore _monitoringCore;
        private readonly DatabaseAdapterManager _databaseAdapterManager;
        private readonly ValuesCache _valuesCache;
        private readonly BarSensorsStorage _barStorage;

        private readonly SensorValuesFactory _sensorValuesFactory;
        private readonly SensorValuesTester _sensorValuesTester;

        private delegate List<SensorData> GetValuesFromCache(List<string> products);
        private delegate SensorHistoryData GetSensorHistoryData(string productName, string path);
        private delegate List<SensorHistoryData> GetAllSensorHistoryData(string productName, string path);
        private delegate SensorInfo GetSensorInfo(string productName, string path);
        private delegate List<SensorInfo> GetAllSensorInfo(Product product);


        public MonitoringDataReceiverTests(MonitoringDataReceiverFixture fixture)
        {
            _valuesCache = new ValuesCache();

            _databaseAdapterManager = new DatabaseAdapterManager(fixture.DatabasePath);
            _databaseAdapterManager.AddTestProduct();
            fixture.CreatedDatabases.Add(_databaseAdapterManager);

            _sensorValuesFactory = new SensorValuesFactory(_databaseAdapterManager);
            _sensorValuesTester = new SensorValuesTester(_databaseAdapterManager);

            var userManager = new Mock<IUserManager>();

            _barStorage = new BarSensorsStorage();

            var configProviderLogger = CommonMoqs.CreateNullLogger<ConfigurationProvider>();
            var configurationProvider = new ConfigurationProvider(_databaseAdapterManager.DatabaseAdapter, configProviderLogger);

            var productManagerLogger = CommonMoqs.CreateNullLogger<ProductManager>();
            var productManager = new ProductManager(_databaseAdapterManager.DatabaseAdapter, productManagerLogger);

            var monitoringLogger = CommonMoqs.CreateNullLogger<MonitoringCore>();
            _monitoringCore = new MonitoringCore(
                _databaseAdapterManager.DatabaseAdapter,
                userManager.Object,
                _barStorage,
                productManager,
                configurationProvider,
                _valuesCache,
                monitoringLogger);
        }


        [Theory]
        [InlineData(SensorType.BooleanSensor)]
        [InlineData(SensorType.IntSensor)]
        [InlineData(SensorType.DoubleSensor)]
        [InlineData(SensorType.StringSensor)]
        [InlineData(SensorType.IntegerBarSensor)]
        [InlineData(SensorType.DoubleBarSensor)]
        [InlineData(SensorType.FileSensorBytes)]
        [InlineData(SensorType.FileSensor)]
        [Trait("Category", "One")]
        public async Task AddSensorValueTest(SensorType type)
        {
            var sensorValue = _sensorValuesFactory.BuildSensorValue(type);

            _monitoringCore.AddSensorValue(sensorValue);

            await FullSensorValueTestAsync(sensorValue,
                                           _valuesCache.GetValues,
                                           _databaseAdapterManager.DatabaseAdapter.GetOneValueSensorValue,
                                           _databaseAdapterManager.DatabaseAdapter.GetSensorInfo);
        }

        [Theory]
        [InlineData(SensorType.IntegerBarSensor)]
        [InlineData(SensorType.DoubleBarSensor)]
        [Trait("Category", "One")]
        public void AddBarSensorValueTest(SensorType type)
        {
            var sensorValue = _sensorValuesFactory.BuildSensorValue(type);
            (sensorValue as BarSensorValueBase).EndTime = System.DateTime.MinValue;

            _monitoringCore.AddSensorValue(sensorValue);

            var lastBarValue = _barStorage.GetLastValue(_databaseAdapterManager.TestProduct.Name, sensorValue.Path);

            Assert.Equal(_databaseAdapterManager.TestProduct.Name, lastBarValue.ProductName);
            Assert.Equal(SensorValuesTester.GetSensorValueType(sensorValue), lastBarValue.ValueType);
            Assert.Equal(sensorValue, lastBarValue.Value);
        }


        [Theory]
        [InlineData(SensorType.BooleanSensor)]
        [InlineData(SensorType.IntSensor)]
        [InlineData(SensorType.DoubleSensor)]
        [InlineData(SensorType.StringSensor)]
        [InlineData(SensorType.IntegerBarSensor)]
        [InlineData(SensorType.DoubleBarSensor)]
        [InlineData(SensorType.FileSensorBytes)]
        [InlineData(SensorType.FileSensor)]
        [Trait("Category", "Several")]
        public async Task AddSeveralSensorValuesTest(SensorType type)
        {
            var sensorValues = new List<SensorValueBase>(SeveralSensorValuesCount);
            for (int i = 0; i < SeveralSensorValuesCount; ++i)
                sensorValues.Add(_sensorValuesFactory.BuildSensorValue(type));

            sensorValues.ForEach(_monitoringCore.AddSensorValue);

            await FullSeveralSensorValuesTestAsync(sensorValues,
                                                   _valuesCache.GetValues,
                                                   _databaseAdapterManager.DatabaseAdapter.GetAllSensorHistory,
                                                   _databaseAdapterManager.DatabaseAdapter.GetProductSensors);
        }


        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        [Trait("Category", "Random")]
        public async Task AddRandomSensorValuesTest(int count)
        {
            var sensorValues = GetRandomSensorValues(count);

            sensorValues.ForEach(_monitoringCore.AddSensorValue);

            await FullSeveralSensorValuesTestAsync(sensorValues,
                                                   _valuesCache.GetValues,
                                                   _databaseAdapterManager.DatabaseAdapter.GetAllSensorHistory,
                                                   _databaseAdapterManager.DatabaseAdapter.GetProductSensors);
        }


        [Theory]
        [InlineData(SensorType.BooleanSensor)]
        [InlineData(SensorType.IntSensor)]
        [InlineData(SensorType.DoubleSensor)]
        [InlineData(SensorType.StringSensor)]
        [InlineData(SensorType.IntegerBarSensor)]
        [InlineData(SensorType.DoubleBarSensor)]
        [Trait("Category", "UnitedSensorValues One")]
        public async Task AddUnitedSensorValueTest(SensorType sensorType)
        {
            var unitedValue = _sensorValuesFactory.BuildUnitedSensorValue(sensorType);

            _monitoringCore.AddSensorsValues(new List<UnitedSensorValue>() { unitedValue });

            await FullSeveralSensorValuesTestAsync(new List<SensorValueBase>() { unitedValue },
                                                   _valuesCache.GetValues,
                                                   _databaseAdapterManager.DatabaseAdapter.GetAllSensorHistory,
                                                   _databaseAdapterManager.DatabaseAdapter.GetProductSensors);
        }

        [Theory]
        [InlineData(SensorType.IntegerBarSensor)]
        [InlineData(SensorType.DoubleBarSensor)]
        [Trait("Category", "UnitedBarSensorValues One")]
        public void AddUnitedBarSensorValueTest(SensorType type)
        {
            var unitedValue = _sensorValuesFactory.BuildUnitedSensorValue(type, isMinEndTime: true);

            _monitoringCore.AddSensorsValues(new List<UnitedSensorValue>() { unitedValue });

            var lastBarValue = _barStorage.GetLastValue(_databaseAdapterManager.TestProduct.Name, unitedValue.Path);

            Assert.Equal(_databaseAdapterManager.TestProduct.Name, lastBarValue.ProductName);
            Assert.Equal(SensorValuesTester.GetSensorValueType(unitedValue), lastBarValue.ValueType);
            SensorValuesTester.TestBarSensorFromUnitedSensor(unitedValue, lastBarValue.Value);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        [Trait("Category", "UnitedSensorValues Several Random")]
        public async Task AddRandomUnitedSensorValuesTest(int count)
        {
            var unitedValues = GetRandomUnitedSensors(count);

            _monitoringCore.AddSensorsValues(unitedValues.Cast<UnitedSensorValue>().ToList());

            await FullSeveralSensorValuesTestAsync(unitedValues,
                                                   _valuesCache.GetValues,
                                                   _databaseAdapterManager.DatabaseAdapter.GetAllSensorHistory,
                                                   _databaseAdapterManager.DatabaseAdapter.GetProductSensors);
        }


        private async Task FullSensorValueTestAsync(SensorValueBase sensorValue, GetValuesFromCache getCachedValues,
            GetSensorHistoryData getSensorHistoryData, GetSensorInfo getSensorInfo)
        {
            await Task.Delay(100);

            TestSensorDataFromCache(sensorValue, getCachedValues);
            TestSensorHistoryDataFromDB(sensorValue, getSensorHistoryData);
            TestSensorInfoFromDB(sensorValue, getSensorInfo);
        }

        private async Task FullSeveralSensorValuesTestAsync(List<SensorValueBase> sensorValues,
           GetValuesFromCache getCachedValues, GetAllSensorHistoryData getAllSensorHistoryData, GetAllSensorInfo getAllSensorInfo, int? time = null)
        {
            await Task.Delay(sensorValues.Count);

            var sensorsDict = sensorValues.GroupBy(s => s.Path)
                                          .ToDictionary(s => s.Key, s => s.ToList());

            TestSeveralSensorDataFromCache(sensorsDict, getCachedValues);
            TestSeveralSensorHistoryDataFromDB(sensorsDict, getAllSensorHistoryData);
            TestSeveralSensorInfoFromDB(sensorsDict, getAllSensorInfo);
        }

        private void TestSensorDataFromCache(SensorValueBase sensorValue, GetValuesFromCache getCachedValues)
        {
            var sensorDataFromCache = getCachedValues?.Invoke(new List<string>(1) { _databaseAdapterManager.TestProduct.Name })
                                                     ?.FirstOrDefault(s => s.Path == sensorValue.Path);

            _sensorValuesTester.TestSensorDataFromCache(sensorValue, sensorDataFromCache);
        }

        private void TestSensorHistoryDataFromDB(SensorValueBase sensorValue, GetSensorHistoryData getSensorHistoryData)
        {
            var sensorHistoryDataFromDB = getSensorHistoryData?.Invoke(_databaseAdapterManager.TestProduct.Name, sensorValue.Path);

            SensorValuesTester.TestSensorHistoryDataFromDB(sensorValue, sensorHistoryDataFromDB);
        }

        private void TestSensorInfoFromDB(SensorValueBase sensorValue, GetSensorInfo getSensorInfo)
        {
            var sensorInfoFromDB = getSensorInfo?.Invoke(_databaseAdapterManager.TestProduct.Name, sensorValue.Path);

            _sensorValuesTester.TestSensorInfoFromDB(sensorValue, sensorInfoFromDB);
        }

        private void TestSeveralSensorDataFromCache(Dictionary<string, List<SensorValueBase>> sensorValues,
            GetValuesFromCache getValuesFromCache)
        {
            var cache = getValuesFromCache?.Invoke(new List<string>(1) { _databaseAdapterManager.TestProduct.Name })
                                          ?.ToDictionary(s => s.Path);

            foreach (var sensors in sensorValues)
                _sensorValuesTester.TestSensorDataFromCache(sensors.Value.LastOrDefault(), cache[sensors.Key]);
        }

        private void TestSeveralSensorHistoryDataFromDB(Dictionary<string, List<SensorValueBase>> sensorValues,
            GetAllSensorHistoryData getAllSensorHistoryData)
        {
            foreach (var sensors in sensorValues)
            {
                var datas = getAllSensorHistoryData?.Invoke(_databaseAdapterManager.TestProduct.Name, sensors.Key);
                for (int i = 0; i < sensors.Value.Count; ++i)
                    SensorValuesTester.TestSensorHistoryDataFromDB(sensors.Value[i], datas[i]);
            }
        }

        private void TestSeveralSensorInfoFromDB(Dictionary<string, List<SensorValueBase>> sensorValues,
            GetAllSensorInfo getAllSensorInfo)
        {
            var infos = getAllSensorInfo(_databaseAdapterManager.TestProduct).ToDictionary(s => s.Path);

            foreach (var sensors in sensorValues)
                for (int i = 0; i < sensors.Value.Count; ++i)
                    _sensorValuesTester.TestSensorInfoFromDB(sensors.Value[i], infos[sensors.Key]);
        }

        private List<SensorValueBase> GetRandomSensorValues(int size)
        {
            var sensorValues = new List<SensorValueBase>(size);
            for (int i = 0; i < size; ++i)
                sensorValues.Add(_sensorValuesFactory.BuildRandomSensorValue());

            return sensorValues;
        }

        private List<SensorValueBase> GetRandomUnitedSensors(int size)
        {
            var sensorValues = new List<SensorValueBase>(size);
            for (int i = 0; i < size; ++i)
                sensorValues.Add(_sensorValuesFactory.BuildRandomUnitedSensorValue());

            return sensorValues;
        }
    }
}