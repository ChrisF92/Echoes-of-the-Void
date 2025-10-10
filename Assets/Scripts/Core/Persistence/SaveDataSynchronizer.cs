using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Database;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Database;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Systems;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Persistence {
  public class SaveDataSynchronizer {
    private readonly PlayerProfileService _profileService;
    private readonly PlayerRosterService _rosterService;
    private readonly PlayerInventory _playerInventory;
    private readonly ItemDatabase _itemDatabase;
    private readonly CombatantDatabase _combatantDatabase;

    public SaveDataSynchronizer(
      PlayerProfileService profileService,
      PlayerRosterService rosterService,
      PlayerInventory playerInventory,
      ItemDatabase itemDatabase,
      CombatantDatabase combatantDatabase) {

      _profileService = profileService;
      _rosterService = rosterService;
      _playerInventory = playerInventory;
      _itemDatabase = itemDatabase;
      _combatantDatabase = combatantDatabase;
    }

    public void Capture(GameSaveData target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      CapturePlayerProfile(target.Player);
      CaptureInventory(target.Inventory);
      CaptureRoster(target.Roster);
    }

    public void Apply(GameSaveData source) {
      if (source == null) {
        return;
      }

      ApplyPlayerProfile(source.Player);
      ApplyInventory(source.Inventory);
      ApplyRoster(source.Roster);
    }

    private void CapturePlayerProfile(PlayerProfileData target) {
      if (target == null || _profileService == null) {
        return;
      }

      PlayerProfileData snapshot = _profileService.CreateSnapshot();
      target.PlayerName = snapshot.PlayerName;
      target.Level = snapshot.Level;
      target.Experience = snapshot.Experience;
      target.Currency = snapshot.Currency;
    }

    private void ApplyPlayerProfile(PlayerProfileData data) {
      _profileService?.ApplySnapshot(data);
    }

    private void CaptureInventory(InventorySaveData target) {
      if (target == null || _playerInventory == null) {
        return;
      }

      target.IsInitialized = true;
      target.Capacity = _playerInventory.Inventory != null
        ? _playerInventory.Inventory.Capacity
        : Mathf.Max(1, target.Capacity);

      target.Items.Clear();

      foreach (ItemStackData stack in _playerInventory.GetSnapshot()) {
        if (stack?.Item == null || stack.Quantity <= 0) {
          continue;
        }

        string itemId = stack.Item.ItemId;
        if (string.IsNullOrWhiteSpace(itemId)) {
          continue;
        }

        target.Items.Add(new ItemStackRecord {
          ItemId = itemId,
          Quantity = Mathf.Max(0, stack.Quantity)
        });
      }
    }

    private void ApplyInventory(InventorySaveData data) {
      if (_playerInventory == null) {
        return;
      }

      bool isInitialized = data?.IsInitialized ?? false;
      bool hasItems = data?.Items != null && data.Items.Count > 0;
      if (!isInitialized && !hasItems) {
        return;
      }

      int capacity = data != null ? Mathf.Max(1, data.Capacity) : _playerInventory.Inventory?.Capacity ?? 30;
      _playerInventory.Resize(capacity, preserveContents: false, notify: false);
      _playerInventory.Inventory?.Clear(suppressNotifications: true);

      if (data?.Items == null || data.Items.Count == 0) {
        return;
      }

      var payload = new List<ItemStackData>(data.Items.Count);
      foreach (ItemStackRecord record in data.Items) {
        if (record == null || string.IsNullOrWhiteSpace(record.ItemId) || record.Quantity <= 0) {
          continue;
        }

        ItemScriptableObject item = _itemDatabase != null ? _itemDatabase.GetItem(record.ItemId) : null;
        if (item == null) {
          Debug.LogWarning($"[SaveDataSynchronizer] Missing item '{record.ItemId}' while loading inventory.");
          continue;
        }

        payload.Add(new ItemStackData {
          Item = item,
          Quantity = record.Quantity
        });
      }

      _playerInventory.Inventory?.Load(payload, suppressNotifications: true);
    }

    private void CaptureRoster(RosterSaveData target) {
      if (target == null || _rosterService == null) {
        return;
      }

      target.Echoes.Clear();
      target.PartySlots.Clear();
      target.IsInitialized = true;

      IReadOnlyList<PlayerEchoData> echoes = _rosterService.OwnedEchoes;
      for (int i = 0; i < echoes.Count; i++) {
        PlayerEchoData echo = echoes[i];
        if (echo?.Template == null) {
          continue;
        }

        var saveEcho = new EchoSaveData {
          InstanceId = echo.InstanceId,
          TemplateId = echo.TemplateId,
          CustomName = echo.CustomName,
          Level = echo.Level,
          IsLocked = echo.IsLocked,
          PreferredFormationSlot = echo.PreferredFormationSlot,
          ActiveGambitIndex = echo.ActiveGambitSlot
        };

        IReadOnlyList<EquippedItemData> loadout = echo.EquipmentLoadout;
        for (int j = 0; j < loadout.Count; j++) {
          EquippedItemData entry = loadout[j];
          if (entry?.Item == null) {
            continue;
          }

          string itemId = entry.Item.ItemId;
          if (string.IsNullOrWhiteSpace(itemId)) {
            continue;
          }

          saveEcho.Equipment.Add(new EquipmentAssignmentData {
            SlotId = entry.Slot.ToString(),
            ItemId = itemId
          });
        }

        IReadOnlyList<GambitProfileData> gambitSlots = echo.GambitProfiles;
        for (int j = 0; j < gambitSlots.Count; j++) {
          GambitProfileData slotProfile = gambitSlots[j];
          saveEcho.GambitSlots.Add(RosterCloneUtility.DeepClone(slotProfile) ?? new GambitProfileData());
        }

        target.Echoes.Add(saveEcho);
      }

      IReadOnlyList<PlayerRosterService.PartySlotInfo> partySlots = _rosterService.PartySlots;
      for (int i = 0; i < partySlots.Count; i++) {
        PlayerRosterService.PartySlotInfo info = partySlots[i];
        target.PartySlots.Add(new PartySlotSaveData {
          SlotIndex = info.SlotIndex,
          EchoInstanceId = info.EchoInstanceId ?? string.Empty
        });
      }
    }

    private void ApplyRoster(RosterSaveData data) {
      if (_rosterService == null) {
        return;
      }

      bool isInitialized = data?.IsInitialized ?? false;
      bool hasPayload = HasRosterPayload(data);

      if (!isInitialized && !hasPayload) {
        return;
      }

      ClearRoster();

      if (data == null || !hasPayload) {
        return;
      }

      if (data.Echoes != null) {
        foreach (EchoSaveData echoData in data.Echoes) {
          ApplyEcho(echoData);
        }
      }

      if (data.PartySlots != null) {
        foreach (PartySlotSaveData slotData in data.PartySlots) {
          if (slotData == null || slotData.SlotIndex < 0) {
            continue;
          }

          if (string.IsNullOrWhiteSpace(slotData.EchoInstanceId)) {
            continue;
          }

          if (!_rosterService.TryAssignToSlot(slotData.EchoInstanceId, slotData.SlotIndex, out string error, allowSwap: true)) {
            Debug.LogWarning($"[SaveDataSynchronizer] Failed to assign echo '{slotData.EchoInstanceId}' to slot {slotData.SlotIndex}: {error}");
          }
        }
      }
    }

    private void ApplyEcho(EchoSaveData data) {
      if (data == null || string.IsNullOrWhiteSpace(data.TemplateId)) {
        return;
      }

      CombatantSO template = _combatantDatabase != null ? _combatantDatabase.GetCombatant(data.TemplateId) : null;
      if (template == null) {
        Debug.LogWarning($"[SaveDataSynchronizer] Missing combatant template '{data.TemplateId}' while loading roster.");
        return;
      }

      if (!_rosterService.TryAddEcho(template, out PlayerEchoData echo, data.InstanceId)) {
        Debug.LogWarning($"[SaveDataSynchronizer] Could not add echo for template '{data.TemplateId}'.");
        return;
      }

      echo.SetCustomName(data.CustomName);
      echo.SetLevel(Mathf.Max(1, data.Level));
      echo.SetLocked(data.IsLocked);
      echo.SetPreferredFormationSlot(data.PreferredFormationSlot);

      ApplyEchoEquipment(echo, data.Equipment);
      ApplyEchoGambits(echo, data.GambitSlots, data.ActiveGambitIndex);
    }

    private void ApplyEchoEquipment(PlayerEchoData echo, List<EquipmentAssignmentData> equipment) {
      if (echo == null) {
        return;
      }

      if (equipment == null || equipment.Count == 0) {
        echo.SetEquipment(Array.Empty<EquippedItemData>());
        return;
      }

      var loadout = new List<EquippedItemData>(equipment.Count);
      foreach (EquipmentAssignmentData entry in equipment) {
        if (entry == null || string.IsNullOrWhiteSpace(entry.SlotId) || string.IsNullOrWhiteSpace(entry.ItemId)) {
          continue;
        }

        if (!Enum.TryParse(entry.SlotId, out EquipmentSlotType slotType)) {
          Debug.LogWarning($"[SaveDataSynchronizer] Invalid equipment slot '{entry.SlotId}' while loading echo '{echo.InstanceId}'.");
          continue;
        }

        ItemScriptableObject item = _itemDatabase != null ? _itemDatabase.GetItem(entry.ItemId) : null;
        if (item is not EquipmentItemScriptableObject equipmentItem) {
          Debug.LogWarning($"[SaveDataSynchronizer] Missing equipment item '{entry.ItemId}' while loading echo '{echo.InstanceId}'.");
          continue;
        }

        loadout.Add(new EquippedItemData {
          Slot = slotType,
          Item = equipmentItem
        });
      }

      echo.SetEquipment(loadout);
    }

    private void ApplyEchoGambits(PlayerEchoData echo, List<GambitProfileData> gambits, int activeIndex) {
      if (echo == null) {
        return;
      }

      int slotCount = PlayerEchoData.GambitProfileSlotCount;
      for (int i = 0; i < slotCount; i++) {
        GambitProfileData source = gambits != null && i < gambits.Count ? gambits[i] : null;
        GambitProfileData cloned = RosterCloneUtility.DeepClone(source) ?? new GambitProfileData();
        echo.SetGambitProfileSlot(i, cloned);
      }

      echo.SetActiveGambitSlot(Mathf.Clamp(activeIndex, 0, slotCount - 1));
    }

    private static bool HasRosterPayload(RosterSaveData data) {
      if (data == null) {
        return false;
      }

      if (data.Echoes != null) {
        foreach (EchoSaveData echo in data.Echoes) {
          if (echo == null) {
            continue;
          }

          if (!string.IsNullOrWhiteSpace(echo.TemplateId) || !string.IsNullOrWhiteSpace(echo.InstanceId)) {
            return true;
          }
        }
      }

      if (data.PartySlots != null) {
        foreach (PartySlotSaveData slot in data.PartySlots) {
          if (slot != null && !string.IsNullOrWhiteSpace(slot.EchoInstanceId)) {
            return true;
          }
        }
      }

      return false;
    }

    private void ClearRoster() {
      IReadOnlyList<PlayerEchoData> owned = _rosterService.OwnedEchoes;
      var toRemove = new List<string>(owned.Count);
      for (int i = 0; i < owned.Count; i++) {
        PlayerEchoData echo = owned[i];
        if (echo != null) {
          toRemove.Add(echo.InstanceId);
        }
      }

      for (int i = 0; i < toRemove.Count; i++) {
        _ = _rosterService.RemoveEcho(toRemove[i], returnEquipmentToInventory: false);
      }

      IReadOnlyList<PlayerRosterService.PartySlotInfo> partySlots = _rosterService.PartySlots;
      for (int i = 0; i < partySlots.Count; i++) {
        _ = _rosterService.ClearSlot(partySlots[i].SlotIndex);
      }
    }
  }
}
