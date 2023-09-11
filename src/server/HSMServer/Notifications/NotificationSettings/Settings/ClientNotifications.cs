﻿using HSMDatabase.AccessManager.DatabaseEntities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;

namespace HSMServer.Notification.Settings
{
    public class ClientNotifications : NotificationSettings
    {
        public ConcurrentDictionary<ChatId, ConcurrentDictionary<Guid, DateTime>> PartiallyIgnored { get; } = new();

        public HashSet<Guid> EnabledSensors { get; } = new();


        public ClientNotifications() : base() { }

        internal ClientNotifications(NotificationSettingsEntity entity) : base(entity)
        {
            if (entity?.EnabledSensors is not null)
            {
                EnabledSensors.Clear();

                foreach (var sensorIdStr in entity.EnabledSensors)
                    if (Guid.TryParse(sensorIdStr, out var sensorId))
                        EnabledSensors.Add(sensorId);
            }

            if (entity?.PartiallyIgnored is not null)
            {
                PartiallyIgnored.Clear();

                foreach (var (chat, sensors) in entity.PartiallyIgnored)
                {
                    var ignoredSensors = new ConcurrentDictionary<Guid, DateTime>();
                    foreach (var (sensorIdStr, endIgnorePeriodTicks) in sensors)
                        if (Guid.TryParse(sensorIdStr, out var sensorId))
                            ignoredSensors.TryAdd(sensorId, new DateTime(endIgnorePeriodTicks));

                    PartiallyIgnored.TryAdd(new(chat), ignoredSensors);
                }
            }
        }


        public bool IsSensorIgnored(Guid sensorId, ChatId chatId = null)
        {
            return chatId is null
                ? PartiallyIgnored.Any(ch => ch.Value.ContainsKey(sensorId))
                : PartiallyIgnored.TryGetValue(chatId, out var ignoredSensors) && ignoredSensors.ContainsKey(sensorId);
        }

        public bool IsSensorEnabled(Guid sensorId) => EnabledSensors.Contains(sensorId);

        public bool RemoveSensor(Guid sensorId)
        {
            bool isSensorRemoved = EnabledSensors.Remove(sensorId);

            foreach (var (_, ignoredSensors) in PartiallyIgnored)
                isSensorRemoved |= ignoredSensors.TryRemove(sensorId, out _);

            return isSensorRemoved;
        }


        public void Ignore(Guid sensorId, DateTime endOfIgnorePeriod)
        {
            if (IsSensorEnabled(sensorId))
            {
                foreach (var (_, ignoredSensors) in PartiallyIgnored)
                    ignoredSensors.TryAdd(sensorId, endOfIgnorePeriod);

                if (PartiallyIgnored.Values.All(s => s.ContainsKey(sensorId)))
                    EnabledSensors.Remove(sensorId);
            }
        }

        public void RemoveIgnore(Guid sensorId, ChatId chatId = null)
        {
            if (IsSensorIgnored(sensorId, chatId))
            {
                if (chatId is null)
                    foreach (var (_, ignoredSensors) in PartiallyIgnored)
                        ignoredSensors.TryRemove(sensorId, out _);
                else if (PartiallyIgnored.TryGetValue(chatId, out var ignoredSensors))
                    ignoredSensors.TryRemove(sensorId, out _);

                EnabledSensors.Add(sensorId);
            }
        }

        public new NotificationSettingsEntity ToEntity() => new()
        {
            TelegramSettings = Telegram.ToEntity(),
            EnabledSensors = EnabledSensors.Select(s => s.ToString()).ToList(),
            PartiallyIgnored = PartiallyIgnored.ToDictionary(s => s.Key.Identifier ?? 0L, s => s.Value.ToDictionary(i => i.Key.ToString(), i => i.Value.Ticks)),
        };
    }
}
