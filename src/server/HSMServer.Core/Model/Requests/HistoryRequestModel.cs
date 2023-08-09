﻿using System;

namespace HSMServer.Core.Model.Requests
{
    public class HistoryRequestModel : BaseRequestModel
    {
        public DateTime From { get; set; }

        public DateTime? To { get; set; }

        public int? Count { get; set; }

        public bool IncludeTTL { get; private set; } = true;

        public HistoryRequestModel(string key, string path) : base(key, path) { }

        public HistoryRequestModel AddTTlFlag(bool flag)
        {
            IncludeTTL = flag;

            return this;
        }
    }
}
