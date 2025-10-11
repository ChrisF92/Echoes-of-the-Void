using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Persistence;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Systems;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Run {
  [DisallowMultipleComponent]
  public class CombatRunController : MonoBehaviour {
    [Header("References")]
    [SerializeField] private PlayerRosterService _rosterService;
    [SerializeField] private CombatSystem _combatSystem;
    [SerializeField] private Transform _playerPartyParent;
    [SerializeField] private Transform _enemyPartyParent;
    [SerializeField] private PlayerProfileService _profileService;
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private SaveManager _saveManager;

    [Header("Configuration")]
    [SerializeField] private CombatRunDefinition _defaultRun;

    private readonly CombatRunState _state = new();
    private readonly List<Combatant> _activeEnemies = new();
    private readonly List<PlayerParticipant> _playerParticipants = new();

    private bool _isRunning;
    private bool _isProcessingCombatEnd;
    private bool _pendingNextFloor;
    private float _floorStartTimestamp;
    private int _floorTurnCount;

    public event Action<CombatRunState> OnRunStarted;
    public event Action<CombatRunFloorDefinition, int, IReadOnlyList<Combatant>, IReadOnlyList<Combatant>> OnFloorStarted;
    public event Action<CombatRunFloorResult> OnFloorCompleted;
    public event Action<CombatRunState> OnRunCompleted;
    public event Action<CombatRunState> OnRunCancelled;

    public CombatRunState State => _state;
    public IReadOnlyList<Combatant> CurrentEnemies => _activeEnemies;
    public bool IsRunning => _isRunning;
    public bool HasPendingNextFloor => _pendingNextFloor;

    private void Awake() {
      ResolveDependencies();
    }

    private void OnEnable() {
      SubscribeCombatSystem();
    }

    private void OnDisable() {
      UnsubscribeCombatSystem();
    }

    private void OnDestroy() {
      CleanupActiveRun();
    }

    public bool StartRun(CombatRunDefinition runDefinition = null) {
      if (_isRunning) {
        Debug.LogWarning("Combat run already in progress.", this);
        return false;
      }

      runDefinition ??= _defaultRun;
      if (runDefinition == null) {
        Debug.LogWarning("No combat run definition provided.", this);
        return false;
      }

      if (_rosterService == null) {
        Debug.LogWarning("CombatRunController requires a PlayerRosterService reference.", this);
        return false;
      }

      if (_combatSystem == null) {
        ResolveCombatSystem();
      }

      if (_combatSystem == null) {
        Debug.LogWarning("CombatRunController could not resolve CombatSystem instance.", this);
        return false;
      }

      if (!TryBuildPlayerParty(out List<Combatant> playerParty)) {
        CleanupActiveRun();
        return false;
      }

      _state.Initialize(runDefinition, playerParty);
      _isRunning = true;
      _isProcessingCombatEnd = false;
      _pendingNextFloor = false;
      OnRunStarted?.Invoke(_state);

      return BeginNextFloor();
    }

    public void CancelRun() {
      if (!_isRunning) {
        return;
      }

      _state.MarkCancelled();
      OnRunCancelled?.Invoke(_state);
      CleanupActiveRun();
    }

    public void ReleaseRunState() {
      if (_isRunning) {
        Debug.LogWarning("Cannot release run state while a run is active.", this);
        return;
      }

      _state.Reset();
    }

    public bool ProceedToNextFloor() {
      if (!IsRunning || !HasPendingNextFloor) {
        return false;
      }

      _pendingNextFloor = false;
      return BeginNextFloor();
    }

    private bool BeginNextFloor() {
      while (true) {
        CombatRunFloorDefinition floor = _state.AdvanceFloor();
        if (floor == null) {
          CompleteRun();
          return false;
        }

        _pendingNextFloor = false;
        PreparePlayerPartyForFloor(floor);
        SpawnEnemiesForFloor(floor);

        if (_activeEnemies.Count == 0) {
          Debug.LogWarning($"Combat run floor '{floor.DisplayName}' has no enemies. Skipping floor.", this);
          HandleFloorCompletedWithoutCombat(floor);
          continue;
        }

        _floorStartTimestamp = Time.time;
        _floorTurnCount = 0;

        var playerInterfaces = _state.PlayerParty.Where(static c => c != null).Cast<ICombatant>().ToList();
        var enemyInterfaces = _activeEnemies.Where(static c => c != null).Cast<ICombatant>().ToList();
        if (enemyInterfaces.Count == 0) {
          HandleFloorCompletedWithoutCombat(floor);
          continue;
        }

        OnFloorStarted?.Invoke(floor, _state.CurrentFloorIndex, _state.PlayerParty, _activeEnemies);

        _combatSystem.StartCombat(playerInterfaces, enemyInterfaces);
        return true;
      }
    }

    private void PreparePlayerPartyForFloor(CombatRunFloorDefinition floor) {
      if (floor == null || !floor.HealPartyOnStart) {
        return;
      }

      float healRatio = floor.PlayerHealthRestoreRatio;
      if (healRatio <= 0f) {
        healRatio = 1f;
      }

      foreach (Combatant combatant in _state.PlayerParty) {
        if (combatant == null || !combatant.IsAlive) {
          continue;
        }

        int maxHealth = combatant.GetMaxStat(StatType.Health);
        int currentHealth = combatant.GetStat(StatType.Health);
        int targetHealth = Mathf.Max(currentHealth, Mathf.CeilToInt(maxHealth * healRatio));
        targetHealth = Mathf.Clamp(targetHealth, 0, maxHealth);

        if (targetHealth > currentHealth) {
          combatant.Heal(targetHealth - currentHealth);
        }
      }
    }

    private void SpawnEnemiesForFloor(CombatRunFloorDefinition floor) {
      CleanupEnemies();

      IReadOnlyList<CombatantSO> templates = floor?.EnemyTemplates;
      if (templates == null || templates.Count == 0) {
        return;
      }

      foreach (CombatantSO template in templates) {
        Combatant combatant = RosterCombatPartyBuilder.CreateEnemyCombatantFromTemplate(template, _enemyPartyParent);
        if (combatant != null) {
          _activeEnemies.Add(combatant);
        }
      }
    }

    private bool TryBuildPlayerParty(out List<Combatant> playerParty) {
      playerParty = new List<Combatant>();
      _playerParticipants.Clear();

      IReadOnlyList<PlayerRosterService.PartyMemberSnapshot> snapshot = _rosterService.GetPartySnapshot();
      foreach (PlayerRosterService.PartyMemberSnapshot member in snapshot) {
        if (member.IsEmpty || member.Echo == null) {
          continue;
        }

        PlayerEchoData echoClone = RosterCloneUtility.DeepClone(member.Echo);
        if (echoClone == null) {
          Debug.LogWarning($"Failed to clone echo for roster slot {member.SlotIndex}.", this);
          continue;
        }

        Combatant combatant = RosterCombatPartyBuilder.CreateCombatantForEcho(echoClone, _playerPartyParent);
        if (combatant == null) {
          continue;
        }

        combatant.SetTeam(CombatTeam.Player);
        playerParty.Add(combatant);
        _playerParticipants.Add(new PlayerParticipant(member.SlotIndex, echoClone, combatant));
      }

      if (playerParty.Count == 0) {
        Debug.LogWarning("Combat run requires at least one configured party member.", this);
        return false;
      }

      return true;
    }

    private void HandleCombatEnded(Results.CombatResult result) {
      if (!_isRunning || _isProcessingCombatEnd) {
        return;
      }

      _isProcessingCombatEnd = true;

      CombatRunFloorDefinition floor = _state.CurrentFloor;
      CombatOutcome outcome = result?.Outcome ?? CombatOutcome.Defeat;
      float duration = Time.time - _floorStartTimestamp;

      var floorRewards = new CombatRunRewards();
      if (floor != null) {
        floorRewards.Add(floor.Rewards);
      }

      List<CombatRunCombatantSnapshot> snapshots = _state.CapturePlayerSnapshots();
      CombatRunFloorResult floorResult = _state.RecordFloorResult(floor, outcome, duration, _floorTurnCount, floorRewards, snapshots);
      OnFloorCompleted?.Invoke(floorResult);

      CleanupEnemies();

      if (outcome != CombatOutcome.Victory) {
        CompleteRun();
      } else if (!_state.HasClearedAllFloors) {
        _pendingNextFloor = true;
        _isProcessingCombatEnd = false;
      } else {
        CompleteRun();
      }
    }

    private void HandleTurnEnded(ICombatant combatant) {
      if (!_isRunning) {
        return;
      }

      _floorTurnCount++;
    }

    private void HandleFloorCompletedWithoutCombat(CombatRunFloorDefinition floor) {
      var floorRewards = new CombatRunRewards();
      if (floor != null) {
        floorRewards.Add(floor.Rewards);
      }

      List<CombatRunCombatantSnapshot> snapshots = _state.CapturePlayerSnapshots();
      CombatRunFloorResult floorResult = _state.RecordFloorResult(floor, CombatOutcome.Victory, 0f, 0, floorRewards, snapshots);
      OnFloorCompleted?.Invoke(floorResult);
    }

    private void CompleteRun() {
      if (!_isRunning) {
        return;
      }

      if (_state.Definition != null && _state.HasClearedAllFloors) {
        CombatRunRewardBundle completionRewards = _state.Definition.CompletionRewards;
        if (completionRewards != null && !completionRewards.IsEmpty) {
          _state.Rewards.Add(completionRewards);
        }
      }

      OnRunCompleted?.Invoke(_state);
      ApplyRunRewards();
      CleanupActiveRun(resetState: false);
    }

    private void ApplyRunRewards() {
      CombatRunRewards rewards = _state?.Rewards;
      if (rewards == null || rewards.IsEmpty) {
        return;
      }

      ResolveDependencies();

      if (_profileService != null) {
        if (rewards.Experience > 0) {
          _profileService.AddExperience(rewards.Experience);
        }

        if (rewards.Currency > 0) {
          _profileService.AddCurrency(rewards.Currency);
        }
      }

      if (_playerInventory != null && rewards.ItemTotals.Count > 0) {
        foreach (KeyValuePair<ItemScriptableObject, int> entry in rewards.ItemTotals) {
          if (entry.Key == null || entry.Value <= 0) {
            continue;
          }

          if (!_playerInventory.AddItem(entry.Key, entry.Value)) {
            string itemName = !string.IsNullOrWhiteSpace(entry.Key.DisplayName) ? entry.Key.DisplayName : entry.Key.name;
            Debug.LogWarning($"CombatRunController could not add {entry.Value}x {itemName} to inventory.", this);
          }
        }
      }

      if (rewards.EchoExperience > 0) {
        DistributeEchoExperience(rewards.EchoExperience);
      }

      if (_saveManager != null) {
        _saveManager.SaveGame();
      } else {
        SaveManager.Instance?.SaveGame();
      }
    }

    private void CleanupActiveRun(bool resetState = true) {
      _isRunning = false;
      _isProcessingCombatEnd = false;
      _pendingNextFloor = false;
      CleanupEnemies();
      CleanupPlayers();

      if (resetState) {
        _state.Reset();
      }
    }

    private void CleanupPlayers() {
      for (int i = _playerParticipants.Count - 1; i >= 0; i--) {
        Combatant combatant = _playerParticipants[i].Combatant;
        if (combatant == null) {
          continue;
        }

        if (Application.isPlaying) {
          Destroy(combatant.gameObject);
        } else {
          DestroyImmediate(combatant.gameObject);
        }
      }

      _playerParticipants.Clear();
    }

    private void CleanupEnemies() {
      for (int i = _activeEnemies.Count - 1; i >= 0; i--) {
        Combatant combatant = _activeEnemies[i];
        if (combatant == null) {
          continue;
        }

        if (Application.isPlaying) {
          Destroy(combatant.gameObject);
        } else {
          DestroyImmediate(combatant.gameObject);
        }
      }

      _activeEnemies.Clear();
    }

    private void ResolveDependencies() {
      if (_rosterService == null) {
        _rosterService = FindFirstObjectByType<PlayerRosterService>();
      }

      if (_profileService == null) {
        _profileService = PlayerProfileService.Instance ?? FindFirstObjectByType<PlayerProfileService>();
      }

      if (_playerInventory == null) {
        _playerInventory = FindFirstObjectByType<PlayerInventory>();
      }

      if (_saveManager == null) {
        _saveManager = SaveManager.Instance ?? FindFirstObjectByType<SaveManager>();
      }

      if (_combatSystem == null) {
        ResolveCombatSystem();
      }
    }

    private void ResolveCombatSystem() {
      _combatSystem = CombatSystem.Instance ?? FindFirstObjectByType<CombatSystem>();
    }

    private void SubscribeCombatSystem() {
      if (_combatSystem == null) {
        ResolveCombatSystem();
      }

      if (_combatSystem == null) {
        return;
      }

      _combatSystem.OnCombatEnd -= HandleCombatEnded;
      _combatSystem.OnCombatEnd += HandleCombatEnded;

      _combatSystem.OnTurnEnd -= HandleTurnEnded;
      _combatSystem.OnTurnEnd += HandleTurnEnded;
    }

    private void UnsubscribeCombatSystem() {
      if (_combatSystem == null) {
        return;
      }

      _combatSystem.OnCombatEnd -= HandleCombatEnded;
      _combatSystem.OnTurnEnd -= HandleTurnEnded;
    }

    private readonly struct PlayerParticipant {
      public PlayerParticipant(int slotIndex, PlayerEchoData echo, Combatant combatant) {
        SlotIndex = slotIndex;
        Echo = echo;
        Combatant = combatant;
      }

      public int SlotIndex { get; }
      public PlayerEchoData Echo { get; }
      public Combatant Combatant { get; }
    }

    private void DistributeEchoExperience(int totalExperience) {
      if (totalExperience <= 0) {
        return;
      }

      if (_rosterService == null) {
        Debug.LogWarning("CombatRunController requires a PlayerRosterService to grant echo experience rewards.", this);
        return;
      }

      var uniqueParticipants = new List<PlayerParticipant>(_playerParticipants.Count);
      var seenInstanceIds = new HashSet<string>(StringComparer.Ordinal);
      for (int i = 0; i < _playerParticipants.Count; i++) {
        PlayerParticipant participant = _playerParticipants[i];
        string instanceId = participant.Echo?.InstanceId;
        if (string.IsNullOrWhiteSpace(instanceId) || !seenInstanceIds.Add(instanceId)) {
          continue;
        }

        uniqueParticipants.Add(participant);
      }

      if (uniqueParticipants.Count == 0) {
        return;
      }

      uniqueParticipants.Sort(static (lhs, rhs) => lhs.SlotIndex.CompareTo(rhs.SlotIndex));

      int baseShare = totalExperience / uniqueParticipants.Count;
      int remainder = totalExperience % uniqueParticipants.Count;

      for (int i = 0; i < uniqueParticipants.Count; i++) {
        PlayerParticipant participant = uniqueParticipants[i];
        string instanceId = participant.Echo?.InstanceId;
        if (string.IsNullOrWhiteSpace(instanceId)) {
          continue;
        }

        int share = baseShare;
        if (remainder > 0) {
          share++;
          remainder--;
        }

        if (share <= 0) {
          continue;
        }

        if (!_rosterService.TryGrantExperience(instanceId, share, out _, out string errorMessage)) {
          if (!string.IsNullOrWhiteSpace(errorMessage)) {
            Debug.LogWarning($"CombatRunController could not grant echo experience to '{instanceId}': {errorMessage}", this);
          } else {
            Debug.LogWarning($"CombatRunController could not grant echo experience to '{instanceId}'.", this);
          }
        }
      }
    }
  }
}
