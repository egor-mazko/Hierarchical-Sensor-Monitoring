﻿using System;
using System.Collections.Concurrent;

namespace HSMCommon.Collections.Reactive
{
    public sealed class RDict<T>(Action reaction) : RDictBase<Guid, T>(reaction) { }


    public readonly struct RDictResult<T>
    {
        private readonly Action _reaction;


        public static RDictResult<T> ErrorResult { get; } = new(false, default);

        public bool IsOk { get; }

        public T Value { get; }


        private RDictResult(bool ok, T value)
        {
            IsOk = ok;
            Value = value;
        }

        public RDictResult(bool ok, T value, Action reaction) : this(ok, value)
        {
            _reaction = reaction;
        }


        public readonly RDictResult<T> ThenCallForSuccess(Action<T> customReaction)
        {
            if (IsOk)
                customReaction?.Invoke(Value);

            return this;
        }

        public readonly RDictResult<T> ThenCall()
        {
            if (IsOk)
                _reaction?.Invoke();

            return this;
        }
    }


    public abstract class RDictBase<TKey, TValue> : ConcurrentDictionary<TKey, TValue>
    {
        private readonly Action _reaction;


        protected RDictBase(Action reaction) : base()
        {
            _reaction = reaction;
        }


        public RDictResult<TValue> IfTryAdd(TKey key, TValue value) => ToReaction(TryAdd(key, value), value);

        public bool TryCallAdd(TKey key, TValue value) => IfTryAdd(key, value).ThenCall().IsOk;


        public RDictResult<TValue> IfTryRemove(TKey key)
        {
            var result = TryRemove(key, out var value);

            return ToReaction(result, value);
        }

        public bool TryCallRemoveCall(TKey key) => IfTryRemove(key).ThenCall().IsOk;


        private RDictResult<TValue> ToReaction(bool result, TValue value) => new(result, value, _reaction);
    }
}