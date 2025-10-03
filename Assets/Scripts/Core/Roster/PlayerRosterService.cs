using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Roster.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster {
  [DisallowMultipleComponent]
  public class PlayerRosterService : MonoBehaviour {
    private const int _formationColumns = 3;
    private const int _formationRows = 3;
    private const int _formationSlotCount = _formationColumns * _formationRows;

    private static readonly Vector2Int[] _defaultFormationSlots = {
      new(0, 0),
      new(1, 0),
      new(2, 0),
      new(0, 1),
      new(1, 1),
      new(2, 1),
      new(0, 2),
      new(1, 2),
      new(2, 2)
    };

    private static readonly EquipmentSlotType[] _defaultSlotLayout = {
      EquipmentSlotType.Head,
      EquipmentSlotType.Chest,
      EquipmentSlotType.Legs,
      EquipmentSlotType.MainHand,
      EquipmentSlotType.OffHand,
      EquipmentSlotType.Accessory,
      EquipmentSlotType.Relic
    };

    public static IReadOnlyList<EquipmentSlotType> EquipmentSlotLayout => _defaultSlotLayout;

    private static readonly Vector2Int _invalidSlot = new(-1, -1);

    [Header("Roster Limits")]
    [SerializeField, Min(1)] private int _maxOwnedEchoes = 10;
    [SerializeField, Range(1, _formationSlotCount)] private int _maxPartySize = 4;
    [Header("References")]
    [SerializeField] private PlayerInventory _playerInventory;

    [Header("State")]
    [SerializeField] private List<PlayerEchoData> _ownedEchoes = new();
    [SerializeField] private List<string> _partyAssignments = new(_formationSlotCount);

    private readonly Dictionary<string, PlayerEchoData> _echoLookup = new(StringComparer.Ordinal);
    private readonly List<PartySlotInfo> _partySlotCache = new(_formationSlotCount);
    private readonly List<PartyMemberSnapshot> _partySnapshotCache = new(_formationSlotCount);

    public event Action<PlayerEchoData> OnEchoAdded;
    public event Action<PlayerEchoData> OnEchoRemoved;
    public event Action<PlayerEchoData> OnEchoUpdated;
    public event Action<int, string, string> OnPartySlotChanged;
    public event Action OnPartyChanged;
    public event Action OnRosterChanged;

    public IReadOnlyList<PlayerEchoData> OwnedEchoes => _ownedEchoes;
    public int MaxOwnedEchoes => Math.Max(1, _maxOwnedEchoes);
    public int MaxPartySize => Mathf.Clamp(_maxPartySize, 1, _formationSlotCount);

    public IReadOnlyList<PartySlotInfo> PartySlots {
      get {
        _partySlotCache.Clear();
        EnsurePartyAssignmentsCapacity();

        for (int i = 0; i < _defaultFormationSlots.Length; i++) {
          string occupant = i < _partyAssignments.Count ? _partyAssignments[i] : string.Empty;
          _partySlotCache.Add(new PartySlotInfo(i, _defaultFormationSlots[i], occupant, i >= MaxPartySize));
        }

        return _partySlotCache;
      }
    }

    private void Awake() {
      EnsureStateContainers();
      EnsurePartyAssignmentsCapacity();
      _ = ResolveInventory();
      RebuildLookup();
      RemoveInvalidAssignments();
    }

    private void OnValidate() {
      EnsureStateContainers();
      if (_maxOwnedEchoes < 1) {
        _maxOwnedEchoes = 1;
      }

      _maxPartySize = Mathf.Clamp(_maxPartySize, 1, _formationSlotCount);
      EnsurePartyAssignmentsCapacity();
      RebuildLookup();
      RemoveInvalidAssignments();
    }

    public IReadOnlyList<PartyMemberSnapshot> GetPartySnapshot(bool includeLockedSlots = false) {
      _partySnapshotCache.Clear();
      EnsurePartyAssignmentsCapacity();

      int limit = includeLockedSlots ? _partyAssignments.Count : Math.Min(MaxPartySize, _partyAssignments.Count);
      for (int i = 0; i < limit; i++) {
        string occupant = _partyAssignments[i];
        _ = TryGetEcho(occupant, out PlayerEchoData echo);
        _partySnapshotCache.Add(new PartyMemberSnapshot(i, _defaultFormationSlots[i], echo));
      }

      return _partySnapshotCache;
    }

    public bool TryGetEcho(string instanceId, out PlayerEchoData echo) {
      if (string.IsNullOrWhiteSpace(instanceId)) {
        echo = null;
        return false;
      }

      return _echoLookup.TryGetValue(instanceId, out echo);
    }

    public bool IsInParty(string instanceId) {
      return FindSlotIndex(instanceId) >= 0;
    }

    public int ActivePartyCount {
      get {
        EnsurePartyAssignmentsCapacity();
        int limit = Math.Min(MaxPartySize, _partyAssignments.Count);
        int count = 0;
        for (int i = 0; i < limit; i++) {
          if (!string.IsNullOrEmpty(_partyAssignments[i])) {
            count++;
          }
        }

        return count;
      }
    }

    public bool TryAssignToSlot(string instanceId, int slotIndex, out string errorMessage, bool allowSwap = true) {
      errorMessage = string.Empty;

      if (!TryGetEcho(instanceId, out PlayerEchoData echo)) {
        errorMessage = "Echo not found.";
        return false;
      }

      if (slotIndex < 0 || slotIndex >= _defaultFormationSlots.Length) {
        errorMessage = "Invalid slot index.";
        return false;
      }

      if (slotIndex >= MaxPartySize) {
        errorMessage = "Slot is currently locked.";
        return false;
      }

      if (echo.IsLocked) {
        errorMessage = "Echo is locked and cannot be assigned.";
        return false;
      }

      EnsurePartyAssignmentsCapacity();

      int currentSlot = FindSlotIndex(instanceId);
      if (currentSlot == slotIndex) {
        return true;
      }

      string previousOccupantId = _partyAssignments[slotIndex];
      bool slotOccupiedByOther = !string.IsNullOrEmpty(previousOccupantId) && previousOccupantId != instanceId;

      if (!allowSwap && slotOccupiedByOther) {
        errorMessage = "Slot already occupied.";
        return false;
      }

      _ = TryGetEcho(previousOccupantId, out PlayerEchoData previousOccupant);
      bool canSwap = allowSwap && slotOccupiedByOther && currentSlot >= 0 && previousOccupant != null;

      if (currentSlot >= 0) {
        _partyAssignments[currentSlot] = string.Empty;
      }

      _partyAssignments[slotIndex] = instanceId;
      echo.SetPreferredFormationSlot(_defaultFormationSlots[slotIndex]);

      if (canSwap) {
        // Swap the displaced echo into the original slot.
        _partyAssignments[currentSlot] = previousOccupantId;
        previousOccupant.SetPreferredFormationSlot(_defaultFormationSlots[currentSlot]);
      } else if (previousOccupant != null) {
        previousOccupant.SetPreferredFormationSlot(_invalidSlot);
      }

      if (currentSlot >= 0) {
        string newOccupant = canSwap ? previousOccupantId : string.Empty;
        OnPartySlotChanged?.Invoke(currentSlot, instanceId, newOccupant);
      }

      OnPartySlotChanged?.Invoke(slotIndex, previousOccupantId, instanceId);

      if (previousOccupant != null) {
        OnEchoUpdated?.Invoke(previousOccupant);
      }

      OnPartyChanged?.Invoke();
      OnEchoUpdated?.Invoke(echo);
      OnRosterChanged?.Invoke();
      return true;
    }

    public bool ClearSlot(int slotIndex) {
      if (slotIndex < 0 || slotIndex >= _partyAssignments.Count) {
        return false;
      }

      string previous = _partyAssignments[slotIndex];
      if (string.IsNullOrEmpty(previous)) {
        return false;
      }

      _partyAssignments[slotIndex] = string.Empty;
      if (TryGetEcho(previous, out PlayerEchoData echo)) {
        echo.SetPreferredFormationSlot(_invalidSlot);
        OnEchoUpdated?.Invoke(echo);
      }

      OnPartySlotChanged?.Invoke(slotIndex, previous, string.Empty);
      OnPartyChanged?.Invoke();
      OnRosterChanged?.Invoke();
      return true;
    }

    public bool RemoveFromParty(string instanceId) {
      int slotIndex = FindSlotIndex(instanceId);
      return slotIndex >= 0 && ClearSlot(slotIndex);
    }

    public bool TryAddEcho(CombatantSO template, out PlayerEchoData echo, string instanceId = null) {
      echo = null;
      if (template == null) {
        return false;
      }

      if (_ownedEchoes.Count >= MaxOwnedEchoes) {
        return false;
      }

      echo = new PlayerEchoData(instanceId, template);
      echo.EnsureIdentity();
      echo.SetGambitProfile(CloneGambitProfile(template.GambitProfile));
      echo.SetEquipment(template.StartingEquipment);
      _ownedEchoes.Add(echo);
      _echoLookup[echo.InstanceId] = echo;

      OnEchoAdded?.Invoke(echo);
      OnRosterChanged?.Invoke();
      return true;
    }

    public bool RemoveEcho(string instanceId, bool returnEquipmentToInventory = true) {
      if (!TryGetEcho(instanceId, out PlayerEchoData echo)) {
        return false;
      }

      if (returnEquipmentToInventory && Application.isPlaying) {
        PlayerInventory inventory = ResolveInventory();
        if (inventory != null) {
          foreach (EquippedItemData entry in echo.EquipmentLoadout) {
            if (entry?.Item == null) {
              continue;
            }

            if (!inventory.AddItem(entry.Item, 1)) {
              Debug.LogWarning($"PlayerRosterService failed to return {entry.Item.DisplayName} to inventory.", this);
            }
          }
        }
      }

      _ = _ownedEchoes.Remove(echo);
      _ = _echoLookup.Remove(instanceId);

      int slotIndex = FindSlotIndex(instanceId);
      if (slotIndex >= 0) {
        _partyAssignments[slotIndex] = string.Empty;
        OnPartySlotChanged?.Invoke(slotIndex, instanceId, string.Empty);
        OnPartyChanged?.Invoke();
      }

      OnEchoRemoved?.Invoke(echo);
      OnRosterChanged?.Invoke();
      return true;
    }

    public bool TryApplyEquipment(string instanceId, IEnumerable<EquippedItemData> requestedLoadout, out string errorMessage) {
      errorMessage = string.Empty;

      if (!TryGetEcho(instanceId, out PlayerEchoData echo)) {
        errorMessage = "Echo not found.";
        return false;
      }

      List<EquippedItemData> sanitizedLoadout = SanitizeLoadout(requestedLoadout);
      Dictionary<EquipmentItemScriptableObject, int> previousCounts = BuildItemCounts(echo.EquipmentLoadout);
      Dictionary<EquipmentItemScriptableObject, int> newCounts = BuildItemCounts(sanitizedLoadout);

      PlayerInventory inventory = ResolveInventory();
      if (inventory == null) {
        errorMessage = "Player inventory reference not assigned.";
        return false;
      }

      var requiredRemovals = new List<(EquipmentItemScriptableObject item, int quantity)>();
      foreach (KeyValuePair<EquipmentItemScriptableObject, int> kvp in newCounts) {
        int previous = previousCounts.TryGetValue(kvp.Key, out int count) ? count : 0;
        int delta = kvp.Value - previous;
        if (delta > 0) {
          requiredRemovals.Add((kvp.Key, delta));
        }
      }

      foreach ((EquipmentItemScriptableObject item, int quantity) in requiredRemovals) {
        if (!inventory.HasItem(item, quantity)) {
          errorMessage = $"Missing {item.DisplayName} x{quantity}.";
          return false;
        }
      }

      var removedItems = new List<(EquipmentItemScriptableObject item, int quantity)>();
      foreach ((EquipmentItemScriptableObject item, int quantity) in requiredRemovals) {
        if (!inventory.RemoveItem(item, quantity)) {
          foreach ((EquipmentItemScriptableObject rollbackItem, int rollbackQuantity) in removedItems) {
            _ = inventory.AddItem(rollbackItem, rollbackQuantity);
          }

          errorMessage = $"Could not remove {item.DisplayName} from inventory.";
          return false;
        }

        removedItems.Add((item, quantity));
      }

      var refunds = new List<(EquipmentItemScriptableObject item, int quantity)>();
      foreach (KeyValuePair<EquipmentItemScriptableObject, int> kvp in previousCounts) {
        int nextCount = newCounts.TryGetValue(kvp.Key, out int count) ? count : 0;
        int delta = kvp.Value - nextCount;
        if (delta > 0) {
          refunds.Add((kvp.Key, delta));
        }
      }

      var appliedRefunds = new List<(EquipmentItemScriptableObject item, int quantity)>();
      foreach ((EquipmentItemScriptableObject item, int quantity) in refunds) {
        if (!inventory.AddItem(item, quantity)) {
          foreach ((EquipmentItemScriptableObject appliedItem, int appliedQuantity) in appliedRefunds) {
            _ = inventory.RemoveItem(appliedItem, appliedQuantity);
          }

          foreach ((EquipmentItemScriptableObject removedItem, int removedQuantity) in removedItems) {
            _ = inventory.AddItem(removedItem, removedQuantity);
          }

          errorMessage = $"Inventory full for {item.DisplayName}.";
          return false;
        }

        appliedRefunds.Add((item, quantity));
      }

      echo.SetEquipment(sanitizedLoadout);
      OnEchoUpdated?.Invoke(echo);
      OnRosterChanged?.Invoke();
      return true;
    }

    public bool TrySetGambitProfile(string instanceId, IGambitRuleSource profile, out string errorMessage, int slotIndex = -1, bool setActiveSlot = true) {
      errorMessage = string.Empty;

      if (!TryGetEcho(instanceId, out PlayerEchoData echo)) {
        errorMessage = "Echo not found.";
        return false;
      }

      int resolvedSlot = slotIndex;
      if (resolvedSlot < 0 || resolvedSlot >= PlayerEchoData.GambitProfileSlotCount) {
        resolvedSlot = echo.ActiveGambitSlot;
      }

      resolvedSlot = Mathf.Clamp(resolvedSlot, 0, PlayerEchoData.GambitProfileSlotCount - 1);

      GambitProfileData cloned = CloneGambitProfile(profile);
      echo.SetGambitProfileSlot(resolvedSlot, cloned);

      if (setActiveSlot) {
        echo.SetActiveGambitSlot(resolvedSlot);
      }

      OnEchoUpdated?.Invoke(echo);
      OnRosterChanged?.Invoke();
      return true;
    }
    private void EnsureStateContainers() {
      _ownedEchoes ??= new List<PlayerEchoData>();
      _partyAssignments ??= new List<string>(_formationSlotCount);
    }

    private void EnsurePartyAssignmentsCapacity() {
      _partyAssignments ??= new List<string>(_formationSlotCount);

      for (int i = _partyAssignments.Count; i < _formationSlotCount; i++) {
        _partyAssignments.Add(string.Empty);
      }

      if (_partyAssignments.Count > _formationSlotCount) {
        _partyAssignments.RemoveRange(_formationSlotCount, _partyAssignments.Count - _formationSlotCount);
      }

      for (int i = 0; i < _partyAssignments.Count; i++) {
        if (_partyAssignments[i] == null) {
          _partyAssignments[i] = string.Empty;
        }
      }
    }

    private void RebuildLookup() {
      _echoLookup.Clear();

      for (int i = _ownedEchoes.Count - 1; i >= 0; i--) {
        PlayerEchoData echo = _ownedEchoes[i];
        if (echo == null) {
          _ownedEchoes.RemoveAt(i);
          continue;
        }

        if (echo.Template == null) {
          if (Application.isPlaying) {
            _ownedEchoes.RemoveAt(i);
          }

          continue;
        }

        echo.EnsureIdentity();
        string id = echo.InstanceId;
        if (string.IsNullOrWhiteSpace(id) || _echoLookup.ContainsKey(id)) {
          string newId;
          do {
            newId = Guid.NewGuid().ToString("N");
          } while (_echoLookup.ContainsKey(newId));

          echo.OverrideIdentity(newId);
          id = newId;
        }

        _echoLookup[id] = echo;
      }
    }

    private void RemoveInvalidAssignments() {
      EnsurePartyAssignmentsCapacity();
      for (int i = 0; i < _partyAssignments.Count; i++) {
        string occupant = _partyAssignments[i];
        if (string.IsNullOrWhiteSpace(occupant) || !_echoLookup.ContainsKey(occupant) || i >= MaxPartySize) {
          _partyAssignments[i] = string.Empty;
        }
      }
    }

    private PlayerInventory ResolveInventory() {
      if (_playerInventory == null && Application.isPlaying) {
        _playerInventory = FindFirstObjectByType<PlayerInventory>();
      }

      return _playerInventory;
    }

    private int FindSlotIndex(string instanceId) {
      if (string.IsNullOrWhiteSpace(instanceId)) {
        return -1;
      }

      for (int i = 0; i < _partyAssignments.Count; i++) {
        if (string.Equals(_partyAssignments[i], instanceId, StringComparison.Ordinal)) {
          return i;
        }
      }

      return -1;
    }

    private static List<EquippedItemData> SanitizeLoadout(IEnumerable<EquippedItemData> loadout) {
      var result = new List<EquippedItemData>();
      if (loadout == null) {
        return result;
      }

      var assignedSlots = new HashSet<EquipmentSlotType>();
      foreach (EquippedItemData entry in loadout) {
        if (entry == null || entry.Item == null) {
          continue;
        }

        EquipmentSlotType slot = entry.Slot;
        if (!IsSlotAllowed(slot)) {
          slot = entry.Item.Slot;
        }

        if (!IsSlotAllowed(slot) || !assignedSlots.Add(slot)) {
          continue;
        }

        result.Add(new EquippedItemData {
          Slot = slot,
          Item = entry.Item
        });
      }

      return result;
    }

    private static bool IsSlotAllowed(EquipmentSlotType slot) {
      for (int i = 0; i < _defaultSlotLayout.Length; i++) {
        if (_defaultSlotLayout[i] == slot) {
          return true;
        }
      }

      return false;
    }

    private static Dictionary<EquipmentItemScriptableObject, int> BuildItemCounts(IEnumerable<EquippedItemData> loadout) {
      var result = new Dictionary<EquipmentItemScriptableObject, int>();
      if (loadout == null) {
        return result;
      }

      foreach (EquippedItemData entry in loadout) {
        if (entry?.Item == null) {
          continue;
        }

        if (!result.TryGetValue(entry.Item, out int count)) {
          count = 0;
        }

        result[entry.Item] = count + 1;
      }

      return result;
    }

    private static GambitProfileData CloneGambitProfile(IGambitRuleSource source) {
      return RosterCloneUtility.CloneGambitProfile(source);
    }

    public readonly struct PartySlotInfo {
      public PartySlotInfo(int slotIndex, Vector2Int gridPosition, string echoInstanceId, bool isLocked) {
        SlotIndex = slotIndex;
        GridPosition = gridPosition;
        EchoInstanceId = echoInstanceId ?? string.Empty;
        IsLocked = isLocked;
      }

      public int SlotIndex { get; }
      public Vector2Int GridPosition { get; }
      public string EchoInstanceId { get; }
      public bool IsLocked { get; }
      public bool IsOccupied => !string.IsNullOrEmpty(EchoInstanceId);
    }

    public readonly struct PartyMemberSnapshot {
      public PartyMemberSnapshot(int slotIndex, Vector2Int gridPosition, PlayerEchoData echo) {
        SlotIndex = slotIndex;
        GridPosition = gridPosition;
        Echo = echo;
      }

      public int SlotIndex { get; }
      public Vector2Int GridPosition { get; }
      public PlayerEchoData Echo { get; }
      public bool IsEmpty => Echo == null;
    }
  }
}





