﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSMCommon.Constants;
using HSMSensorDataObjects;
using HSMSensorDataObjects.SensorValueRequests;
using HSMServer.BackgroundServices;
using HSMServer.Core.Model;
using HSMServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Header = HSMSensorDataObjects.Header;

namespace HSMServer.Middleware;

public abstract class PermissionFilter(IPermissionService service, DataCollectorWrapper collector) : IAsyncActionFilter, IAsyncResultFilter
{
    private const string ValuesArgument = "values";
    private const string CommandsArgument = "sensorCommands";


    protected abstract KeyPermissions Permissions { get; }


    public void CheckPermission(ActionExecutingContext context, RequestData requestData, out string message)
    {
        message = string.Empty;

        var values = context.ActionArguments.Values.FirstOrDefault();
        var collectorName = context.HttpContext.Request.Headers.TryGetValue(nameof(Header.ClientName), out var clientName) && !string.IsNullOrWhiteSpace(clientName) ? clientName.ToString() : "No name";
        requestData.TelemetryPath = $"{requestData.Product.DisplayName}/{requestData.Key.DisplayName}/{collectorName}";

        collector.Statistics[requestData.TelemetryPath]?.AddRequestData(context.HttpContext.Request);

        if (values is BaseRequest request)
        {
            if (!service.CheckPermission(requestData, new SensorData()
                {
                    Path = request.Path,
                    KeyId = request.Key
                }, Permissions, out message))
            {
                context.HttpContext.Response.StatusCode = 406;
                return;
            }
        }

        switch (values)
        {
            case List<SensorValueBase> requestValues:
                AddRequestData<SensorValueBase>(context, requestData, values: requestValues, argumentName: ValuesArgument);
                break;
            case List<CommandRequestBase> commands:
                AddRequestData<CommandRequestBase>(context, requestData, values: commands, argumentName: CommandsArgument);
                break;
            default:
                AddRequestData<BaseRequest>(context, requestData);
                break;
        }
    }


    private void AddRequestData<T>(ActionExecutingContext context, RequestData requestData, List<T> values = null, string argumentName = null) where T : BaseRequest
    {
        if (values is not null && !string.IsNullOrEmpty(argumentName))
        {
            context.ActionArguments.Remove(argumentName);
            values = values.Where(x => service.CheckPermission(requestData, new SensorData() {Path = x.Path, KeyId = x.Key}, Permissions, out _)).ToList();

            context.ActionArguments.Add(argumentName, values);

            requestData.Count = values.Count;
        }

        collector.Statistics[requestData.TelemetryPath]?.AddReceiveData(requestData.Count);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Items.TryGetValue(TelemetryMiddleware.RequestData, out var obj) ||
            obj is not RequestData requestData)
            return;

        GetKeyIdFromHeader(context.HttpContext.Request.Headers, out var keyId);
        service.TryGetKey(keyId, out var key, out var message);
        requestData.Key = key;

        service.TryGetProduct(key.ProductId, out var product, out message);
        requestData.Product = product;

        CheckPermission(context, requestData, out message);
        await next();
    }


    private static bool GetKeyIdFromHeader(IHeaderDictionary headers, out Guid guidKey)
    {
        headers.TryGetValue(nameof(Header.Key), out var key);

        return Guid.TryParse(key, out guidKey);
    }

    public static ReadOnlySpan<string> GetPathParts(string path)
    {
        path = path.FirstOrDefault() == CommonConstants.SensorPathSeparator ? path[1..] : path;

        return path.Split(CommonConstants.SensorPathSeparator, StringSplitOptions.TrimEntries).AsSpan();
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.HttpContext.Items.TryGetValue(TelemetryMiddleware.RequestData, out var value) && value is RequestData requestData)
            collector.Statistics[requestData.TelemetryPath]?.AddResponseResult(context.HttpContext.Response);

        await next();
    }
}