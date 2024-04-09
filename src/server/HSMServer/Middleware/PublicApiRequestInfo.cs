using HSMServer.Core.Model;

namespace HSMServer.Middleware
{
    public sealed class PublicApiRequestInfo
    {
        public ProductModel Product { get; init; }

        public AccessKeyModel Key { get; init; }

        public string CollectorName { get; init; }

        public string RemoteIP { get; init; }

        public string TelemetryPath { get; private set; }


        public void BuildTelemetryPath() => TelemetryPath = $"{Product.DisplayName}/{Key.DisplayName}/{CollectorName}";
    }
}
