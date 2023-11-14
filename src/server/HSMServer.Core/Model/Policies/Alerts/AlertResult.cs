﻿using HSMCommon.Extensions;
using HSMServer.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace HSMServer.Core.Model.Policies
{
    public sealed record AlertResult
    {
        public AlertDestination Destination { get; }

        public Guid PolicyId { get; }


        public long? ConfirmationPeriod { get; }

        public string Template { get; }

        public string Icon { get; }


        public bool IsStatusIsChangeResult { get; }


        public AlertState LastState { get; private set; }

        public string LastComment { get; private set; }

        public int Count { get; private set; }


        public (string, int) Key => (Icon, Count);


        internal AlertResult(Policy policy)
        {
            Destination = new(policy.Destination.AllChats, new HashSet<Guid>(policy.Destination.Chats.Keys));

            ConfirmationPeriod = policy.ConfirmationPeriod;
            Template = policy.Template;
            PolicyId = policy.Id;
            Icon = policy.Icon;

            IsStatusIsChangeResult = policy.Conditions.IsStatusChangeResult();

            AddPolicyResult(policy);
        }


        public bool TryAddResult(AlertResult result)
        {
            if (PolicyId != result.PolicyId)
                return false;

            if (!TryCustomUpdateApply(result.LastState))
            {
                Count += result.Count;
                LastComment = result.LastComment;
                LastState = result.LastState;
            }

            return true;
        }

        internal void AddPolicyResult(Policy policy)
        {
            if (!TryCustomUpdateApply(policy.State))
            {
                Count++;
                LastComment = policy.Comment;
                LastState = policy.State;
            }
        }

        private bool TryCustomUpdateApply(AlertState newState)
        {
            if (IsStatusIsChangeResult && LastState is not null)
            {
                LastState = newState with
                {
                    PrevStatus = $"{LastState.PrevStatus}->{newState.PrevStatus}",
                    PrevComment = $"{LastState.PrevComment}->{newState.PrevComment}",
                    PrevValue = $"{LastState.PrevValue}->{newState.PrevValue.ToReadableView()}"
                };

                LastComment = LastState.BuildComment();

                return true;
            }

            return false;
        }


        public string BuildFullComment(string comment, int extraCnt = 0)
        {
            var sb = new StringBuilder(1 << 5);
            var totalCnt = Count + extraCnt;

            sb.Append($"{Icon} {comment}");

            if (totalCnt > 1)
                sb.Append($" ({totalCnt} times)");

            return sb.ToString().Trim();
        }

        public override string ToString() => BuildFullComment(LastComment);
    }


    public sealed record AlertDestination(bool AllChats, HashSet<Guid> Chats);
}