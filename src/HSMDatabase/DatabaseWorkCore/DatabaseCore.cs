﻿using HSMDatabase.DatabaseInterface;
using HSMDatabase.Entity;
using HSMDatabase.EnvironmentDatabase;
using HSMDatabase.SensorsDatabase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HSMDatabase.DatabaseWorkCore
{
    public class DatabaseCore : IDatabaseCore
    {
        #region Singleton

        private static volatile DatabaseCore _instance;
        private static readonly object _singletonLockObj = new object();
        public static IDatabaseCore GetInstance()
        {
            return Instance;
        }

        private static DatabaseCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_singletonLockObj)
                    {
                        if (_instance == null)
                        {
                            _instance = new DatabaseCore();
                        }
                    }
                }

                return _instance;
            }
        }
        #endregion

        private readonly IEnvironmentDatabase _environmentDatabase;
        private readonly ITimeDatabaseDictionary _sensorsDatabases;
        internal const string DatabaseParentFolder = "Databases";
        private const string EnvironmentDatabaseName = "EnvironmentData";
        private DatabaseCore()
        {
            _environmentDatabase = new EnvironmentDatabaseWorker($"{DatabaseParentFolder}/{EnvironmentDatabaseName}");
            _sensorsDatabases = new TimeDatabaseDictionary(_environmentDatabase);
            OpenAllExistingSensorDatabases();
        }

        private void OpenAllExistingSensorDatabases()
        {
            List<string> databasesList = _environmentDatabase.GetMonitoringDatabases();
            foreach (var databaseName in databasesList)
            {
                GetDatesFromFolderName(databaseName, out DateTime from, out DateTime to);
                if (from != DateTime.MinValue && to != DateTime.MinValue)
                {
                    ISensorsDatabase database = new SensorsDatabaseWorker($"{DatabaseParentFolder}/{databaseName}", from, to);
                    _sensorsDatabases.AddDatabase(database);
                }
            }
        }

        #region Sensors methods

        public List<SensorDataEntity> GetAllSensorData(string productName, string path)
        {
            List<SensorDataEntity> result = new List<SensorDataEntity>();
            var databases = _sensorsDatabases.GetAllDatabases();
            foreach (var database in databases)
            {
                result.AddRange(database.GetAllSensorValues(productName, path));
            }

            return result;
        }

        public List<SensorDataEntity> GetSensorData(string productName, string path, DateTime from)
        {
            List<SensorDataEntity> result = new List<SensorDataEntity>();
            var databases = _sensorsDatabases.GetAllDatabases();
            foreach (var database in databases)
            {
                //Skip too old data
                if (database.DatabaseMaxTicks < from.Ticks)
                    continue;

                result.AddRange(database.GetSensorValuesFrom(productName, path, from));
            }

            result.RemoveAll(r => r.TimeCollected < from);
            return result;
        }

        public List<SensorDataEntity> GetSensorData(string productName, string path, DateTime from, DateTime to)
        {
            List<SensorDataEntity> result = new List<SensorDataEntity>();
            var databases = _sensorsDatabases.GetAllDatabases();
            foreach (var database in databases)
            {
                //Skip too old data
                if (database.DatabaseMaxTicks < from.Ticks)
                    continue;

                //Skip too new data
                if(database.DatabaseMinTicks > to.Ticks)
                    continue;

                result.AddRange(database.GetSensorValuesBetween(productName, path, from, to));
            }

            return result;
        }

        public long GetSensorSize(string productName, string path)
        {
            long size = 0L;
            var databases = _sensorsDatabases.GetAllDatabases();
            foreach (var database in databases)
            {
                size += database.GetSensorSize(productName, path);
            }

            return size;
        }

        public void AddSensorValue(SensorDataEntity entity, string productName)
        {
            var database = _sensorsDatabases.GetDatabase(entity.TimeCollected);
            database.PutSensorData(entity, productName);
        }

        public SensorDataEntity GetLatestSensorValue(string productName, string path)
        {
            List<ISensorsDatabase> databases = _sensorsDatabases.GetAllDatabases();
            foreach (var database in databases.AsEnumerable().Reverse())
            {
                var currentLatestValue = database.GetLatestSensorValue(productName, path);
                if (currentLatestValue != null)
                {
                    return currentLatestValue;
                }
            }

            return null;
        }
        
        #endregion

        #region Environment database : Sensor

        public SensorEntity GetSensorInfo(string productName, string path)
        {
            return _environmentDatabase.GetSensorInfo(productName, path);
        }

        public List<SensorEntity> GetProductSensors(string productName)
        {
            List<SensorEntity> sensors = new List<SensorEntity>();
            List<string> sensorPaths = _environmentDatabase.GetSensorsList(productName);
            foreach (var path in sensorPaths)
            {
                SensorEntity sensorEntity = _environmentDatabase.GetSensorInfo(productName, path);
                if(sensorEntity != null)
                    sensors.Add(sensorEntity);
            }

            return sensors;
        }

        public void RemoveSensor(string productName, string path)
        {
            //TODO: write this method
            _environmentDatabase.RemoveSensor(productName, path);
            _environmentDatabase.RemoveSensorFromList(productName, path);
            var databases = _sensorsDatabases.GetAllDatabases();
            foreach (var database in databases)
            {
                database.DeleteAllSensorValues(productName, path);
            }
        }

        public void AddSensor(SensorEntity entity)
        {
            _environmentDatabase.AddNewSensorToList(entity.ProductName, entity.Path);
            _environmentDatabase.AddSensor(entity);
        }
        #endregion

        #region Environment database : Product

        public void RemoveProduct(string productName)
        {
            _environmentDatabase.RemoveProductInfo(productName);
            _environmentDatabase.RemoveProductFromList(productName);
            var sensorsList = _environmentDatabase.GetSensorsList(productName);
            _environmentDatabase.RemoveSensorsList(productName);
            foreach (var sensor in sensorsList)
            {
                RemoveSensor(productName, sensor);
            }
        }

        public void UpdateProduct(ProductEntity productEntity)
        {
            AddProduct(productEntity);
        }

        public void AddProduct(ProductEntity productEntity)
        {
            _environmentDatabase.AddProductToList(productEntity.Name);
            _environmentDatabase.PutProductInfo(productEntity);
        }

        public ProductEntity GetProduct(string productName)
        {
            return _environmentDatabase.GetProductInfo(productName);
        }

        public List<ProductEntity> GetAllProducts()
        {
            List<ProductEntity> products = new List<ProductEntity>();
            var productNames = _environmentDatabase.GetProductsList();
            foreach (var productName in productNames)
            {
                var product = _environmentDatabase.GetProductInfo(productName);
                if (product != null)
                    products.Add(product);
            }

            return products;
        }
        
        #endregion

        #region Environment database : User

        public void AddUser(UserEntity user)
        {
            _environmentDatabase.AddUser(user);
        }

        public List<UserEntity> ReadUsers()
        {
            return _environmentDatabase.ReadUsers();
        }

        public void RemoveUser(UserEntity user)
        {
            _environmentDatabase.RemoveUser(user);
        }

        public List<UserEntity> ReadUsersPage(int page, int pageSize)
        {
            return _environmentDatabase.ReadUsersPage(page, pageSize);
        }

        #endregion

        #region Environment database : Configuration

        public ConfigurationEntity ReadConfigurationObject(string name)
        {
            return _environmentDatabase.ReadConfigurationObject(name);
        }

        public void WriteConfigurationObject(ConfigurationEntity obj)
        {
            _environmentDatabase.WriteConfigurationObject(obj);
        }

        public void RemoveConfigurationObject(string name)
        {
            _environmentDatabase.RemoveConfigurationObject(name);
        }

        #endregion

        #region Environment database : Ticket

        public RegisterTicketEntity ReadRegistrationTicket(Guid id)
        {
            return _environmentDatabase.ReadRegistrationTicket(id);
        }

        public void RemoveRegistrationTicket(Guid id)
        {
            _environmentDatabase.RemoveRegistrationTicket(id);
        }

        public void WriteRegistrationTicket(RegisterTicketEntity ticket)
        {
            _environmentDatabase.WriteRegistrationTicket(ticket);
        }

        #endregion

        #region Private methods

        

        private void GetDatesFromFolderName(string folder, out DateTime from, out DateTime to)
        {
            var splitResults = folder.Split('_');
            bool success1 = long.TryParse(splitResults[1], out long fromTicks);
            from = success1 ? new DateTime(fromTicks) : DateTime.MinValue;
            bool success2 = long.TryParse(splitResults[2], out long toTicks);
            to = success2 ? new DateTime(toTicks) : DateTime.MinValue;
        }
        #endregion
    }
}