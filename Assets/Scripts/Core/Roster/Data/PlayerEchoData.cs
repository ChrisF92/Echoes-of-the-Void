using System;
using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Roster.Data {
  [Serializable]
  public class PlayerEchoData {
    [SerializeField] private string _instanceId;
    [SerializeField] private CombatantTemplateScriptableObject _template;
    [SerializeField] private string _customName;
    [SerializeField] private int _level = 1;
    [SerializeField] private List<EquippedItemData> _equipmentLoadout = new();
    [SerializeField] private GambitProfileData _gambitProfile = new();
    [SerializeField] private Vector2Int _preferredFormationSlot = new(-1, -1);
    [SerializeField] private bool _isLocked;

    public PlayerEchoData(string instanceId, CombatantTemplateScriptableObject template) {
      _instanceId = !string.IsNullOrWhiteSpace(instanceId)
        ? instanceId
        : Guid.NewGuid().ToString("N");
      _template = template;
      _customName = template != null ? template.displayName : string.Empty;
    }

    public string InstanceId => _instanceId;
    public CombatantTemplateScriptableObject Template => _template;
    public string TemplateId => _template != null ? _template.combatantId : string.Empty;
    public string DisplayName => !string.IsNullOrWhiteSpace(_customName)
      ? _customName
      : _template != null ? _template.displayName : "Unnamed Echo";
    public int Level => Math.Max(1, _level);
    public IReadOnlyList<EquippedItemData> EquipmentLoadout => _equipmentLoadout;
    public GambitProfileData GambitProfile => _gambitProfile;
    public Vector2Int PreferredFormationSlot => _preferredFormationSlot;
    public bool IsLocked => _isLocked;

    public void SetCustomName(string newName) {
      _customName = string.IsNullOrWhiteSpace(newName) ? string.Empty : newName.Trim();
    }

    internal void EnsureIdentity() {
      if (!string.IsNullOrWhiteSpace(_instanceId)) {
        return;
      }

      _instanceId = Guid.NewGuid().ToString("N");
    }

    internal void OverrideIdentity(string newInstanceId) {
      _instanceId = string.IsNullOrWhiteSpace(newInstanceId)
        ? Guid.NewGuid().ToString("N")
        : newInstanceId.Trim();
    }

    internal void SetLevel(int level) {
      _level = Math.Max(1, level);
    }

    internal void SetEquipment(IEnumerable<EquippedItemData> loadout) {
      _equipmentLoadout.Clear();
      if (loadout == null) {
        return;
      }

      foreach (EquippedItemData entry in loadout) {
        if (entry == null || entry.Item == null) {
          continue;
        }

        _equipmentLoadout.Add(new EquippedItemData {
          Slot = entry.Slot,
          Item = entry.Item
        });
      }
    }

    internal void SetGambitProfile(GambitProfileData profile) {
      _gambitProfile = profile ?? new GambitProfileData();
    }

    internal void SetPreferredFormationSlot(Vector2Int slot) {
      _preferredFormationSlot = slot;
    }

    internal void SetLocked(bool isLocked) {
      _isLocked = isLocked;
    }
  }
}
