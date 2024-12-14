using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace AssetPacksManager;

public static class TelemetryTransmitter
{
    private const string Endpoint = "http://server.webgadgets.de";
    private const int Port = 5001;
    private static bool _submitted;

    public static async Task SubmitAsync(int loaded, int autoLoaded, int notLoaded, bool adaptiveLoadingEnabled)
    {
        if (Setting.Instance.DisableTelemetry)
        {
            KLogger.Instance.Info("Telemetry disabled");
            return;
        }
        if (_submitted)
        {
            KLogger.Instance.Info("Telemetry already submitted");
            return;
        }
        string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        _submitted = true;
        string uri = $"{Endpoint}:{Port}/submit?" +
                     $"loaded={loaded}&" +
                     $"autoLoaded={autoLoaded}&" +
                     $"notLoaded={notLoaded}&" +
                     $"adaptiveEnabled={adaptiveLoadingEnabled}&" +
                     $"version={version}";
        try
        {
            var request = WebRequest.Create(uri);
            request.Timeout = 3000;
            request.Method = "GET";
            await request.GetResponseAsync();
            KLogger.Instance.Info($"{uri} -> Telemetry submit OK");
        }
        catch (Exception e)
        {
            KLogger.Instance.Error($"{uri} -> {e.Message}");
        }
    }
}