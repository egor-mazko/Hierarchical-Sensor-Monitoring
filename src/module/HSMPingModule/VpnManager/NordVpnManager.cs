﻿using HSMPingModule.Common;
using HSMPingModule.Console;

namespace HSMPingModule.VpnManager
{
    internal sealed class NordVpnManager : BaseVpnManager
    {
        private const string ServiceName = "nordvpn";
        private const string ErrorAnswer = "Whoops";

        private const string CountriesListCommand = "countries";
        private const string SwitchCountryCommand = "connect";
        private const string DisconnectCommand = "disconnect";

        private string _description = $"[**Nord VPN**](https://nordvpn.com/) is used to check configured resources.";


        internal override string VpnDescription => _description;


        internal override Task<TaskResult> Connect() => TaskResult.OkTask;

        internal override async Task<TaskResult> Disconnect()
        {
            var result = await RunCommand(DisconnectCommand);

            return result.IsOk ? TaskResult.Ok : new TaskResult(result.Error);
        }


        internal override async Task<TaskResult> SwitchCountry(string country)
        {
            var check = await base.SwitchCountry(country);

            if (!check.IsOk)
                return check;

            var result = await RunCommand($"{SwitchCountryCommand} {country}");

            return result.IsOk ? TaskResult.Ok : new TaskResult($"Cannot connect to country {country}. {result.Error}");
        }


        protected override async Task<TaskResult<List<string>>> LoadAvailableCountries()
        {
            var result = await RunCommand(CountriesListCommand);

            if (result.IsOk)
            {
                _description = $"{_description}  \nAvailable countires:  \n{result.Result}";

                var countries = result.Result.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                return TaskResult<List<string>>.GetOk(countries);
            }

            return new TaskResult<List<string>>(result.Error);
        }


        private static async Task<TaskResult<string>> RunCommand(string command)
        {
            var result = await ConsoleExecutor.Run($"{ServiceName} {command}");

            return result.StartsWith(ErrorAnswer) ? new TaskResult<string>(result) : TaskResult<string>.GetOk(result);
        }
    }
}