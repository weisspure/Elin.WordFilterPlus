using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;

namespace WordFilterPlus;

public static class ModInfo
{
    public const string Guid    = "dk.elinplugins.wordfilterplus";
    public const string Name    = "WordFilterPlus";
    public const string Version = "1.0.0";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
internal class Plugin : BaseUnityPlugin
{
    internal static Plugin? Instance;

    private void Awake()
    {
        Instance = this;
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), ModInfo.Guid);
        LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded.");
    }

    internal static void LogDebug(object msg, [CallerMemberName] string caller = "") =>
        Instance?.Logger.LogDebug($"[{caller}] {msg}");

    internal static void LogInfo(object msg) =>
        Instance?.Logger.LogInfo(msg);

    internal static void LogError(object msg) =>
        Instance?.Logger.LogError(msg);
}
