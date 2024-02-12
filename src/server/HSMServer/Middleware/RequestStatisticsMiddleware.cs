﻿using HSMServer.BackgroundServices;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HSMServer.Middleware
{
    internal sealed class RequestStatisticsMiddleware
    {
        private const double KbDivisor = 1 << 10;

        private readonly DataCollectorWrapper _collector;
        private readonly RequestDelegate _next;

        public RequestStatisticsMiddleware(RequestDelegate next, DataCollectorWrapper collector)
        {
            _collector = collector;
            _next = next;
        }

        public Task InvokeAsync(HttpContext context)
        {
            // _collector.Statistics.RequestsPerSecondSensor.AddValue(1);
            //
            // var request = context.Request;
            // _collector.Statistics.RequestSizePerSecondSensor.AddValue((request.ContentLength ?? 0) / KbDivisor);
            //
            // context.Response.OnCompleted(() =>
            // {
            //     _collector.Statistics.ResponseSizeSensor.AddValue((context.Response.ContentLength ?? 0) / KbDivisor);
            //     return Task.CompletedTask;
            // });

            return _next(context);
        }
    }
}
