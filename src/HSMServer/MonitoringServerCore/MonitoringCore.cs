﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HSMCommon.Certificates;
using HSMCommon.Model;
using HSMCommon.Model.SensorsData;
using HSMSensorDataObjects;
using HSMSensorDataObjects.FullDataObject;
using HSMSensorDataObjects.TypedDataObject;
using HSMServer.Authentication;
using HSMServer.Configuration;
using HSMServer.DataLayer;
using HSMServer.DataLayer.Model;
using HSMServer.Model;
using HSMServer.Products;
using NLog;
using RSAParameters = System.Security.Cryptography.RSAParameters;

namespace HSMServer.MonitoringServerCore
{
    public class MonitoringCore : IMonitoringCore
    {
        //#region IDisposable implementation

        //private bool _disposed;

        //// Implement IDisposable.
        //public void Dispose()
        //{
        //    Dispose(true);
        //    GC.SuppressFinalize(this);
        //}

        //protected virtual void Dispose(bool disposingManagedResources)
        //{
        //    // The idea here is that Dispose(Boolean) knows whether it is 
        //    // being called to do explicit cleanup (the Boolean is true) 
        //    // versus being called due to a garbage collection (the Boolean 
        //    // is false). This distinction is useful because, when being 
        //    // disposed explicitly, the Dispose(Boolean) method can safely 
        //    // execute code using reference type fields that refer to other 
        //    // objects knowing for sure that these other objects have not been 
        //    // finalized or disposed of yet. When the Boolean is false, 
        //    // the Dispose(Boolean) method should not execute code that 
        //    // refer to reference type fields because those objects may 
        //    // have already been finalized."

        //    if (!_disposed)
        //    {
        //        if (disposingManagedResources)
        //        {

        //        }

        //        _disposed = true;
        //    }
        //}

        //// Use C# destructor syntax for finalization code.
        //~MonitoringCore()
        //{
        //    // Simply call Dispose(false).
        //    Dispose(false);
        //}

        //#endregion

        private readonly IDatabaseClass _database;
        private readonly IBarSensorsStorage _barsStorage;
        private readonly IMonitoringQueueManager _queueManager;
        private readonly UserManager _userManager;
        private readonly CertificateManager _certificateManager;
        private readonly IProductManager _productManager;
        private readonly Logger _logger;
        public readonly char[] _pathSeparator = new[] { '/' };

        public MonitoringCore(IDatabaseClass database, UserManager userManager, IBarSensorsStorage barsStorage,
            IProductManager productManager)
        {
            _logger = LogManager.GetCurrentClassLogger();
            _database = database;
            _barsStorage = barsStorage;
            _barsStorage.IncompleteBarOutdated += BarsStorage_IncompleteBarOutdated;
            _certificateManager = new CertificateManager();
            _userManager = userManager;
            _queueManager = new MonitoringQueueManager();
            _productManager = productManager;
            _logger.Debug("Monitoring core initialized");
        }

        #region Sensor saving
        private void BarsStorage_IncompleteBarOutdated(object sender, ExtendedBarSensorData e)
        {
            switch (e.ValueType)
            {
                case SensorType.IntegerBarSensor:
                {
                    var typedValue = e.Value as IntBarSensorValue;
                    typedValue.EndTime = DateTime.Now;
                    SensorDataObject obj = Converter.ConvertToDatabase(typedValue, e.TimeCollected);
                    SaveSensorValue(obj, e.ProductName);
                    break;
                }
                case SensorType.DoubleBarSensor:
                {
                    var typedValue = e.Value as DoubleBarSensorValue;
                    typedValue.EndTime = DateTime.Now;
                    SensorDataObject obj = Converter.ConvertToDatabase(typedValue, e.TimeCollected);
                    SaveSensorValue(obj, e.ProductName);
                    break;
                }
            }
        }
        /// <summary>
        /// Simply save the given sensorValue
        /// </summary>
        /// <param name="dataObject"></param>
        /// <param name="productName"></param>
        private void SaveSensorValue(SensorDataObject dataObject, string productName)
        {
            _productManager.AddSensorIfNotRegistered(productName, dataObject.Path);
            Task.Run(() => _database.WriteSensorData(dataObject, productName));
        }

