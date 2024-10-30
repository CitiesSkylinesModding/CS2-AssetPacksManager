using System.Collections.Generic;
using System.IO;

namespace AssetPacksManager;

public enum AssetPackType
{
    Local,
    PDX,
}

public class AssetPack
{
    public int ID = 0;
    public string Name;
    public string Path;
    public string AssetPath;
    public PackageStability Stability = PackageStability.Unknown;
    public AssetPackType Type = AssetPackType.PDX;
    public List<FileInfo> AssetFiles = new();
    public List<string> MissingCids = new();

    public void AddAssetFiles(List<FileInfo> assetFiles)
    {
        AssetFiles.AddRange(assetFiles);
    }
}