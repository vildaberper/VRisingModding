using System;
using System.Text;
using HarmonyLib;
using BepInEx.Configuration;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wetstone.API;
using ProjectM;
using ProjectM.Scripting;
using Stunlock.Network;

namespace PPRising
{
  [HarmonyPatch]
  public static class QuickStack
  {
    public static ConfigEntry<bool> _enable;
    public static ConfigEntry<float> _distance;
    public static ConfigEntry<string> _nameIgnore;
    public static ConfigEntry<bool> _sortOnStack;

    public static Keybinding _keybinding;

    private static QuickStackSorter _sorter;

    private static System.Collections.Generic.HashSet<PrefabGUID> _withDebuff = null;

    public static void Load(Plugin plugin)
    {
      _enable = plugin.Config.Bind(new ConfigDefinition("PPRising", "QuickStackEnable"), true,
        new ConfigDescription("Enable quick stack")
      );
      _distance = plugin.Config.Bind(new ConfigDefinition("PPRising", "QuickStackDistance"), 20.0f,
        new ConfigDescription("Quick stack distance", new AcceptableValueRange<float>(5f, 100f))
      );
      _nameIgnore = plugin.Config.Bind(new ConfigDefinition("PPRising", "QuickStackNameIgnore"), "nostack",
        new ConfigDescription("Ignore quick stacking to containers with name containing")
      );
      _sortOnStack = plugin.Config.Bind(new ConfigDefinition("PPRising", "QuickStackSortOnStack"), true,
        new ConfigDescription("Sort container after quick stacking to it")
      );

      _keybinding = KeybindManager.Register(new()
      {
        Id = "vildaberper.PPRising.QuickStack",
        Category = "PPRising",
        Name = "Quick Stack",
        DefaultKeybinding = KeyCode.Insert
      });

      VNetworkRegistry.RegisterServerboundStruct<QuickStackRequestMessage>((fromCharacter, msg) =>
      {
        if (!VWorld.IsServer) return;
        if (!_enable.Value) return;

        Plugin.Logger.LogDebug($"QuickStackMessage recieved: {msg}");

        var character = fromCharacter.Character;
        var characterInventory = Util.GetInventoryEntity(character);
        if (!characterInventory.HasValue)
        {
          Plugin.Logger.LogError("Failed to get character inventory entity");
          return;
        }

        var gameManager = VWorld.Server.GetExistingSystem<ServerScriptMapper>()?._ServerGameManager;
        var position = Util.GetLocalToWorld(character).Position;
        var quickStackDistanceSquared = Math.Pow(_distance.Value, 2);
        var containersSet = new System.Collections.Generic.HashSet<(Entity, float)>();
        foreach (var container in Util.GetContainerEntities())
        {
          if (Util.GetNameableInteractable(container).Name.ToString().Contains(_nameIgnore.Value)) continue;
          if (Util.DistanceSquared(position, Util.GetLocalToWorld(container).Position) > quickStackDistanceSquared) continue;
          if (!gameManager._TeamChecker.IsAllies(character, container)) continue;

          containersSet.Add((container, Util.GetItemSlotFillRatio(container)));
        }

        var containers = System.Linq.Enumerable.ToArray(containersSet);
        Array.Sort(containers, _sorter ?? (_sorter = new QuickStackSorter()));

        var gameDataSystem = Util.GetGameDataSystem();
        var movedTotal = 0;
        var containerLocationsSet = new System.Collections.Generic.HashSet<float3>();
        for (int i = 0; i < containers.Length; ++i)
        {
          if (Util.TrySmartMergeInventories(gameDataSystem.ItemHashLookupMap, characterInventory.Value, containers[i].Item1))
          {
            ++movedTotal;
            containerLocationsSet.Add(Util.GetLocalToWorld(containers[i].Item1).Position);
            if (_sortOnStack.Value)
            {
              Util.TrySortInventory(gameDataSystem.ItemHashLookupMap, containers[i].Item1);
            }
          }
          Plugin.Logger.LogDebug($"Quick Stack merge to container {movedTotal}/{i}/{containers.Length} ({Util.ToPercent(containers[i].Item2)})");
        }

        if (_withDebuff == null)
        {
          _withDebuff = new System.Collections.Generic.HashSet<PrefabGUID>();
          foreach (var item in gameDataSystem.ItemHashLookupMap)
          {
            if (!item.Value.ItemCategory.HasFlag(ItemCategory.Silver)) continue;

            _withDebuff.Add(item.Key);
            Plugin.Logger.LogDebug($"Found item with debuff: {item.Key.GuidHash}");
          }
        }

        foreach (var itemType in _withDebuff)
        {
          InventoryUtilitiesServer.CreateInventoryChangedEvent(VWorld.Server.EntityManager, character, itemType, 0, InventoryChangedEventType.Moved);
        }

        var user = VWorld.Server.EntityManager.GetComponentData<ProjectM.Network.User>(fromCharacter.User);
        VNetwork.SendToClient(user, new QuickStackedMessage { containers = System.Linq.Enumerable.ToArray(containerLocationsSet) });

        Plugin.Logger.LogDebug($"Quick Stack done {movedTotal}/{containers.Length}");
      });

      VNetworkRegistry.RegisterClientbound<QuickStackedMessage>(msg =>
      {
        Plugin.Logger.LogDebug($"Quick Stack QuickStackedMessage recieved {msg.containers.Length}");
      });
    }

    public static void Unload(Plugin plugin)
    {
      KeybindManager.Unregister(_keybinding);
      VNetworkRegistry.UnregisterStruct<QuickStackRequestMessage>();
      VNetworkRegistry.Unregister<QuickStackedMessage>();
    }

    [HarmonyPatch(typeof(GameplayInputSystem), nameof(GameplayInputSystem.HandleInput))]
    [HarmonyPostfix]
    static void HandleInput(GameplayInputSystem __instance, InputState inputState)
    {
      if (!VWorld.IsClient) return;
      if (!_keybinding.IsPressed) return;

      VNetwork.SendToServerStruct<QuickStackRequestMessage>(new()
      {
      });

      Plugin.Logger.LogDebug("QuickStackMessage sent");
    }
  }

  public struct QuickStackRequestMessage
  {

  }

  public struct QuickStackedMessage : VNetworkMessage
  {
    public float3[] containers = Array.Empty<float3>();

    public QuickStackedMessage() { }
    public QuickStackedMessage(float3[] containers)
    {
      this.containers = containers;
    }

    public void Deserialize(NetBufferIn reader)
    {
      var parsed = reader.ReadString(Allocator.Temp);
      if (parsed.Equals("0")) return;

      var containersSet = new System.Collections.Generic.HashSet<float3>();
      foreach (var p in parsed.Split(";"))
      {
        var f = p.Split(":");
        containersSet.Add(new float3(
          float.Parse(f[0]),
          float.Parse(f[1]),
          float.Parse(f[2])
        ));
      }
      containers = System.Linq.Enumerable.ToArray(containersSet);
    }

    public void Serialize(NetBufferOut writer)
    {
      StringBuilder sb = new StringBuilder();
      foreach (var container in containers)
      {
        if (sb.Length > 0) sb.Append(";");
        sb.Append($"{container.x}:{container.y}:{container.z}");
      }
      writer.Write(sb.Length > 0 ? sb.ToString() : "0");
    }
  }

  class QuickStackSorter : System.Collections.Generic.IComparer<(Entity, float)>
  {
    public int Compare((Entity, float) a, (Entity, float) b)
    {
      return (int)Math.Round(b.Item2 * 100 - a.Item2 * 100);
    }
  }
}
