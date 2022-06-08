using BepInEx.IL2CPP;
using BepInEx.Logging;
using BepInEx;
using HarmonyLib;
using System.Reflection;

namespace PPRising
{
  [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
  [BepInDependency("xyz.molenzwiebel.wetstone")]
  [Wetstone.API.Reloadable]
  public class Plugin : BasePlugin
  {
    public static ManualLogSource Logger;
    private Harmony _harmony;
    private bool _unloaded = false;

    public override void Load()
    {
      Logger = Log;
      _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_GUID);

      Log.LogInfo(_harmony.GetPatchedMethods().Join(m => $" - {m.ReflectedType.Namespace}.{m.ReflectedType.Name}.{m.Name}", "\n"));

      QuickStack.Load(this);

      Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    public override bool Unload()
    {
      if (_unloaded) return false;
      _unloaded = true;

      Config.Clear();
      _harmony.UnpatchSelf();

      QuickStack.Unload(this);

      Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is unloaded!");

      return base.Unload();
    }
  }
}
