﻿using HSMDatabase.AccessManager.DatabaseEntities;
using HSMServer.Core.Cache.UpdateEntities;
using HSMServer.Core.Model.NodeSettings;
using HSMServer.Core.Model.Policies;
using System;
using System.Runtime.CompilerServices;
using HSMServer.Core.Journal;

namespace HSMServer.Core.Model
{
    public abstract class BaseNodeModel : IChangesEntity
    {
        public event Action<JournalRecordModel> ChangesHandler;


        public abstract PolicyCollectionBase Policies { get; }

        public SettingsCollection Settings { get; } = new();


        public Guid Id { get; }

        public Guid? AuthorId { get; }

        public DateTime CreationDate { get; }


        public ProductModel Parent { get; private set; }


        public string DisplayName { get; private set; }

        public string Description { get; private set; }

        public bool UseParentPolicies { get; private set; }


        public string RootProductName => Parent?.RootProductName ?? DisplayName;

        public string Path => Parent is null ? string.Empty : $"{Parent.Path}/{DisplayName}";


        protected BaseNodeModel()
        {
            Id = Guid.NewGuid();
            CreationDate = DateTime.UtcNow;
        }

        protected BaseNodeModel(string name, Guid? authorId) : this()
        {
            DisplayName = name;
            AuthorId = authorId ?? Guid.Empty;
        }

        protected BaseNodeModel(BaseNodeEntity entity) : this()
        {
            Id = Guid.Parse(entity.Id);
            AuthorId = Guid.TryParse(entity.AuthorId, out var authorId) ? authorId : Guid.Empty;
            CreationDate = new DateTime(entity.CreationDate);

            DisplayName = entity.DisplayName;
            Description = entity.Description;

            if (entity.Settings != null)
                Settings.SetSettings(entity.Settings);
        }


        internal abstract bool CheckTimeout();


        protected internal BaseNodeModel AddParent(ProductModel parent)
        {
            Parent = parent;

            Settings.SetParentSettings(parent.Settings);

            return this;
        }

        protected internal void Update(BaseNodeUpdate update, string initiator = null)
        {
            Description = UpdateProperty(update.Description, Description,initiator);

            Settings.KeepHistory.TrySetValue(update.KeepHistory, Id);
            Settings.SelfDestroy.TrySetValue(update.SelfDestroy, Id);

            if (Settings.TTL.TrySetValue(update.TTL, Id))
                CheckTimeout();
        }

        protected T UpdateProperty<T>(T newValue, T oldValue, string initiator, [CallerArgumentExpression("oldValue")] string propName = "")
        {
            if (newValue is not null && !newValue.Equals(oldValue))
                ChangesHandler?.Invoke(new JournalRecordModel(Id, DateTime.UtcNow, $"{propName} -> {newValue}", Path, RecordType.Changes, initiator));
            
            return newValue ?? oldValue;
        }
    }
}