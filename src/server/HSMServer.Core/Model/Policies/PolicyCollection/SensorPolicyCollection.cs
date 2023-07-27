﻿using HSMDatabase.AccessManager.DatabaseEntities;
using HSMServer.Core.Cache;
using HSMServer.Core.Cache.UpdateEntities;
using HSMServer.Core.Journal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HSMServer.Core.Model.Policies
{
    public abstract class SensorPolicyCollection : PolicyCollectionBase, IChangesEntity
    {
        internal protected SensorResult SensorResult { get; protected set; } = SensorResult.Ok;

        internal protected PolicyResult PolicyResult { get; protected set; } = PolicyResult.Ok;


        internal Action<ActionType, Policy> Uploaded;

        public event Action<JournalRecordModel> ChangesHandler;


        internal abstract void Update(List<PolicyUpdate> updates, string initiator);

        internal abstract void Attach(BaseSensorModel sensor);

        [Obsolete("remove after policy migration")]
        internal abstract void AddStatus();


        internal void Reset()
        {
            SensorResult = SensorResult.Ok;
            PolicyResult = PolicyResult.Ok;
        }

        protected void CallJournal(JournalRecordModel record) => ChangesHandler?.Invoke(record);
    }


    public abstract class SensorPolicyCollection<T> : SensorPolicyCollection where T : BaseValue
    {
        private protected BaseSensorModel _sensor;
        private CorrectTypePolicy<T> _typePolicy;


        protected abstract bool CalculateStorageResult(T value, bool updateSensor);


        internal override void Attach(BaseSensorModel sensor)
        {
            _typePolicy = new CorrectTypePolicy<T>(sensor);
            _sensor = sensor;

            base.BuildDefault(sensor);
        }

        internal override void BuildDefault(BaseNodeModel node, PolicyEntity entity = null)
        {
            base.BuildDefault(node, entity);
            _typePolicy.RebuildState();
        }


        internal bool TryValidate(BaseValue value, out T valueT, bool updateSensor = true)
        {
            SensorResult = SensorResult.Ok;

            valueT = value as T;

            if (!CorrectTypePolicy<T>.Validate(valueT))
            {
                SensorResult = _typePolicy.SensorResult;
                PolicyResult = _typePolicy.PolicyResult;

                return false;
            }

            return CalculateStorageResult(valueT, updateSensor);
        }

        internal bool SensorTimeout(DateTime? time)
        {
            if (TimeToLive is null)
                return false;

            var timeout = TimeToLive.HasTimeout(time);

            PolicyResult = timeout ? TimeToLive.PolicyResult : PolicyResult.Ok;

            SensorExpired?.Invoke(_sensor, timeout);

            return timeout;
        }
    }


    public sealed class SensorPolicyCollection<ValueType, PolicyType> : SensorPolicyCollection<ValueType>
        where ValueType : BaseValue
        where PolicyType : Policy<ValueType>, new()
    {
        private readonly ConcurrentDictionary<Guid, PolicyType> _storage = new();


        internal override IEnumerable<Guid> Ids => _storage.Keys;

        internal IEnumerable<Policy<ValueType>> Policies => _sensor.UseParentPolicies ? _sensor.Parent.GetPolicies<PolicyType>(_sensor.Type) : _storage.Values;


        protected override bool CalculateStorageResult(ValueType value, bool updateStatus = true)
        {
            PolicyResult = new(_sensor.Id);

            foreach (var policy in Policies ?? Enumerable.Empty<PolicyType>())
                if (!policy.Validate(value))
                {
                    PolicyResult.AddAlert(policy);

                    if (updateStatus)
                        SensorResult += policy.SensorResult;
                }

            return true;
        }


        internal override void AddPolicy<T>(T policy)
        {
            if (policy is PolicyType typedPolicy)
                _storage.TryAdd(policy.Id, typedPolicy);
        }

        internal override void Update(List<PolicyUpdate> updatesList, string initiator)
        {
            var updates = updatesList.Where(u => u.Id != Guid.Empty).ToDictionary(u => u.Id);

            foreach (var (id, policy) in _storage)
            {
                if (updates.TryGetValue(id, out var update))
                {
                    var oldPolicy = policy.ToString();

                    policy.Update(update);

                    CallJournal(oldPolicy, policy.ToString(), initiator);

                    Uploaded?.Invoke(ActionType.Update, policy);
                }
                else if (_storage.TryRemove(id, out var oldPolicy))
                {
                    if (_sensor.LastValue is ValueType lastValue && lastValue is not null)
                        CalculateStorageResult(lastValue);

                    Uploaded?.Invoke(ActionType.Delete, oldPolicy);
                }
            }

            foreach (var update in updatesList)
                if (update.Id == Guid.Empty)
                {
                    var policy = new PolicyType();

                    policy.Update(update, _sensor);

                    AddPolicy(policy);
                    CallJournal(string.Empty, policy.ToString(), initiator);

                    Uploaded?.Invoke(ActionType.Add, policy);
                }
        }

        public override IEnumerator<Policy> GetEnumerator() => _storage.Values.GetEnumerator();

        internal override void ApplyPolicies(List<string> policyIds, Dictionary<string, PolicyEntity> allPolicies)
        {
            foreach (var id in policyIds ?? Enumerable.Empty<string>())
                if (allPolicies.TryGetValue(id, out var entity))
                {
                    var policy = new PolicyType();

                    policy.Apply(entity, _sensor);

                    _storage.TryAdd(policy.Id, policy);
                }
        }

        internal override void AddStatus()
        {
            var policy = new PolicyType();

            var statusUpdate = new PolicyUpdate(
                Guid.NewGuid(),
                new()
                {
                    new PolicyConditionUpdate(
                        PolicyOperation.IsChanged,
                        PolicyProperty.Status,
                        new TargetValue(TargetType.LastValue, _sensor.Id.ToString())),
                },
                null,
                SensorStatus.Ok,
                $"$status [$product]$path = $comment",
                null);

            policy.Update(statusUpdate, _sensor);

            AddPolicy(policy);
            Uploaded?.Invoke(ActionType.Add, policy);
        }

        private void CallJournal(string oldValue, string newValue, string initiator) => 
            CallJournal(new JournalRecordModel(_sensor.Id, initiator)
            {
                Enviroment = "Alerts update",
                OldValue = oldValue,
                NewValue = newValue,
                Path = _sensor.FullPath,
            });
    }
}