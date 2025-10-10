using System.Collections.Generic;
using System.Reflection;
using EchoesOfTheVoid.Core.Combat.Database;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Database;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Persistence;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Systems;
using NUnit.Framework;
using UnityEngine;

namespace EchoesOfTheVoid.Tests.Persistence {
  public class SaveDataSynchronizerTests {
    private GameObject _serviceRoot;
    private GameObject _itemDatabaseRoot;
    private GameObject _combatantDatabaseRoot;

    private PlayerProfileService _profileService;
    private PlayerInventory _playerInventory;
    private PlayerRosterService _rosterService;
    private ItemDatabase _itemDatabase;
    private CombatantDatabase _combatantDatabase;
    private SaveDataSynchronizer _synchronizer;

    private EquipmentItemScriptableObject _swordItem;
    private ItemScriptableObject _potionItem;
    private CombatantSO _echoTemplate;

    [SetUp]
    public void SetUp() {
      _serviceRoot = new GameObject("PersistenceTestServices");
      _profileService = _serviceRoot.AddComponent<PlayerProfileService>();
      _playerInventory = _serviceRoot.AddComponent<PlayerInventory>();
      _rosterService = _serviceRoot.AddComponent<PlayerRosterService>();

      // Ensure roster service can resolve inventory in edit mode.
      SetPrivateField(_rosterService, "_playerInventory", _playerInventory);

      _itemDatabaseRoot = new GameObject("ItemDatabase");
      _itemDatabase = _itemDatabaseRoot.AddComponent<ItemDatabase>();

      _combatantDatabaseRoot = new GameObject("CombatantDatabase");
      _combatantDatabase = _combatantDatabaseRoot.AddComponent<CombatantDatabase>();

      _synchronizer = new SaveDataSynchronizer(
        _profileService,
        _rosterService,
        _playerInventory,
        _itemDatabase,
        _combatantDatabase);

      CreateTestAssets();
    }

    [TearDown]
    public void TearDown() {
      if (_serviceRoot != null) {
        Object.DestroyImmediate(_serviceRoot);
      }

      if (_itemDatabaseRoot != null) {
        Object.DestroyImmediate(_itemDatabaseRoot);
      }

      if (_combatantDatabaseRoot != null) {
        Object.DestroyImmediate(_combatantDatabaseRoot);
      }

      if (_swordItem != null) {
        Object.DestroyImmediate(_swordItem);
      }

      if (_potionItem != null) {
        Object.DestroyImmediate(_potionItem);
      }

      if (_echoTemplate != null) {
        Object.DestroyImmediate(_echoTemplate);
      }
    }

    [Test]
    public void CaptureAndApply_RestoresRuntimeState() {
      // Arrange runtime state.
      _profileService.SetPlayerName("Aeris");
      _profileService.SetLevel(12);
      _profileService.SetExperience(345);
      _profileService.SetCurrency(789);

      _playerInventory.Resize(12, preserveContents: false, notify: false);
      Assert.IsTrue(_playerInventory.AddItem(_potionItem, 3));

      Assert.IsTrue(_rosterService.TryAddEcho(_echoTemplate, out PlayerEchoData echo, instanceId: "echo-1"));
      echo.SetCustomName("Echo Prime");
      InvokeInternal(echo, "SetLevel", 5);
      InvokeInternal(echo, "SetLocked", true);
      InvokeInternal(echo, "SetPreferredFormationSlot", new Vector2Int(1, 1));
      InvokeInternal(echo, "SetEquipment", new List<EquippedItemData> {
        new() { Slot = EquipmentSlotType.MainHand, Item = _swordItem }
      });
      InvokeInternal(echo, "SetGambitProfileSlot", 0, new GambitProfileData {
        profileId = "aggressive",
        displayName = "Aggressive"
      });
      InvokeInternal(echo, "SetGambitProfileSlot", 1, new GambitProfileData {
        profileId = "defensive",
        displayName = "Defensive"
      });
      InvokeInternal(echo, "SetActiveGambitSlot", 1);
      Assert.IsTrue(_rosterService.TryAssignToSlot(echo.InstanceId, 0, out string errorMessage), errorMessage);

      var saveData = new GameSaveData();
      _synchronizer.Capture(saveData);

      // Mutate runtime to ensure apply really restores.
      _profileService.SetPlayerName("Temp");
      _profileService.SetLevel(1);
      _profileService.SetExperience(0);
      _profileService.SetCurrency(0);
      _playerInventory.Clear(suppressNotifications: true);

      IReadOnlyList<PlayerEchoData> existing = _rosterService.OwnedEchoes;
      for (int i = existing.Count - 1; i >= 0; i--) {
        _ = _rosterService.RemoveEcho(existing[i].InstanceId, returnEquipmentToInventory: false);
      }

      // Act.
      _synchronizer.Apply(saveData);

      // Assert player profile.
      Assert.AreEqual("Aeris", _profileService.PlayerName);
      Assert.AreEqual(12, _profileService.Level);
      Assert.AreEqual(345, _profileService.Experience);
      Assert.AreEqual(789, _profileService.Currency);

      // Assert inventory.
      Assert.NotNull(_playerInventory.Inventory);
      Assert.AreEqual(12, _playerInventory.Inventory.Capacity);
      Assert.AreEqual(3, _playerInventory.GetItemCount(_potionItem));

      // Assert roster.
      Assert.AreEqual(1, _rosterService.OwnedEchoes.Count);
      PlayerEchoData restoredEcho = _rosterService.OwnedEchoes[0];
      Assert.AreEqual("echo_template_primary", restoredEcho.TemplateId);
      Assert.AreEqual("Echo Prime", restoredEcho.DisplayName);
      Assert.AreEqual(5, restoredEcho.Level);
      Assert.IsTrue(restoredEcho.IsLocked);
      Assert.AreEqual(new Vector2Int(1, 1), restoredEcho.PreferredFormationSlot);

      IReadOnlyList<EquippedItemData> loadout = restoredEcho.EquipmentLoadout;
      Assert.AreEqual(1, loadout.Count);
      Assert.AreEqual(EquipmentSlotType.MainHand, loadout[0].Slot);
      Assert.AreEqual(_swordItem, loadout[0].Item);

      IReadOnlyList<GambitProfileData> gambits = restoredEcho.GambitProfiles;
      Assert.AreEqual(3, gambits.Count);
      Assert.AreEqual("aggressive", gambits[0].profileId);
      Assert.AreEqual("defensive", gambits[1].profileId);
      Assert.AreEqual(1, restoredEcho.ActiveGambitSlot);

      IReadOnlyList<PlayerRosterService.PartySlotInfo> partySlots = _rosterService.PartySlots;
      Assert.AreEqual(restoredEcho.InstanceId, partySlots[0].EchoInstanceId);
    }

