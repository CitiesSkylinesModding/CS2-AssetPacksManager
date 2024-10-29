using System;
using System.Net;
using System.Reflection;

namespace AssetPacksManager;

public static class TelemetryTransmitter
{
    private const string Endpoint = "http://server.webgadgets.de";
    private const int Port = 5001;
    private static bool _submitted;

    public static string Submit(int assetCount, bool adaptiveLoadingEnabled)
    {
        if (Setting.Instance.DisableTelemetry)
            return "Telemetry disabled";
        if (_submitted)
            return "Already submitted";
        string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        _submitted = true;
        string uri = $"{Endpoint}:{Port}/submit?" +
                     $"assetCount={assetCount}&" +
                     $"adaptiveEnabled={adaptiveLoadingEnabled}&" +
                     $"version={version}";
        try
        {
            var request = WebRequest.Create(uri);
            request.Method = "GET";
            request.GetResponse();
            return $"{uri} -> Telemetry submit OK";
        }
        catch (Exception e)
        {
            return $"{uri} -> {e.Message}";
        }
    }
}