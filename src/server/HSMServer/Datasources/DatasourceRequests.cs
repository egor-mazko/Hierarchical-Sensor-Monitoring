﻿using System.Collections.Generic;

namespace HSMServer.Datasources
{
    public sealed record InitChartSourceResponse
    {
        public List<BaseChartValue> Values { get; init; }

        public ChartType ChartType { get; init; }
    }


    public sealed record UpdateChartSourceResponse
    {
        public List<BaseChartValue> NewVisibleValues { get; init; }

        public long RemovedValuesCount { get; init; }
    }
}