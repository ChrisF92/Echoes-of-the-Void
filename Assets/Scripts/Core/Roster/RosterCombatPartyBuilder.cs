using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Roster.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster {
  public static class RosterCombatPartyBuilder {
    public static List<Combatant> BuildPlayerParty(PlayerRosterService rosterService, Transform parent = null) {
      var result = new List<Combatant>();
      if (rosterService == null) {
        return result;
      }

      IReadOnlyList<PlayerRosterService.PartyMemberSnapshot> snapshot = rosterService.GetPartySnapshot();
      foreach (PlayerRosterService.PartyMemberSnapshot member in snapshot) {
        if (member.IsEmpty || member.Echo == null) {
          continue;
        }

        Combatant combatant = CreateCombatantForEcho(member.Echo, parent);
        if (combatant == null) {
          continue;
        }

        combatant.SetTeam(CombatTeam.Player);
        result.Add(combatant);
      }

      return result;
    }

    public static List<Combatant> BuildEnemyParty(IEnumerable<CombatantSO> templates, Transform parent = null) {
      var result = new List<Combatant>();
      if (templates == null) {
        return result;
      }

      foreach (CombatantSO template in templates) {
        Combatant combatant = CreateEnemyCombatantFromTemplate(template, parent);
        if (combatant == null) {
          continue;
        }

        result.Add(combatant);
      }

      return result;
    }

    public static Combatant CreateEnemyCombatantFromTemplate(CombatantSO template, Transform parent = null) {
      if (template == null) {
        return null;
      }

      var tempEcho = new PlayerEchoData(template.CombatantId, template);
      tempEcho.SetEquipment(template.StartingEquipment);
      tempEcho.SetGambitProfile(RosterCloneUtility.CloneGambitProfile(template.GambitProfile));

      Combatant combatant = CreateCombatantForEcho(tempEcho, parent);
      if (combatant == null) {
        return null;
      }

      combatant.SetTeam(CombatTeam.Enemy);
      combatant.SetAutoCombatEnabled(true);
      return combatant;
    }

    public static Combatant CreateCombatantForEcho(PlayerEchoData echo, Transform parent = null) {
      if (echo == null || echo.Template == null) {
        return null;
      }

      Combatant combatant = InstantiateCombatant(echo.Template, parent);
      if (combatant == null) {
        return null;
      }

      ApplyRosterLoadout(combatant, echo);
      return combatant;
    }

    private static Combatant InstantiateCombatant(CombatantSO template, Transform parent) {
      GameObject spawned;
      Combatant combatant;

      if (template.CombatPrefab != null) {
        spawned = Object.Instantiate(template.CombatPrefab, parent);
        combatant = spawned.GetComponent<Combatant>();
        if (combatant == null) {
          combatant = spawned.AddComponent<Combatant>();
        }
      } else {
        string name = string.IsNullOrWhiteSpace(template.DisplayName) ? "PlayerCombatant" : template.DisplayName;
        spawned = new GameObject(name);
        if (parent != null) {
          spawned.transform.SetParent(parent, false);
        }

        combatant = spawned.AddComponent<Combatant>();
      }

      combatant.InitializeFromTemplate(template);
      return combatant;
    }

    private static void ApplyRosterLoadout(Combatant combatant, PlayerEchoData echo) {
      if (combatant == null || echo == null) {
        return;
      }

      EquipmentComponent equipment = combatant.GetComponent<EquipmentComponent>();
      equipment?.LoadFromSnapshot(echo.EquipmentLoadout, suppressNotifications: true);

      combatant.ApplyGambitProfile(echo.GambitProfile);
    }
  }
}
