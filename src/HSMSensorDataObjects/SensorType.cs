﻿using System.Runtime.Serialization;

namespace HSMSensorDataObjects
{
    [DataContract]
    public enum SensorType
    {

        /// <summary>
        /// Simple sensor which collects data of boolean type and sends the collected data instantly
        /// </summary>
        [EnumMember]
        BooleanSensor = 0,

        /// <summary>
        /// Simple sensor which collects data of integer type and sends the collected data instantly
        /// </summary>
        [EnumMember]
        IntSensor = 1,

        /// <summary>
        /// Simple sensor which collects data of double type and sends the collected data instantly
        /// </summary>
        [EnumMember]
        DoubleSensor = 2,

        /// <summary>
        /// Simple sensor which collects data of string type and sends the collected data instantly
        /// </summary>
        [EnumMember]
        StringSensor = 3,

        /// <summary>
        /// The sensor collects integer values during the 5-minute period and creates the object with
        /// min, max and mean values, the first value time, the last value time, and values count.
        /// Once the 5-minute period is over, the data is sent to the server and sensor starts collecting new bar.
        /// All values must be integers
        /// </summary>
        [EnumMember]
        IntegerBarSensor = 4,

        /// <summary>
        /// The sensor collects integer values during the 5-minute period and creates the object with
        /// min, max and mean values, the first value time, the last value time, and values count.
        /// Once the 5-minute period is over, the data is sent to the server and sensor starts collecting new bar.
        /// All values must have type double
        /// </summary>
        [EnumMember]
        DoubleBarSensor = 5,
        
        /// <summary>
        /// The sensor has a file data in its' value, the file data is stored in the database like the other sensors' data
        /// Only the last value for file sensors is stored due to possible size of files
        /// May be used to store different reports, graphs, etc.
        /// Data object also includes file extension, so the client app automatically opens the file using a proper program
        /// </summary>
        [EnumMember]
        FileSensor = 6
    }
}