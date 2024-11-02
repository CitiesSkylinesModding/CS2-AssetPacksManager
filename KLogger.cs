using System;
using System.Diagnostics;
using Colossal.Logging;
using Colossal.PSI.Environment;

namespace AssetPacksManager;

public class KLogger
{
    private static readonly string logFileName = $"{nameof(AssetPacksManager)}.{nameof(Mod)}";

    public static readonly ILog Logger = LogManager.GetLogger(logFileName)
        .SetShowsErrorsInUI(false);

    public static KLogger Instance;
    private static readonly bool DisableLogging = false;

    public static DateTime nextLogTime = DateTime.MinValue;

    public static void Init()
    {
        Instance = new KLogger();
    }

    public void OpenLogFile()
    {
        Process.Start($"{EnvPath.kUserDataPath}/Logs/{logFileName}.log");
    }

    public void Debug(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Debug)
            return;
        var cooldown = Setting.Instance.LogCooldownTicks;
        while (cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }

        try
        {
            Logger.Debug(message);
        }
        catch (Exception)
        {
        }

        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Info(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Info)
            return;
        var cooldown = Setting.Instance.LogCooldownTicks;
        while (cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }

        try
        {
            Logger.Info(message);
        }
        catch (Exception)
        {
        }

        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Warn(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Warning)
            return;
        var cooldown = Setting.Instance.LogCooldownTicks;
        while (cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }

        try
        {
            Logger.Warn(message);
        }
        catch (Exception)
        {
        }

        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Error(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Error)
            return;
        var cooldown = Setting.Instance.LogCooldownTicks;
        while (cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }

        try
        {
            Logger.Error(message);
        }
        catch (Exception)
        {
        }

        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Critical(string message)
    {
        if (DisableLogging || Setting.Instance.LoggingLevel > Setting.LogLevel.Critical)
            return;
        var cooldown = Setting.Instance.LogCooldownTicks;
        while (cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }

        try
        {
            Logger.Critical(message);
        }
        catch (Exception)
        {
        }

        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }
}