    [Test]
    public void ApplyRoster_WhenSaveUninitialized_DoesNotClearExistingRoster() {
      Assert.IsTrue(_rosterService.TryAddEcho(_echoTemplate, out PlayerEchoData echo, instanceId: "echo-default"));

      var saveData = new GameSaveData();

      _synchronizer.Apply(saveData);

      Assert.AreEqual(1, _rosterService.OwnedEchoes.Count);
      Assert.AreSame(echo, _rosterService.OwnedEchoes[0]);
    }

    [Test]
    public void ApplyInventory_WhenSaveUninitialized_DoesNotClearExistingItems() {
      _playerInventory.Resize(6, preserveContents: false, notify: false);
      Assert.IsTrue(_playerInventory.AddItem(_potionItem, 2));

      var saveData = new GameSaveData();

      _synchronizer.Apply(saveData);

      Assert.AreEqual(2, _playerInventory.GetItemCount(_potionItem));
    }

    private void CreateTestAssets() {
      _potionItem = ScriptableObject.CreateInstance<ItemScriptableObject>();
      _potionItem.ItemId = "potion_small";
      _potionItem.DisplayName = "Small Potion";
      _potionItem.ItemType = ItemType.Consumable;
      _potionItem.MaxStackSize = 5;

      _swordItem = ScriptableObject.CreateInstance<EquipmentItemScriptableObject>();
      _swordItem.ItemId = "sword_bronze";
      _swordItem.DisplayName = "Bronze Sword";
      _swordItem.Slot = EquipmentSlotType.MainHand;

      SetPrivateList(_itemDatabase, "_allItems", new List<ItemScriptableObject> { _potionItem, _swordItem });

      _echoTemplate = ScriptableObject.CreateInstance<CombatantSO>();
      _echoTemplate.CombatantId = "echo_template_primary";
      _echoTemplate.DisplayName = "Template Echo";
      _echoTemplate.IsPlayerControlled = true;
      _echoTemplate.StartingEquipment = new List<EquippedItemData> {
        new() { Slot = EquipmentSlotType.MainHand, Item = _swordItem }
      };

      _combatantDatabase.RegisterCombatant(_echoTemplate);
    }

    private static void SetPrivateList<TInstance, TElement>(TInstance instance, string fieldName, List<TElement> values) {
      FieldInfo field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.NotNull(field, $"Field '{fieldName}' not found on {typeof(TInstance).Name}.");
      var list = (List<TElement>)field.GetValue(instance) ?? new List<TElement>();
      list.Clear();
      list.AddRange(values);
      field.SetValue(instance, list);
    }

    private static void SetPrivateField<TInstance>(TInstance instance, string fieldName, object value) {
      FieldInfo field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.NotNull(field, $"Field '{fieldName}' not found on {typeof(TInstance).Name}.");
      field.SetValue(instance, value);
    }

    private static void InvokeInternal(object target, string methodName, params object[] arguments) {
      MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.NotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
      method.Invoke(target, arguments);
    }
  }
}
