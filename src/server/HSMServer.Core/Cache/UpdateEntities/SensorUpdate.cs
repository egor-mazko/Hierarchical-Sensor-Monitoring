﻿using HSMServer.Core.Model;
using HSMServer.Core.Model.Policies;
using System;
using System.Collections.Generic;

namespace HSMServer.Core.Cache.UpdateEntities
{
    /// <summary>
    /// If properties are null - there's no updates for that properties
    /// </summary>
    public record SensorUpdate : BaseNodeUpdate, IUpdateComparer<BaseSensorModel, SensorUpdate>
    {
        public SensorState? State { get; init; }

        public Integration? Integration { get; init; }

        public DateTime? EndOfMutingPeriod { get; init; }

        public List<DataPolicyUpdate> DataPolicies { get; init; }
        
        public string GetComparisonString(BaseSensorModel entity, SensorUpdate update)
        {
            return "";
        }
    }


    public sealed record DataPolicyUpdate(
        Guid Id,
        string Property,
        PolicyOperation Operation,
        TargetValue Target,
        SensorStatus Status,
        string Template,
        string Icon
    );
}
