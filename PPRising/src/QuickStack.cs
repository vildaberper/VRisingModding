using BepInEx.Configuration;
using HarmonyLib;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM;
using Stunlock.Network;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wetstone.API;

namespace PPRising
{
  [HarmonyPatch]
  public static class QuickStack
  {
    public static ConfigEntry<bool> _enable;
    public static ConfigEntry<float> _distance;
    public static ConfigEntry<string> _nameIgnore;
    public static ConfigEntry<bool> _sortOnStack;
    public static ConfigEntry<float> _cooldown;

    public static Keybinding _keybinding;

    private static QuickStackSorter _sorter;

    private static readonly Dictionary<int, DateTime> _lastQuickStack = new();

    public static void Load(Plugin plugin)
    {
      _enable = plugin.Config.Bind(new ConfigDefinition("QuickStack", "Enable"), true,
        new ConfigDescription("[SERVER] Enable quick stack")
      );
      _distance = plugin.Config.Bind(new ConfigDefinition("QuickStack", "Distance"), 20f,
        new ConfigDescription("[SERVER] Quick stack distance", new AcceptableValueRange<float>(5f, 100f))
      );
      _nameIgnore = plugin.Config.Bind(new ConfigDefinition("QuickStack", "NameIgnore"), "nostack",
        new ConfigDescription("[SERVER] Ignore quick stacking to containers with name containing")
      );
      _sortOnStack = plugin.Config.Bind(new ConfigDefinition("QuickStack", "SortOnStack"), true,
        new ConfigDescription("[SERVER] Sort container after quick stacking to it")
      );
      _cooldown = plugin.Config.Bind(new ConfigDefinition("QuickStack", "Cooldown"), 1f,
        new ConfigDescription("[SERVER] Quick stack cooldown in seconds", new AcceptableValueRange<float>(0f, 60f))
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

        Plugin.Logger.LogDebug("Recieved QuickStackRequestMessage");

        var user = VWorld.Server.EntityManager.GetComponentData<User>(fromCharacter.User);

        if (_cooldown.Value > 0f)
        {
          DateTime lastQuickStack;
          if (_lastQuickStack.TryGetValue(user.Index, out lastQuickStack))
          {
            var cooldown = _cooldown.Value - (DateTime.Now - lastQuickStack).TotalSeconds;
            if (cooldown > 0d)
            {
              user.SendSystemMessage($"Quick stack is on cooldown ({Util.ToSeconds(cooldown)})");
              return;
            }
          }
        }

        var character = fromCharacter.Character;
        var characterInventory = Util.GetInventoryEntity(character);
        if (!characterInventory.HasValue)
        {
          Plugin.Logger.LogError("Failed to get character inventory entity");
          return;
        }

        Util.GetInventoryItemAmounts(characterInventory.Value);

        var gameManager = VWorld.Server.GetExistingSystem<ServerScriptMapper>()?._ServerGameManager;
        var characterPosition = Util.GetLocalToWorld(character).Position;
        var quickStackDistanceSquared = Math.Pow(_distance.Value, 2);
        var containersSet = new HashSet<(Entity, QuickStackedMessageContainer)>();
        foreach (var container in Util.GetContainerEntities())
        {
          var name = Util.GetNameableInteractable(container).Name.ToString();
          if (name.Contains(_nameIgnore.Value)) continue;

          var position = Util.GetLocalToWorld(container).Position;
          if (Util.DistanceSquared(characterPosition, position) > quickStackDistanceSquared) continue;

          if (!gameManager._TeamChecker.IsAllies(character, container)) continue;

          containersSet.Add((container, new QuickStackedMessageContainer
          {
            name = name,
            fillRatio = Util.GetItemSlotsFillRatio(container),
            position = position
          }));
        }

        var containers = Enumerable.ToArray(containersSet);
        Array.Sort(containers, _sorter ?? (_sorter = new QuickStackSorter()));

        var inventoryItemAmountsBefore = Util.GetInventoryItemAmounts(characterInventory.Value);
        var gameDataSystem = Util.GetGameDataSystem();
        var movedTotal = 0;
        var messageContainersSet = new HashSet<QuickStackedMessageContainer>();
        for (var i = 0; i < containers.Length; ++i)
        {
          if (!Util.TrySmartMergeInventories(gameDataSystem.ItemHashLookupMap, characterInventory.Value, containers[i].Item1)) continue;
          if (_sortOnStack.Value) Util.TrySortInventory(gameDataSystem.ItemHashLookupMap, containers[i].Item1);
          ++movedTotal;
          var fillRatioBefore = Util.ToPercent(containers[i].Item2.fillRatio);
          containers[i].Item2.fillRatio = Util.GetItemSlotsFillRatio(containers[i].Item1);
          messageContainersSet.Add(containers[i].Item2);
          Plugin.Logger.LogDebug($"Quick Stack merge to container {i + 1}/{containers.Length} ({fillRatioBefore} -> {Util.ToPercent(containers[i].Item2.fillRatio)})");
        }

        foreach (var (item, amount) in Util.CompareInventoryItemAmounts(inventoryItemAmountsBefore, Util.GetInventoryItemAmounts(characterInventory.Value)))
        {
          InventoryUtilitiesServer.CreateInventoryChangedEvent(VWorld.Server.EntityManager, character, item, amount, InventoryChangedEventType.Moved);
        }

        Plugin.Logger.LogDebug("Sending QuickStackResultMessage");
        VNetwork.SendToClient(user, new QuickStackResultMessage { containers = Enumerable.ToArray(messageContainersSet) });
      });

