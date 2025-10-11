using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster.Data {
  [Serializable]
  public class PlayerEchoData : ISerializationCallbackReceiver {
    public const int GambitProfileSlotCount = 3;

    [SerializeField] private string _instanceId;
    [SerializeField] private CombatantSO _template;
    [SerializeField] private string _customName;
    [SerializeField] private int _level = 1;
    [SerializeField] private int _currentExperience;
    [SerializeField] private int _unspentSkillPoints;
    [SerializeField] private List<string> _unlockedSkillNodeIds = new();
    [SerializeField] private List<EquippedItemData> _equipmentLoadout = new();
    [SerializeField] private List<GambitProfileData> _gambitProfiles = new();
    [SerializeField] private int _activeGambitSlot;
    [SerializeField] private Vector2Int _preferredFormationSlot = new(-1, -1);
    [SerializeField] private bool _isLocked;

    public PlayerEchoData() {
      ResetProgressionState();
      EnsureGambitProfileSlots();
    }

    public PlayerEchoData(string instanceId, CombatantSO template) {
      _instanceId = !string.IsNullOrWhiteSpace(instanceId)
        ? instanceId
        : Guid.NewGuid().ToString("N");
      _template = template;
      _customName = template != null ? template.DisplayName : string.Empty;
      ResetProgressionState();
      EnsureGambitProfileSlots();
    }

    public string InstanceId => _instanceId;
    public CombatantSO Template => _template;
    public string TemplateId => _template != null ? _template.CombatantId : string.Empty;
    public string CustomName => _customName;
    public string DisplayName => !string.IsNullOrWhiteSpace(_customName)
      ? _customName
      : _template != null ? _template.DisplayName : "Unnamed Echo";
    public int Level => Math.Max(1, _level);
    public int CurrentExperience => Math.Max(0, _currentExperience);
    public int UnspentSkillPoints => Math.Max(0, _unspentSkillPoints);
    public IReadOnlyList<string> UnlockedSkillNodes {
      get {
        _unlockedSkillNodeIds ??= new List<string>();
        return _unlockedSkillNodeIds;
      }
    }
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

    internal void ResetProgressionState() {
      _level = Math.Max(1, _level);
      _currentExperience = 0;
      _unspentSkillPoints = 0;
      if (_unlockedSkillNodeIds == null) {
        _unlockedSkillNodeIds = new List<string>();
      } else {
        _unlockedSkillNodeIds.Clear();
      }
    }

    internal void SetExperience(int experience) {
      _currentExperience = Mathf.Max(0, experience);
    }

    internal void AdjustExperience(int delta) {
      if (delta == 0) {
        return;
      }

      int next = Mathf.Max(0, _currentExperience + delta);
      _currentExperience = next;
    }

    internal void SetSkillPoints(int points) {
      _unspentSkillPoints = Mathf.Max(0, points);
    }

    internal void GrantSkillPoints(int points) {
      if (points <= 0) {
        return;
      }

      _unspentSkillPoints = Mathf.Max(0, _unspentSkillPoints) + points;
    }

    internal bool TryConsumeSkillPoints(int cost) {
      cost = Mathf.Max(0, cost);
      if (cost == 0) {
        return true;
      }

      if (_unspentSkillPoints < cost) {
        return false;
      }

      _unspentSkillPoints -= cost;
      if (_unspentSkillPoints < 0) {
        _unspentSkillPoints = 0;
      }

      return true;
    }

    internal void RestoreSkillPoints(int points) {
      if (points <= 0) {
        return;
      }

      _unspentSkillPoints = Mathf.Max(0, _unspentSkillPoints) + points;
    }

    internal void SetUnlockedSkillNodes(IEnumerable<string> nodes) {
      _unlockedSkillNodeIds ??= new List<string>();
      _unlockedSkillNodeIds.Clear();
      if (nodes == null) {
        return;
      }

      foreach (string nodeId in nodes) {
        AddUnlockedSkillNode(nodeId);
      }
    }

    internal bool AddUnlockedSkillNode(string nodeId) {
      if (string.IsNullOrWhiteSpace(nodeId)) {
        return false;
      }

      _unlockedSkillNodeIds ??= new List<string>();
      string trimmed = nodeId.Trim();
      if (_unlockedSkillNodeIds.Contains(trimmed)) {
        return false;
      }

      _unlockedSkillNodeIds.Add(trimmed);
      return true;
    }

    internal bool RemoveUnlockedSkillNode(string nodeId) {
      if (string.IsNullOrWhiteSpace(nodeId) || _unlockedSkillNodeIds == null) {
        return false;
      }

      return _unlockedSkillNodeIds.Remove(nodeId.Trim());
    }

    public bool HasUnlockedSkillNode(string nodeId) {
      if (string.IsNullOrWhiteSpace(nodeId) || _unlockedSkillNodeIds == null) {
        return false;
      }

      string trimmed = nodeId.Trim();
      for (int i = 0; i < _unlockedSkillNodeIds.Count; i++) {
        if (string.Equals(_unlockedSkillNodeIds[i], trimmed, StringComparison.Ordinal)) {
          return true;
        }
      }

      return false;
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
      _gambitProfiles ??= new List<GambitProfileData>(GambitProfileSlotCount);

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

      _unlockedSkillNodeIds ??= new List<string>();
    }
  }
}
