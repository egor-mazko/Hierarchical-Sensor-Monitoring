﻿using HSMServer.ConcurrentStorage;
using System;

namespace HSMServer.Model.Authentication
{
    public record UserUpdate : IUpdateModel
    {
        public required Guid Id { get; init; }

        public string Password { get; set; }

        public bool? IsAdmin { get; set; }

        public string Name { get; set; }
    }
}
