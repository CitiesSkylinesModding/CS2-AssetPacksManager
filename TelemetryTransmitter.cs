using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace AssetPacksManager;

public static class TelemetryTransmitter
{
    private const string Endpoint = "http://apm.mimonsi.de";
    private const int Port = 5001;
    private static bool _submitted;

    public static async Task SubmitAsync(int packs, int assets)
    {
        if (Setting.Instance.DisableTelemetry)
        {
            ApmLogger.Instance.Info("Telemetry disabled");
            return;
        }
        if (_submitted)
        {
            ApmLogger.Instance.Info("Telemetry already submitted");
            return;
        }
        string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        _submitted = true;
        string uri = $"{Endpoint}:{Port}/submit?" +
                     $"packs={packs}&" +
                     $"assets={assets}&" +
                     $"version={version}";
        try
        {
            var request = WebRequest.Create(uri);
            request.Timeout = 10000;
            request.Method = "GET";
            await request.GetResponseAsync();
            ApmLogger.Instance.Info($"{uri} -> Telemetry submit OK");
        }
        catch (WebException e) when (e.Status == WebExceptionStatus.Timeout)
        {
            ApmLogger.Instance.Info("Telemetry submit timeout; Server is probably unavailable at this time. This is not an issue.");
        }
        catch (Exception e)
        {
            ApmLogger.Instance.Info($"This is not an issue and can be ignored. Telemetry could not be sent: {uri} -> {e}");
        }
    }
}