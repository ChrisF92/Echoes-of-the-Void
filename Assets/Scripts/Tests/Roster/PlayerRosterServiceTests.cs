using NUnit.Framework;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;

namespace EchoesOfTheVoid.Tests.Roster {
  public class PlayerRosterServiceTests {
    private GameObject _context;
    private PlayerRosterService _rosterService;

    [SetUp]
    public void SetUp() {
      _context = new GameObject("PlayerRosterServiceTests");
      _rosterService = _context.AddComponent<PlayerRosterService>();
    }

    [TearDown]
    public void TearDown() {
      if (_context != null) {
        Object.DestroyImmediate(_context);
      }
    }

    [Test]
    public void TryAssignToSlot_AssignsEchoToFirstSlot() {
      CombatantTemplateScriptableObject template = CreateTemplate("echo_primary", "Primary Echo");
      Assert.IsTrue(_rosterService.TryAddEcho(template, out PlayerEchoData echo));

      bool result = _rosterService.TryAssignToSlot(echo.InstanceId, 0, out string errorMessage);

      Assert.IsTrue(result, errorMessage);
      var party = _rosterService.GetPartySnapshot(includeLockedSlots: true);
      Assert.AreEqual(echo.InstanceId, party[0].Echo?.InstanceId);
    }

    [Test]
    public void TryAssignToSlot_FailsWhenSlotLocked() {
      CombatantTemplateScriptableObject template = CreateTemplate("echo_primary", "Primary Echo");
      Assert.IsTrue(_rosterService.TryAddEcho(template, out PlayerEchoData echo));

      bool result = _rosterService.TryAssignToSlot(echo.InstanceId, _rosterService.MaxPartySize + 1, out string errorMessage);

      Assert.IsFalse(result);
      Assert.IsNotEmpty(errorMessage);
    }

    [Test]
    public void TryAssignToSlot_SwapsOccupiedSlots() {
      CombatantTemplateScriptableObject alphaTemplate = CreateTemplate("echo_alpha", "Alpha");
      Assert.IsTrue(_rosterService.TryAddEcho(alphaTemplate, out PlayerEchoData alpha));

      CombatantTemplateScriptableObject betaTemplate = CreateTemplate("echo_beta", "Beta");
      Assert.IsTrue(_rosterService.TryAddEcho(betaTemplate, out PlayerEchoData beta));

      Assert.IsTrue(_rosterService.TryAssignToSlot(alpha.InstanceId, 0, out string errorMessage), errorMessage);
      Assert.IsTrue(_rosterService.TryAssignToSlot(beta.InstanceId, 1, out errorMessage), errorMessage);

      bool swapped = _rosterService.TryAssignToSlot(alpha.InstanceId, 1, out errorMessage);
      Assert.IsTrue(swapped, errorMessage);

      var party = _rosterService.GetPartySnapshot(includeLockedSlots: true);
      Assert.AreEqual(beta.InstanceId, party[0].Echo?.InstanceId);
      Assert.AreEqual(alpha.InstanceId, party[1].Echo?.InstanceId);
    }

    private static CombatantTemplateScriptableObject CreateTemplate(string id, string displayName) {
      var template = ScriptableObject.CreateInstance<CombatantTemplateScriptableObject>();
      template.combatantId = id;
      template.displayName = displayName;
      template.baseStats = new CombatStats {
        Health = 10,
        Mana = 5,
        Attack = 3,
        Defense = 2,
        Speed = 4,
        Luck = 1
      };

      return template;
    }
  }
}

