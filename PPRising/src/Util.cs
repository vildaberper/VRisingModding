using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wetstone.API;
using UnhollowerRuntimeLib;
using ProjectM;
using ProjectM.CastleBuilding;

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

    public static float GetItemSlotFillRatio(Entity inventory)
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

    public static string ToPercent(float value, int decimals = 1)
    {
      var p = Math.Pow(10, decimals);
      return $"{Math.Round(value * 100 * p) / p}%";
    }
  }
}
