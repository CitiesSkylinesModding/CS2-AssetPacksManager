using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;

namespace AssetPacksManager;

public enum PackageStability
{
    Unspecified = -1,
    Stable = 1,
    NotEnoughInformation = 2,
    HasIssues = 3,
    Broken = 4,
    BrokenFromPatch = 5,
    BrokenFromNewVersion = 10,
    StableNoNewFeatures = 6,
    StableNoFutureUpdates = 7,
    HasIssuesNoFutureUpdates = 8,
    BreaksOnPatch = 9,

    NotReviewed = 0,
    Incompatible = 99,
    AssetNotReviewed = 98,
    Local = 97,
    AuthorRetired = 96,
    BrokenFromPatchSafe = 95,
    AssetIncompatible = 94,
    BrokenFromPatchUpdated = 93,
    BrokenFromNewVersionSafe = 92,
}

public static class SkyveInterface
{
    private static KLogger Logger;
    private const bool Initialized = false;
    private static HttpClient _client;
    private static string url = $"https://skyve-mod.com/v2/api/CompatibilityData/";

    public static void Init()
    {
        Logger = Mod.Logger;
        _client = new HttpClient();
        _client.Timeout = new System.TimeSpan(0, 0, 2);
    }

    public static PackageStability CheckModStatus(int id)
    {
        if (!Initialized)
            Init();

        var request = new HttpRequestMessage(HttpMethod.Get, url + id);

        request.Headers.Add("API_KEY", "MPHIoIlbsrmDCYWFLDZaUaMGD0p1l282ARrXhHt4");
        request.Headers.Add("USER_ID", "");

        try
        {
            HttpResponseMessage response = _client.SendAsync(request).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            Logger.Info(responseBody);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

            if (data.ContainsKey("stability"))
            {
                int stability = int.Parse(data.TryGetValue("stability", out var value) ? value.ToString() : "-1");
                return (PackageStability) stability;
            }
        }
        catch (HttpRequestException e)
        {

        }

        return PackageStability.Unspecified;
    }
}