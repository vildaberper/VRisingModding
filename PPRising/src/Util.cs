using ProjectM.CastleBuilding;
using ProjectM;
using System.Collections.Generic;
using System;
using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wetstone.API;

namespace PPRising
{
  public static class Util
  {
    public static EntityManager EntityManager
    {
      get
      {
        return VWorld.IsClient ? VWorld.Client.EntityManager : VWorld.Server.EntityManager;
      }
    }

    public static LocalToWorld GetLocalToWorld(Entity entity)
    {
      return EntityManager.GetComponentData<LocalToWorld>(entity);
    }

    public static NameableInteractable GetNameableInteractable(Entity entity)
    {
      return EntityManager.GetComponentData<NameableInteractable>(entity);
    }

    public static Entity? GetInventoryEntity(Entity entity)
    {
      Entity e;
      InventoryUtilities.TryGetInventoryEntity(EntityManager, entity, out e);
      return e == Entity.Null ? null : e;
    }

    public static Dictionary<PrefabGUID, int> GetInventoryItemAmounts(Entity inventory)
    {
      Dictionary<PrefabGUID, int> items = new();
      foreach (var item in GetInventoryBuffer(inventory))
      {
        if (item.ItemType.GuidHash == 0) continue;

        int amount;
        if (items.TryGetValue(item.ItemType, out amount)) items[item.ItemType] = amount + item.Stacks;
        else items[item.ItemType] = item.Stacks;
      }
      return items;
    }

    public static Dictionary<PrefabGUID, int> CompareInventoryItemAmounts(Dictionary<PrefabGUID, int> from, Dictionary<PrefabGUID, int> to)
    {
      Dictionary<PrefabGUID, int> items = new();
      foreach (var (item, amount) in from)
      {
        int toAmount;
        if (to.TryGetValue(item, out toAmount))
        {
          if (amount == toAmount) continue;
          items[item] = toAmount - amount;
        }
        else items[item] = -amount;
      }
      foreach (var (item, amount) in to)
      {
        if (from.ContainsKey(item)) continue;
        else items[item] = amount;
      }
      return items;
    }

    public static DynamicBuffer<InventoryBuffer> GetInventoryBuffer(Entity inventory)
    {
      return EntityManager.GetBufferFromEntity<InventoryBuffer>()[inventory];
    }

    public static float GetItemSlotsFillRatio(Entity inventory)
    {
      return 1 - InventoryUtilities.GetFreeSlotsCount(EntityManager, inventory) / (float)InventoryUtilities.GetItemSlots(EntityManager, inventory);
    }

    public static GameDataSystem GetGameDataSystem()
    {
      return VWorld.Server.GetExistingSystem<GameDataSystem>();
    }

    public static bool TrySmartMergeInventories(Unity.Collections.NativeHashMap<PrefabGUID, ItemData> ItemHashLookupMap, Entity fromInventoryEntity, Entity toInventoryEntity)
    {
      var movedAny = false;
      InventoryUtilitiesServer.TrySmartMergeInventories(EntityManager, ItemHashLookupMap ?? GetGameDataSystem().ItemHashLookupMap, fromInventoryEntity, toInventoryEntity, out movedAny);
      return movedAny;
    }

    public static bool TrySortInventory(Unity.Collections.NativeHashMap<PrefabGUID, ItemData> ItemHashLookupMap, Entity inventory)
    {
      return InventoryUtilitiesServer.TrySortInventory(EntityManager, ItemHashLookupMap ?? GetGameDataSystem().ItemHashLookupMap, inventory);
    }

    public static NativeArray<Entity> GetEntities(params ComponentType[] requiredComponents)
    {
      return EntityManager.CreateEntityQuery(requiredComponents).ToEntityArray(Allocator.Temp);
    }

    private static ComponentType[] _GetContainerEntities_containerComponents = null;
    public static NativeArray<Entity> GetContainerEntities()
    {
      return GetEntities(_GetContainerEntities_containerComponents ?? (_GetContainerEntities_containerComponents = new[] {
            ComponentType.ReadOnly(Il2CppType.Of<Team>()),
            ComponentType.ReadOnly(Il2CppType.Of<CastleHeartConnection>()),
            ComponentType.ReadOnly(Il2CppType.Of<InventoryBuffer>()),
            ComponentType.ReadOnly(Il2CppType.Of<NameableInteractable>()),
        }));
    }

    public static double DistanceSquared(float3 p0, float3 p1)
    {
      return Math.Pow(p0.x - p1.x, 2) + Math.Pow(p0.y - p1.y, 2) + Math.Pow(p0.z - p1.z, 2);
    }

    public static double Format(double value, int decimals = 1)
    {
      var p = Math.Pow(10, decimals);
      return Math.Round(value * p) / p;
    }

    public static string ToPercent(double value, int decimals = 1)
    {
      return $"{Format(value * 100, decimals)}%";
    }

    public static string ToSeconds(double value, int decimals = 1)
    {
      return $"{Format(value, decimals)}s";
    }
  }
}