        /// <summary>
        /// Use this method for sensors, for which only last value is stored
        /// </summary>
        /// <param name="dataObject"></param>
        /// <param name="productName"></param>
        private void SaveOneValueSensorValue(SensorDataObject dataObject, string productName)
        {
            _productManager.AddSensorIfNotRegistered(productName, dataObject.Path);
            Task.Run(() => _database.WriteOneValueSensorData(dataObject, productName));
        }
        private async Task<bool> SaveSensorValueAsync(SensorDataObject dataObject, string productName)
        {
            try
            {
                if (!_productManager.IsSensorRegistered(productName, dataObject.Path))
                {
                    _productManager.AddSensor(productName, dataObject.Path);
                }

                await Task.Run(() => _database.WriteSensorData(dataObject, productName));
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
            
        }
        //private void SaveSensorValue(SensorData updateMessage, string productName, DateTime originalTime)
        //{
        //    SensorDataObject obj = Converter.ConvertToDatabase(updateMessage, originalTime);

        //    if (!_productManager.IsSensorRegistered(productName, obj.Path))
        //    {
        //        //_productManager.AddSensor(new SensorInfo() { Path = updateMessage.Path, ProductName = productName, SensorName = updateMessage.Name });
        //        _productManager.AddSensor(productName, obj.Path);
        //    }

        //    //ThreadPool.QueueUserWorkItem(_ => DatabaseClass.Instance.WriteSensorData(obj, productName));
        //    Task.Run(() => DatabaseClass.Instance.WriteSensorData(obj, productName));
        //}

 

        public async Task<bool> AddSensorValueAsync(BoolSensorValue value)
        {
            string productName = _productManager.GetProductNameByKey(value.Key);

            DateTime timeCollected = DateTime.Now;
            SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
            _queueManager.AddSensorData(updateMessage);

            SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
            return await SaveSensorValueAsync(dataObject, productName);
        }

        public void AddSensorsValues(IEnumerable<CommonSensorValue> values)
        {
            var commonSensorValues = values.ToList();
            foreach (var value in commonSensorValues)
            {
                if (value == null)
                {
                    _logger.Warn("Received null value in list!");
                    continue;
                }
                switch (value.SensorType)
                {
                    case SensorType.IntegerBarSensor:
                    {
                        var typedValue = Converter.GetIntBarSensorValue(value.TypedValue);
                        AddSensorValue(typedValue);
                        break;
                    }
                    case SensorType.DoubleBarSensor:
                    {
                        var typedValue = Converter.GetDoubleBarSensorValue(value.TypedValue);
                        AddSensorValue(typedValue);
                        break;
                    }
                    case SensorType.DoubleSensor:
                    {
                        var typedValue = Converter.GetDoubleSensorValue(value.TypedValue);
                        AddSensorValue(typedValue);
                        break;
                    }
                    case SensorType.IntSensor:
                    {
                        var typedValue = Converter.GetIntSensorValue(value.TypedValue);
                        AddSensorValue(typedValue);
                        break;
                    }
                    case SensorType.BooleanSensor:
                    {
                        var typedValue = Converter.GetBoolSensorValue(value.TypedValue);
                        AddSensorValue(typedValue);
                        break;
                    }
                    case SensorType.StringSensor:
                    {
                        var typedValue = Converter.GetStringSensorValue(value.TypedValue);
                        AddSensorValue(typedValue);
                        break;
                    }
                }
            }
        }

        public void AddSensorValue(BoolSensorValue value)
        {
            try
            {
                string productName = _productManager.GetProductNameByKey(value.Key);

                DateTime timeCollected = DateTime.Now;
                SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
                _queueManager.AddSensorData(updateMessage);

                SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
                ThreadPool.QueueUserWorkItem(_ => SaveSensorValue(dataObject, productName));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to add value for sensor '{value?.Path}'");
            }
        }
        public void AddSensorValue(IntSensorValue value)
        {
            try
            {
                string productName = _productManager.GetProductNameByKey(value.Key);

                DateTime timeCollected = DateTime.Now;
                SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
                _queueManager.AddSensorData(updateMessage);

                SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
                ThreadPool.QueueUserWorkItem(_ => SaveSensorValue(dataObject, productName));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to add value for sensor '{value?.Path}'");
            }
        }

        public void AddSensorValue(DoubleSensorValue value)
        {
            try
            {
                string productName = _productManager.GetProductNameByKey(value.Key);

                DateTime timeCollected = DateTime.Now;
                SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
                _queueManager.AddSensorData(updateMessage);

                SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
                ThreadPool.QueueUserWorkItem(_ => SaveSensorValue(dataObject, productName));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to add value for sensor '{value?.Path}'");
            }
        }

        public void AddSensorValue(StringSensorValue value)
        {
            try
            {
                string productName = _productManager.GetProductNameByKey(value.Key);

                DateTime timeCollected = DateTime.Now;
                SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
                _queueManager.AddSensorData(updateMessage);

                SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
                ThreadPool.QueueUserWorkItem(_ => SaveSensorValue(dataObject, productName));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to add value for sensor '{value?.Path}'");
            }
        }

        public void AddSensorValue(FileSensorValue value)
        {
            try
            {
                string productName = _productManager.GetProductNameByKey(value.Key);

                DateTime timeCollected = DateTime.Now;
                SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
                _queueManager.AddSensorData(updateMessage);

                SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
                ThreadPool.QueueUserWorkItem(_ => SaveOneValueSensorValue(dataObject, productName));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to add value for sensor '{value?.Path}'");
            }
        }
        public void AddSensorValue(IntBarSensorValue value)
        {
            try
            {
                string productName = _productManager.GetProductNameByKey(value.Key);

                DateTime timeCollected = DateTime.Now;
                SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
                _queueManager.AddSensorData(updateMessage);

                //Skip 
                if (value.EndTime == DateTime.MinValue)
                {
                    _barsStorage.Add(value, updateMessage.Product, timeCollected);
                    return;
                }
                
                _barsStorage.Remove(productName, value.Path);
                SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
                ThreadPool.QueueUserWorkItem(_ => SaveSensorValue(dataObject, productName));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to add value for sensor '{value?.Path}'");
            }
        }

        public void AddSensorValue(DoubleBarSensorValue value)
        {
            try
            {
                string productName = _productManager.GetProductNameByKey(value.Key);

                DateTime timeCollected = DateTime.Now;
                SensorData updateMessage = Converter.Convert(value, productName, timeCollected);
                _queueManager.AddSensorData(updateMessage);

                if (value.EndTime == DateTime.MinValue)
                {
                    _barsStorage.Add(value, updateMessage.Product, timeCollected);
                    return;
                }

                _barsStorage.Remove(productName, value.Path);
                SensorDataObject dataObject = Converter.ConvertToDatabase(value, timeCollected);
                ThreadPool.QueueUserWorkItem(_ => SaveSensorValue(dataObject, productName));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to add value for sensor '{value?.Path}'");
            }
        }

        #endregion

        public List<SensorData> GetSensorUpdates(User user)
        {
            return _queueManager.GetUserUpdates(user);
        }

        public List<SensorData> GetSensorsTree(User user)
        {
            if (!_queueManager.IsUserRegistered(user))
            {
                _queueManager.AddUserSession(user);
            }

            List<SensorData> result = new List<SensorData>();
            var productsList = _productManager.Products;
            foreach (var product in productsList)
            {
                var sensorsList = _productManager.GetProductSensors(product.Name);
                foreach (var sensorPath in sensorsList)
                {
                    var lastVal = _database.GetLastSensorValue(product.Name, sensorPath);
                    if (lastVal != null)
                    {
                        result.Add(Converter.Convert(lastVal, product.Name));
                    }
                }
            }

            return result;
        }

        public List<SensorHistoryData> GetSensorHistory(User user, GetSensorHistoryModel model)
        {
            return GetSensorHistory(user, model.Path, model.Product, model.Amount);
        }
        public List<SensorHistoryData> GetSensorHistory(User user, string path, string product, long n = -1)
        {
            List<SensorHistoryData> historyList = new List<SensorHistoryData>();
            List<SensorDataObject> dataList = _database.GetSensorDataHistory(product, path, n);
            //_logger.Info($"GetSensorHistory: {dataList.Count} history items found for sensor {getMessage.Path} at {DateTime.Now:F}");
            dataList.Sort((a, b) => a.TimeCollected.CompareTo(b.TimeCollected));
            if (n != -1)
            {
                dataList = dataList.TakeLast((int)n).ToList();
            }

            var finalList = dataList.Select(Converter.Convert).ToList();
            var lastValue = _barsStorage.GetLastValue(product, path);
            if (lastValue != null)
            {
                finalList.Add(Converter.Convert(lastValue));
            }

            historyList.AddRange(n == -1 ? finalList : finalList.TakeLast((int) n));
            return historyList;
        }

        public string GetFileSensorValue(User user, string product, string path)
        {
            List<SensorDataObject> historyList = _database.GetSensorDataHistory(product, path, 1);
            if (historyList.Count < 1)
            {
                return string.Empty;
            }

            var typedData = JsonSerializer.Deserialize<FileSensorData>(historyList[0].TypedData);
            if (typedData == null)
            {
                return string.Empty;
            }

            return typedData.FileContent;
        }

        public string GetFileSensorValueExtension(User user, string product, string path)
        {
            string result = string.Empty;
            List<SensorDataObject> historyList = _database.GetSensorDataHistory(product, path, 1);
            if (historyList.Count > 0)
            {
                var typedData = JsonSerializer.Deserialize<FileSensorData>(historyList[0].TypedData);
                if (typedData != null)
                {
                    result = typedData.Extension;
                }
            }

            return result;
        }

        public List<Product> GetProductsList(User user)
        {
            var products = _productManager.Products;

            return products;
        }

        public bool AddProduct(User user, string productName, out Product product, out string error)
        {
            product = default(Product);
            error = string.Empty;
            bool result;
            try
            {
                _productManager.AddProduct(productName);
                product = _productManager.GetProductByName(productName);
                result = true;
            }
            catch (Exception e)
            {
                result = false;
                error = e.Message;
                _logger.Error(e, $"Failed to add new product name = {productName}, user = {user.UserName}");
            }

            return result;
        }

        public bool RemoveProduct(User user, string productName, out Product product, out string error)
        {
            product = default(Product);
            error = string.Empty;
            bool result;
            try
            {
                product = _productManager.GetProductByName(productName);
                _productManager.RemoveProduct(productName);
                result = true;
            }
            catch (Exception e)
            {
                result = false;
                error = e.Message;
                _logger.Error(e, $"Failed to remove product name = {productName}, user = {user.UserName}");
            }

            return result;
        }

        public (X509Certificate2, X509Certificate2) SignClientCertificate(User user, string subject, string commonName,
            RSAParameters rsaParameters)
        {
            (X509Certificate2, X509Certificate2) result;
            var rsa = RSA.Create(rsaParameters);

            X509Certificate2 clientCert =
                CertificatesProcessor.CreateAndSignCertificate(subject, rsa, Config.CACertificate);

            string fileName = $"{commonName}.crt";
            _certificateManager.InstallClientCertificate(clientCert);
            _certificateManager.SaveClientCertificate(clientCert, fileName);
            _userManager.AddNewUser(commonName, clientCert.Thumbprint, fileName);
            result.Item1 = clientCert;
            result.Item2 = Config.CACertificate;
            return result;
        }

        public ClientVersionModel GetLastAvailableClientVersion()
        {
            return Config.LastAvailableClientVersion;
        }

        #region Sub-methods


        #endregion

        public void Dispose()
        {
            _barsStorage?.Dispose();
            _database?.Dispose();
        }
    }
}