﻿using HSMCommon.Extensions;
using HSMServer.Core.Model;
using HSMServer.Dashboards;
using HSMServer.Datasources;
using HSMServer.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSMCommon.Extensions;
using HSMServer.Core;

namespace HSMServer.Model.Dashboards;

public class DatasourceViewModel
{
    private readonly PanelDatasource _panelSource;

    private static readonly List<PlottedProperty> _singleSensorProperties =
    [
        PlottedProperty.Value
    ];

    private static readonly List<PlottedProperty> _barSensorProperties =
    [
        PlottedProperty.Min,
        PlottedProperty.Mean,
        PlottedProperty.Max,
        PlottedProperty.Count,
    ];
    
    private static readonly List<PlottedProperty> _barEmaSensorProperties =
    [
        PlottedProperty.EmaMin,
        PlottedProperty.EmaMean,
        PlottedProperty.EmaMax,
        PlottedProperty.EmaCount,
    ];


    public List<SelectListItem> AvailableProperties { get; set; }

    public SensorInfoViewModel SensorInfo { get; set; }

    public List<object> Values { get; set; } = new();

    public PlottedProperty Property { get; set; }

    public SensorType Type { get; set; }

    public Guid SensorId { get; set; }

    public string Label { get; set; }

    public string Color { get; set; }

    public string Path { get; set; }

    public Unit? Unit { get; set; }

    public Guid Id { get; set; }

    public ChartType ChartType { get; set; }


    public DatasourceViewModel() { }

    public DatasourceViewModel(PanelDatasource source)
    {
        _panelSource = source;

        Id = source.Id;
        SensorId = source.SensorId;
        Color = source.Color.ToRGB();
        Label = source.Label;

        var sensor = source.Sensor;

        Path = sensor.FullPath;
        Type = sensor.Type;
        Unit = sensor.OriginalUnit;

        SensorInfo = new SensorInfoViewModel(Type, Type, Unit?.GetDisplayName());

        AvailableProperties = GetAvailableProperties(sensor);
        Property = source.Property;
    }

    public DatasourceViewModel(InitChartSourceResponse chartResponse, PanelDatasource source) : this(source)
    {
        ChartType = chartResponse.ChartType;
        Values = chartResponse.Values;
    }


    public async Task LoadDataFrom(DateTime? from)
    {
        var task = from is null ? _panelSource.Source.Initialize() : _panelSource.Source.Initialize(from.Value, DateTime.UtcNow);

        var response = await task;
        Values = response.Values;
        ChartType = response.ChartType;
    }


    private List<SelectListItem> GetAvailableProperties(BaseSensorModel sensor)
    {
        var properties = new List<PlottedProperty>(sensor switch
        {
            IntegerBarSensorModel or DoubleBarSensorModel => _barSensorProperties,
            _ => _singleSensorProperties
        });
        
        if (sensor.Statistics.HasEma())
            properties.AddRange(_barEmaSensorProperties);

        return properties.ToSelectedItems(k => k.GetDisplayName());
    }
}