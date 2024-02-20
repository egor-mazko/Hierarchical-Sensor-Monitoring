﻿using HSMServer.Datasources.Aggregators;
using System;
using System.Numerics;
using HSMServer.Dashboards;

namespace HSMServer.Datasources
{
    public abstract class BaseChartValue
    {
        private static long _idCounter = 0L;


        public long Id { get; } = _idCounter++;

        public DateTime Time { get; protected set; }

        public string Tooltip { get; protected set; }

        
        protected internal abstract object Filter(RangeSettings settings);
    }


    public abstract class BaseChartValue<T> : BaseChartValue
    {
        public T Value { get; protected set; }
    }


    public sealed class LineChartValue<T> : BaseChartValue<T> where T : INumber<T>
    {
        internal void SetNewState(ref readonly LinePointState<T> state)
        {
            Value = state.Value;
            Time = state.Time;

            var count = state.Count;

            Tooltip = count > 1 ? $"Aggregated ({count}) values" : string.Empty;
        }
        
        protected internal override object Filter(RangeSettings settings)
        {
            Tooltip = $"""
                      Original value: {Value}<br>
                      Comment: {Tooltip}
                      """;
            
            var checkedValue = double.CreateChecked(Value);
            if (settings.MaxY.CompareTo(checkedValue) < 0)
                Value = T.CreateChecked(settings.MaxY);
          
            if (settings.MinY.CompareTo(checkedValue) > 0)
                Value = T.CreateChecked(settings.MinY);
            
            return this;
        }
    }
}