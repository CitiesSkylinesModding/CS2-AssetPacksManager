using System;
using System.Net;

namespace AssetPacksManager;

public class TelemetryTransmitter
{
    private const string Endpoint = "http://85.214.141.43";
    private const int Port = 5001;
    private static bool submitted = false;

    public static string Submit(int assetPackCount, bool adaptiveLoadingEnabled)
    {
        if (Setting.Instance.DisableTelemetry)
            return "Telemetry disabled";
        if (submitted)
            return "Already submitted";
        submitted = true;
        string uri = Endpoint + ":" + Port + "/submit?assetCount=" + assetPackCount + "&adaptiveEnabled=" +
                     adaptiveLoadingEnabled;
        try
        {
            var request = WebRequest.Create(uri);
            request.Method = "GET";
            request.GetResponse();
            return "OK";
        }
        catch (Exception e)
        {
            return uri + " -> " + e.Message;
        }
    }
}