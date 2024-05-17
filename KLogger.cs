using System;
using System.Threading;
using Colossal.Logging;
using Colossal.PSI.Environment;

namespace AssetPacksManager;

public class KLogger
{
    private static readonly string logFileName = $"{nameof(AssetPacksManager)}.{nameof(Mod)}";
    public static readonly ILog Logger = LogManager.GetLogger(logFileName)
        .SetShowsErrorsInUI(false);

    public static KLogger Instance;
    private static bool DisableLogging = false;

    public static void Init()
    {
        Instance = new KLogger();
    }

    public void OpenLogFile()
    {
        System.Diagnostics.Process.Start($"{EnvPath.kUserDataPath}/Logs/{logFileName}.log");
    }

    public static DateTime nextLogTime = DateTime.MinValue;

    public void Debug(string message)
    {
        if(DisableLogging)
            return;
        int cooldown = Setting.Instance.LogCooldownTicks;
        while(cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }
        Logger.Debug(message);
        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Info(string message)
    {
        if(DisableLogging)
            return;
        int cooldown = Setting.Instance.LogCooldownTicks;
        while(cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }
        Logger.Info(message);
        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Warn(string message)
    {
        if(DisableLogging)
            return;
        int cooldown = Setting.Instance.LogCooldownTicks;
        while(cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }
        Logger.Warn(message);
        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Error(string message)
    {
        if(DisableLogging)
            return;
        int cooldown = Setting.Instance.LogCooldownTicks;
        while(cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }
        Logger.Error(message);
        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }

    public void Critical(string message)
    {
        if(DisableLogging)
            return;
        int cooldown = Setting.Instance.LogCooldownTicks;
        while(cooldown > 0 && DateTime.Now < nextLogTime)
        {
        }
        Logger.Critical(message);
        nextLogTime = DateTime.Now.AddTicks(cooldown);
    }
}