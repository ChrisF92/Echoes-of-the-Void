using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Roster.Data {
  [Serializable]
  public class PlayerEchoData : ISerializationCallbackReceiver {
    public const int GambitProfileSlotCount = 3;

    [SerializeField] private string _instanceId;
    [SerializeField] private CombatantTemplateScriptableObject _template;
    [SerializeField] private string _customName;
    [SerializeField] private int _level = 1;
    [SerializeField] private List<EquippedItemData> _equipmentLoadout = new();
    [SerializeField, FormerlySerializedAs("_gambitProfile")] private GambitProfileData _legacyGambitProfile = new();
    [SerializeField] private List<GambitProfileData> _gambitProfiles = new();
    [SerializeField] private int _activeGambitSlot;
    [SerializeField] private Vector2Int _preferredFormationSlot = new(-1, -1);
    [SerializeField] private bool _isLocked;

    public PlayerEchoData(string instanceId, CombatantTemplateScriptableObject template) {
      _instanceId = !string.IsNullOrWhiteSpace(instanceId)
        ? instanceId
        : Guid.NewGuid().ToString("N");
      _template = template;
      _customName = template != null ? template.displayName : string.Empty;
      EnsureGambitProfileSlots();
    }

    public string InstanceId => _instanceId;
    public CombatantTemplateScriptableObject Template => _template;
    public string TemplateId => _template != null ? _template.combatantId : string.Empty;
    public string DisplayName => !string.IsNullOrWhiteSpace(_customName)
      ? _customName
      : _template != null ? _template.displayName : "Unnamed Echo";
    public int Level => Math.Max(1, _level);
    public IReadOnlyList<EquippedItemData> EquipmentLoadout => _equipmentLoadout;
    public Vector2Int PreferredFormationSlot => _preferredFormationSlot;
    public bool IsLocked => _isLocked;
    public GambitProfileData GambitProfile => GetGambitProfileSlot(ActiveGambitSlot);
    public IReadOnlyList<GambitProfileData> GambitProfiles {
      get {
        EnsureGambitProfileSlots();
        return _gambitProfiles;
      }
    }

    public int ActiveGambitSlot {
      get {
        EnsureGambitProfileSlots();
        return _activeGambitSlot;
      }
    }

    public GambitProfileData GetGambitProfileSlot(int slotIndex) {
      EnsureGambitProfileSlots();
      if (_gambitProfiles.Count == 0) {
        return new GambitProfileData();
      }

      int clamped = Mathf.Clamp(slotIndex, 0, _gambitProfiles.Count - 1);
      return _gambitProfiles[clamped];
    }

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
      SetGambitProfileSlot(ActiveGambitSlot, profile);
    }

    internal void SetGambitProfileSlot(int slotIndex, GambitProfileData profile) {
      EnsureGambitProfileSlots();
      int clamped = Mathf.Clamp(slotIndex, 0, _gambitProfiles.Count - 1);
      _gambitProfiles[clamped] = profile ?? new GambitProfileData();
    }

    internal void SetActiveGambitSlot(int slotIndex) {
      EnsureGambitProfileSlots();
      _activeGambitSlot = Mathf.Clamp(slotIndex, 0, _gambitProfiles.Count - 1);
    }

    internal void SetPreferredFormationSlot(Vector2Int slot) {
      _preferredFormationSlot = slot;
    }

    internal void SetLocked(bool isLocked) {
      _isLocked = isLocked;
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize() {
      EnsureGambitProfileSlots();
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize() {
      EnsureGambitProfileSlots();
    }

    private void EnsureGambitProfileSlots() {
      if (_gambitProfiles == null) {
        _gambitProfiles = new List<GambitProfileData>(GambitProfileSlotCount);
      }

      if (_legacyGambitProfile != null) {
        if (_gambitProfiles.Count == 0) {
          _gambitProfiles.Add(_legacyGambitProfile);
        } else if (!HasProfileContent(_gambitProfiles[0])) {
          _gambitProfiles[0] = _legacyGambitProfile;
        }

        _legacyGambitProfile = null;
      }

      for (int i = _gambitProfiles.Count; i < GambitProfileSlotCount; i++) {
        _gambitProfiles.Add(new GambitProfileData());
      }

      if (_gambitProfiles.Count > GambitProfileSlotCount) {
        _gambitProfiles.RemoveRange(GambitProfileSlotCount, _gambitProfiles.Count - GambitProfileSlotCount);
      }

      for (int i = 0; i < _gambitProfiles.Count; i++) {
        if (_gambitProfiles[i] == null) {
          _gambitProfiles[i] = new GambitProfileData();
        }
      }

      int maxIndex = _gambitProfiles.Count > 0 ? _gambitProfiles.Count - 1 : 0;
      _activeGambitSlot = Mathf.Clamp(_activeGambitSlot, 0, maxIndex);
    }

    private static bool HasProfileContent(GambitProfileData profile) {
      return profile != null && ((profile.rules != null && profile.rules.Count > 0)
        || !string.IsNullOrWhiteSpace(profile.displayName)
        || !string.IsNullOrWhiteSpace(profile.profileId));
    }
  }
}