      VNetworkRegistry.RegisterClientbound<QuickStackResultMessage>(msg =>
      {
        if (!VWorld.IsClient) return;

        Plugin.Logger.LogDebug("Recieved QuickStackResultMessage");
        foreach (var c in msg.containers)
        {
          Plugin.Logger.LogDebug($" - {c.name} ({Util.ToPercent(c.fillRatio)}) {c.position.x} {c.position.y} {c.position.z}");
        }
      });
    }

    public static void Unload(Plugin plugin)
    {
      KeybindManager.Unregister(_keybinding);
      VNetworkRegistry.UnregisterStruct<QuickStackRequestMessage>();
      VNetworkRegistry.Unregister<QuickStackResultMessage>();
    }

    [HarmonyPatch(typeof(GameplayInputSystem), nameof(GameplayInputSystem.HandleInput))]
    [HarmonyPostfix]
    static void HandleInput(GameplayInputSystem __instance, InputState inputState)
    {
      if (!VWorld.IsClient) return;
      if (!_keybinding.IsPressed) return;

      Plugin.Logger.LogDebug("Sending QuickStackRequestMessage");
      VNetwork.SendToServerStruct<QuickStackRequestMessage>(new() { });
    }
  }

  public struct QuickStackRequestMessage { }

  public struct QuickStackedMessageContainer
  {
    public string name;
    public float fillRatio;
    public float3 position;
  }

  public struct QuickStackResultMessage : VNetworkMessage
  {
    public QuickStackedMessageContainer[] containers = Array.Empty<QuickStackedMessageContainer>();

    public QuickStackResultMessage() { }
    public QuickStackResultMessage(QuickStackedMessageContainer[] containers)
    {
      this.containers = containers;
    }

    public void Deserialize(NetBufferIn reader)
    {
      var parsed = reader.ReadString(Allocator.Temp);
      if (parsed.Equals("0")) return;

      var lines = parsed.Split("\n");
      var containersSet = new HashSet<QuickStackedMessageContainer>();

      for (var i = 0; i < lines.Length; i += 5)
      {
        try
        {
          containersSet.Add(new QuickStackedMessageContainer
          {
            name = lines[i],
            fillRatio = float.Parse(lines[i + 1]),
            position = new float3(
            float.Parse(lines[i + 2]),
            float.Parse(lines[i + 3]),
            float.Parse(lines[i + 4])
          )
          });
        }
        catch
        {
          Plugin.Logger.LogWarning("Failed to parse QuickStackResultMessage");
          break;
        }
      }
      containers = Enumerable.ToArray(containersSet);
    }

    public void Serialize(NetBufferOut writer)
    {
      StringBuilder sb = new StringBuilder();
      foreach (var container in containers)
      {
        if (sb.Length > 0) sb.Append("\n");
        sb.Append($"{container.name}\n{container.fillRatio}\n{container.position.x}\n{container.position.y}\n{container.position.z}");
      }
      writer.Write(sb.Length > 0 ? sb.ToString() : "0");
    }
  }

  class QuickStackSorter : IComparer<(Entity, QuickStackedMessageContainer)>
  {
    public int Compare((Entity, QuickStackedMessageContainer) a, (Entity, QuickStackedMessageContainer) b)
    {
      return (int)Math.Round(b.Item2.fillRatio * 100 - a.Item2.fillRatio * 100);
    }
  }
}
