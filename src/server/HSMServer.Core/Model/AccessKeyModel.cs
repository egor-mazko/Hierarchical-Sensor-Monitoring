﻿using HSMCommon.Constants;
using HSMDatabase.AccessManager.DatabaseEntities;
using HSMServer.Core.Cache.UpdateEntitites;
using System;

namespace HSMServer.Core.Model
{
    [Flags]
    public enum KeyPermissions : long
    {
        CanSendSensorData = 1,
        CanAddNodes = 2,
        CanAddSensors = 4,
        CanReadSensorData = 8,
    }

    public enum KeyState : byte
    {
        Active = 0,
        Expired = 1,
        Blocked = 7
    }


    public class AccessKeyModel
    {
        private static readonly KeyPermissions _fullPermissions = (KeyPermissions)(1 << Enum.GetValues<KeyPermissions>().Length) - 1;

        internal static InvalidAccessKey InvalidKey { get; } = new();


        public Guid Id { get; }

        public string AuthorId { get; }

        public string ProductId { get; }

        public DateTime CreationTime { get; }

        public DateTime ExpirationTime { get; init; }

        public KeyState State { get; private set; }

        public KeyPermissions Permissions { get; private set; }

        public string DisplayName { get; private set; }


        public bool HasExpired => DateTime.UtcNow >= ExpirationTime && State < KeyState.Expired;


        public AccessKeyModel(AccessKeyEntity entity)
        {
            Id = Guid.Parse(entity.Id);
            AuthorId = entity.AuthorId;
            ProductId = entity.ProductId;
            State = (KeyState)entity.State;
            Permissions = (KeyPermissions)entity.Permissions;
            DisplayName = entity.DisplayName;
            CreationTime = new DateTime(entity.CreationTime);
            ExpirationTime = new DateTime(entity.ExpirationTime);
        }

        public AccessKeyModel(string authorId, string productId) : this()
        {
            AuthorId = authorId;
            ProductId = productId;
        }

        protected AccessKeyModel()
        {
            Id = Guid.NewGuid();
            CreationTime = DateTime.UtcNow;
        }

        private AccessKeyModel(ProductModel product) : this()
        {
            AuthorId = product.AuthorId;
            ProductId = product.Id;
            State = KeyState.Active;
            Permissions = _fullPermissions;
            DisplayName = CommonConstants.DefaultAccessKey;
            ExpirationTime = DateTime.MaxValue;
        }


        public AccessKeyModel Update(AccessKeyUpdate model)
        {
            if (model.DisplayName != null)
                DisplayName = model.DisplayName;

            if (model.Permissions.HasValue)
                Permissions = model.Permissions.Value;

            if (model.State.HasValue)
                State = model.State.Value;

            return this;
        }

        internal AccessKeyEntity ToAccessKeyEntity() =>
            new()
            {
                Id = Id.ToString(),
                AuthorId = AuthorId,
                ProductId = ProductId,
                State = (byte)State,
                Permissions = (long)Permissions,
                DisplayName = DisplayName,
                CreationTime = CreationTime.Ticks,
                ExpirationTime = ExpirationTime.Ticks
            };

        internal static AccessKeyModel BuildDefault(ProductModel product) => new AccessKeyModel(product);

        internal bool IsHasPermissions(KeyPermissions expectedPermissions, out string message)
        {
            var common = expectedPermissions & Permissions;

            if (common == expectedPermissions)
            {
                message = string.Empty;
                return true;
            }
            else
            {
                message = $"AccessKey doesn't have {expectedPermissions & ~common}.";
                return false;
            }
        }

        internal bool IsExpired(out string message)
        {
            message = string.Empty;

            if (ExpirationTime < DateTime.UtcNow)
            {
                message = "AccessKey expired.";
                State = KeyState.Expired;
                return true;
            }

            return false;
        }

        internal virtual bool IsValid(KeyPermissions permissions, out string message) =>
            !IsExpired(out message) && IsHasPermissions(permissions, out message);
    }

    public class InvalidAccessKey : AccessKeyModel
    {
        internal override bool IsValid(KeyPermissions permissions, out string message)
        {
            message = "Key is invalid.";
            return false;
        }
    }
}
