﻿using HSMServer.Core.Model;
using HSMServer.Core.Model.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using HSMServer.Core.Journal;

namespace HSMServer.Core.Cache.UpdateEntities
{
    /// <summary>
    /// If properties are null - there's no updates for that properties
    /// </summary>
    public record SensorUpdate : BaseNodeUpdate
    {
        public List<PolicyUpdate> Policies { get; init; }


        public DateTime? EndOfMutingPeriod { get; init; }

        public Integration? Integration { get; init; }

        public SensorState? State { get; init; }
    }


    public sealed record PolicyConditionUpdate(
        PolicyOperation Operation,
        PolicyProperty Property,
        TargetValue Target,
        PolicyCombination Combination = PolicyCombination.And);


    public sealed class PolicyUpdate : IUpdateComparer<Policy, PolicyUpdate>, IPolicy<PolicyConditionUpdate>
    {
        public Guid Id { get; init; }

        public List<PolicyConditionUpdate> Conditions { get; init; }

        public TimeIntervalModel Sensitivity { get; set; }
        
        public SensorStatus Status { get; set; }

        public string Template { get; set; }

        public string Icon { get; set; }


        public PolicyUpdate(Guid id, List<PolicyConditionUpdate> conditions, TimeIntervalModel sensitivity, SensorStatus status, string template, string icon)
        {
            Id = id;
            Conditions = conditions;
            Sensitivity = sensitivity;
            Status = status;
            Template = template;
            Icon = icon;
        }

        public bool Compare(Policy entity, PolicyUpdate update, out string message)
        {
            var oldValue = GetValue(entity);
            var newValue = GetValue(update);

            string GetValue<U>(IPolicy<U> properties) where U : IPolicyCondition
            {
                return $"{string.Join(",", properties.Conditions.Select(x => $"{x.Property} {x.Operation} {x.Target.Value}"))} {properties.Icon} {properties.Template} {(properties.Status is SensorStatus.Ok ? string.Empty : properties.Status)}";
            }

            message = $"{JournalConstants.Alerts}{Environment.NewLine}Old: {oldValue}{Environment.NewLine}New: {newValue}";
            
            return oldValue != newValue;
        }
    }
}
