﻿using HSMServer.Core.Cache.UpdateEntities;
using HSMServer.Core.Model;
using HSMServer.Core.Model.NodeSettings;
using HSMServer.Core.Model.Policies;
using HSMServer.Core.TableOfChanges;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HSMServer.Core.Cache
{
    internal sealed class MigrationManager
    {
        private static readonly InitiatorInfo _softMigrator = InitiatorInfo.AsSoftSystemMigrator();
        private static readonly InitiatorInfo _migrator = InitiatorInfo.AsSystemMigrator();


        private static readonly HashSet<PolicyProperty> _numberToEmaSet =
        [
            PolicyProperty.Value,
            PolicyProperty.Mean,
            PolicyProperty.Max,
            PolicyProperty.Min,
            PolicyProperty.Count
        ];

        private static readonly HashSet<PolicyProperty> _emaToScheduleSet =
        [
            PolicyProperty.EmaValue,
            PolicyProperty.EmaMean,
            PolicyProperty.EmaMax,
            PolicyProperty.EmaMin,
            PolicyProperty.EmaCount,
        ];

        private static readonly PolicyUpdate _timeInGcPolicy = new()
        {
            Conditions = [new PolicyConditionUpdate(PolicyOperation.GreaterThan, PolicyProperty.EmaMean, new TargetValue(TargetType.Const, "20"))],
            Destination = new PolicyDestinationUpdate(),
            Icon = "⚠",
            Initiator = _migrator,
            Schedule = GetDefaultScheduleUpdate(),
            Template = "[$product]$path $property $operation $target $unit",
        };


        internal static IEnumerable<SensorUpdate> GetMigrationUpdates(List<BaseSensorModel> sensors)
        {
            foreach (var sensor in sensors)
            {
                if (IsDefaultSensor(sensor))
                {
                    if (IsNumberSensor(sensor.Type))
                    {
                        if (TryBuildNumberToEmaMigration(sensor, out var updateDefault))
                            yield return updateDefault;

                        if (TryBuildNumberToScheduleMigration(sensor, out updateDefault))
                            yield return updateDefault;

                        if (TryBuildTimeInGcSensorMigration(sensor, out updateDefault))
                            yield return updateDefault;
                    }

                    if (IsBoolSensor(sensor.Type) && TryMigrateServiceAliveTtlToSchedule(sensor, out var updateTtl))
                        yield return updateTtl;
                }

                if (TryMigratePolicyDestinationToDefaultChat(sensor, out var update))
                    yield return update;

                if (TryMigrateTTLPolicyDestinationToDefaultChat(sensor, out update))
                    yield return update;

                if (TryMigrateSensorDefaultChatToParent(sensor, out update))
                    yield return update;
            }
        }


        private static bool TryMigratePolicyDestinationToDefaultChat(BaseSensorModel sensor, out SensorUpdate update)
        {
            static bool IsTarget(Policy policy) => policy.Destination.IsNotInitialized;

            static PolicyUpdate Migration(PolicyUpdate update) => ToDefaultChatDestination(update);

            return TryMigratePolicy(sensor, IsTarget, Migration, out update);
        }

        private static bool TryMigrateTTLPolicyDestinationToDefaultChat(BaseSensorModel sensor, out SensorUpdate update)
        {
            static bool IsTarget(Policy policy) => policy.Destination.IsNotInitialized;

            static PolicyUpdate Migration(PolicyUpdate update) => ToDefaultChatDestination(update);

            return TryMigrateTtlPolicy(sensor, IsTarget, Migration, out update);
        }

        private static bool TryMigrateServiceAliveTtlToSchedule(BaseSensorModel sensor, out SensorUpdate update)
        {
            bool IsTarget(Policy policy) => sensor.DisplayName == "Service alive" && !policy.Schedule.IsActive;

            static PolicyUpdate Migration(PolicyUpdate update) =>
                update with { Schedule = GetDefaultScheduleUpdate(false) };

            return TryMigrateTtlPolicy(sensor, IsTarget, Migration, out update);
        }

        private static bool TryBuildNumberToEmaMigration(BaseSensorModel sensor, out SensorUpdate update)
        {
            static bool IsTarget(Policy policy) => IsTargetPolicy(policy, _numberToEmaSet);

            static PolicyUpdate Migration(PolicyUpdate update)
            {
                var conditions = update.Conditions[0];

                update.Conditions[0] = conditions with
                {
                    Property = conditions.Property switch
                    {
                        PolicyProperty.Value => PolicyProperty.EmaValue,
                        PolicyProperty.Mean => PolicyProperty.EmaMean,
                        PolicyProperty.Max => PolicyProperty.EmaMax,
                        PolicyProperty.Min => PolicyProperty.EmaMin,
                        PolicyProperty.Count => PolicyProperty.EmaCount,
                        _ => conditions.Property,
                    }
                };

                return update;
            }


            var result = TryMigratePolicy(sensor, IsTarget, Migration, out update);

            if (result)
                update = update with { Statistics = StatisticsOptions.EMA };

            return result;
        }

        private static bool TryBuildNumberToScheduleMigration(BaseSensorModel sensor, out SensorUpdate update)
        {
            static bool IsTarget(Policy policy) => IsTargetPolicy(policy, _emaToScheduleSet) && !policy.UseScheduleManagerLogic;

            static PolicyUpdate Migration(PolicyUpdate update) =>
                update with { Schedule = GetDefaultScheduleUpdate() };

            return TryMigratePolicy(sensor, IsTarget, Migration, out update);
        }

        private static bool TryBuildTimeInGcSensorMigration(BaseSensorModel sensor, out SensorUpdate update)
        {
            static bool IsTarget(BaseSensorModel sensor) => sensor.DisplayName == "Time in GC" && !sensor.Statistics.HasEma();


            var result = TryAddPolicy(sensor, IsTarget, _timeInGcPolicy, out update);

            if (result)
                update = update with { Statistics = StatisticsOptions.EMA };

            return result;
        }

        private static bool TryMigratePolicy(BaseSensorModel sensor, Predicate<Policy> isTarget, Func<PolicyUpdate, PolicyUpdate> migrator, out SensorUpdate sensorUpdate)
        {
            var alerts = new List<PolicyUpdate>();
            var hasMigrations = false;

            foreach (var policy in sensor.Policies)
            {
                var update = ToUpdate(policy);

                if (isTarget(policy))
                {
                    update = migrator(update);
                    hasMigrations = true;
                }

                alerts.Add(update);
            }

            sensorUpdate = new SensorUpdate()
            {
                Id = sensor.Id,
                Policies = alerts,
                Initiator = _migrator,
            };

            return hasMigrations;
        }

        private static bool TryAddPolicy(BaseSensorModel sensor, Predicate<BaseSensorModel> isTarget, PolicyUpdate policyToAdd, out SensorUpdate sensorUpdate)
        {
            static bool IsTargetPolicy(Policy policy) => false;

            static PolicyUpdate ExistingPoliciesMigration(PolicyUpdate update) => update;


            sensorUpdate = null;

            if (isTarget(sensor))
            {
                TryMigratePolicy(sensor, IsTargetPolicy, ExistingPoliciesMigration, out sensorUpdate);

                sensorUpdate.Policies.Add(policyToAdd);

                return true;
            }

            return false;
        }

        private static bool TryMigrateTtlPolicy(BaseSensorModel sensor, Predicate<Policy> isTarget, Func<PolicyUpdate, PolicyUpdate> migrator, out SensorUpdate sensorUpdate)
        {
            var ttl = sensor.Policies.TimeToLive;
            var needMigration = isTarget(ttl);

            sensorUpdate = !needMigration ? null : new SensorUpdate()
            {
                Id = sensor.Id,
                TTLPolicy = migrator(ToUpdate(ttl)),
                Initiator = _migrator,
            };

            return needMigration;
        }

        private static bool IsTargetPolicy(Policy policy, HashSet<PolicyProperty> targetProperties)
        {
            if (policy.Conditions.Count == 1)
            {
                var condition = policy.Conditions[0];

                return targetProperties.Contains(condition.Property);
            }

            return false;
        }

        private static bool TryMigrateSensorDefaultChatToParent(BaseSensorModel sensor, out SensorUpdate update)
        {
            if (sensor.Settings.DefaultChats.CurValue.IsNotInitialized)
            {
                update = new SensorUpdate
                {
                    Id = sensor.Id,
                    DefaultChats = new PolicyDestinationSettings(DefaultChatsMode.FromParent),
                    Initiator = _softMigrator,
                };

                return true;
            }

            update = null;

            return false;
        }


        private static bool IsDefaultSensor(BaseSensorModel sensor) => IsComputerSensor(sensor) || IsModuleSensor(sensor);

        private static bool IsComputerSensor(BaseSensorModel sensor) => sensor.Path.Contains(".computer");

        private static bool IsModuleSensor(BaseSensorModel sensor) => sensor.Path.Contains(".module");


        private static bool IsNumberSensor(SensorType type) => type.IsBar() || type is SensorType.Integer or SensorType.Double or SensorType.Rate;

        private static bool IsBoolSensor(SensorType type) => type is SensorType.Boolean;


        private static PolicyUpdate ToUpdate(Policy policy) =>
            new()
            {
                Conditions = policy.Conditions.Select(ToUpdate).ToList(),
                Destination = ToUpdate(policy.Destination),
                Schedule = ToUpdate(policy.Schedule),

                ConfirmationPeriod = policy.ConfirmationPeriod,
                Id = policy.Id,
                Status = policy.Status,
                Template = policy.Template,
                IsDisabled = policy.IsDisabled,
                Icon = policy.Icon,

                Initiator = _migrator,
            };

        private static PolicyScheduleUpdate ToUpdate(PolicySchedule schedule) =>
            new()
            {
                InstantSend = schedule.InstantSend,
                RepeatMode = schedule.RepeatMode,
                Time = schedule.Time,
            };

        private static PolicyDestinationUpdate ToUpdate(PolicyDestination destination) => new(destination);

        private static PolicyConditionUpdate ToUpdate(PolicyCondition condition) =>
            new()
            {
                Operation = condition.Operation,
                Target = condition.Target,
                Property = condition.Property
            };


        private static PolicyScheduleUpdate GetDefaultScheduleUpdate(bool instantSend = true) => new()
        {
            RepeatMode = AlertRepeatMode.Hourly,
            InstantSend = instantSend,
            Time = new DateTime(1, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        private static PolicyUpdate ToDefaultChatDestination(PolicyUpdate update) =>
            update with { Destination = new PolicyDestinationUpdate(useDefaultChat: true) };
    }
}
