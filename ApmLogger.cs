using System;
using System.Diagnostics;
using Colossal.Logging;
using Colossal.PSI.Environment;

namespace AssetPacksManager;

public class ApmLogger
{
    private static readonly string logFileName = $"{nameof(AssetPacksManager)}.{nameof(Mod)}";

    public static readonly ILog Logger = LogManager.GetLogger(logFileName)
        .SetShowsErrorsInUI(false);

    public static ApmLogger Instance;
    private static readonly bool DisableLogging = false;

    public static void Init()
    {
        Instance = new ApmLogger();
    }

    public void OpenLogFile()
    {
        Process.Start($"{EnvPath.kUserDataPath}/Logs/{logFileName}.log");
    }

    public void Debug(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Debug)
            return;
        Logger.Debug(message);
    }

    public void Info(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Info)
            return;
        Logger.Info(message);
    }

    public void Warn(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Warning)
            return;
        Logger.Warn(message);
    }

    public void Error(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Error)
            return;
        Logger.Error(message);
    }

    public void Critical(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Critical)
            return;
        Logger.Critical(message);
    }
}