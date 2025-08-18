using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AssetPacksManager
{
    public enum AssetPackType
    {
        Local,
        PDX,
    }

    public class AssetPack
    {
        public AssetPack(string modDir, string name, string assetPath)
        {
            Path = modDir;
            Name = name;
            AssetPath = assetPath;
            var metadataPath = System.IO.Path.Combine(modDir, ".metadata", "metadata.json");
            if (File.Exists(metadataPath))
            {
                try
                {
                    Mod.Logger.Info("Loading metadata from " + metadataPath);
                    var content = File.ReadAllText(metadataPath);
                    Metadata = JsonConvert.DeserializeObject<ModMetadata>(content);
                }
                catch
                {
                    // Ignore errors in loading metadata
                }
            }
        }
        
        // ID fallback for mods that do not have metadata (local mods)
        private int _id = 0;

        public int ID
        {
            get
            {
                if (Metadata != null && Metadata.Id > 0)
                {
                    return Metadata.Id;
                }
                return _id;
            }
            set { _id = value; }
        }
        
        public string Author
        {
            get
            {
                if (Metadata != null && !string.IsNullOrEmpty(Metadata.Author))
                {
                    return Metadata.Author;
                }
                return "Unknown";
            }
        }

        // Name fallback read from file name if metadata is not available
        private string _name;
        public string Name
        {
            get
            {
                if (Metadata != null && !string.IsNullOrEmpty(Metadata.DisplayName))
                {
                    return Metadata.DisplayName;
                }

                return _name;
            } set { _name = value; } 
            
        }
        public string? Path;
        public ModMetadata? Metadata;
        public string AssetPath;
        public PackageStability Stability = PackageStability.Unknown;
        public AssetPackType Type = AssetPackType.PDX;
        public List<FileInfo> AssetFiles = new();
        public List<string> MissingCids = new();

        public int AddAssetFiles(List<FileInfo> assetFiles)
        {
            AssetFiles.AddRange(assetFiles);
            return assetFiles.Count;
        }
    }
}