using System;
using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Roster.Progression.Contracts;
using EchoesOfTheVoid.Core.Roster.Progression.Definitions;
using EchoesOfTheVoid.Core.Roster.Progression.Results;

namespace EchoesOfTheVoid.Core.Roster.Progression.Services {
  public class EchoProgressionService : IEchoProgressionService {
    public EchoExperienceGainResult GrantExperience(PlayerEchoData echo, ILevelProgression progression, int experience) {
      if (echo == null || progression == null || experience <= 0) {
        return new EchoExperienceGainResult(0, 0, Array.Empty<int>(), 0);
      }

      int level = echo.Level;
      int pooledExperience = echo.CurrentExperience + Mathf.Max(0, experience);
      var levelsReached = new List<int>();
      int totalSkillPoints = 0;

      while (true) {
        if (progression.IsMaxLevel(level)) {
          int maxCarry = Mathf.Max(0, progression.GetExperienceRequiredForLevel(level));
          pooledExperience = Mathf.Min(pooledExperience, maxCarry);
          break;
        }

        int requiredForNext = progression.GetExperienceRequiredForLevel(level);
        if (requiredForNext <= 0 || pooledExperience < requiredForNext) {
          break;
        }

        pooledExperience -= requiredForNext;
        level++;
        levelsReached.Add(level);
        totalSkillPoints += Mathf.Max(0, progression.GetSkillPointsGrantedAtLevel(level));
      }

      echo.SetLevel(level);
      echo.SetExperience(pooledExperience);

      if (totalSkillPoints > 0) {
        echo.GrantSkillPoints(totalSkillPoints);
      }

      return new EchoExperienceGainResult(experience, levelsReached.Count, levelsReached, totalSkillPoints);
    }

    public SkillUnlockResult TryUnlockNode(PlayerEchoData echo, IEchoSkillTreeDefinition skillTree, string nodeId) {
      if (echo == null) {
        return new SkillUnlockResult(false, null, 0, "Echo data missing.");
      }

      if (skillTree == null) {
        return new SkillUnlockResult(false, null, 0, "Skill tree definition missing.");
      }

      if (!skillTree.TryGetNode(nodeId, out SkillTreeNodeDefinition node)) {
        return new SkillUnlockResult(false, null, 0, $"Unknown skill node '{nodeId}'.");
      }

      if (echo.HasUnlockedSkillNode(node.NodeId)) {
        return new SkillUnlockResult(false, node, 0, "Skill node already unlocked.");
      }

      if (!node.ArePrerequisitesSatisfied(echo.UnlockedSkillNodes)) {
        return new SkillUnlockResult(false, node, 0, "Prerequisites not met.");
      }

      int cost = Mathf.Max(0, node.SkillPointCost);
      if (cost > echo.UnspentSkillPoints) {
        return new SkillUnlockResult(false, node, 0, "Insufficient skill points.");
      }

      if (!echo.TryConsumeSkillPoints(cost)) {
        return new SkillUnlockResult(false, node, 0, "Failed to consume skill points.");
      }

      if (!echo.AddUnlockedSkillNode(node.NodeId)) {
        echo.RestoreSkillPoints(cost);
        return new SkillUnlockResult(false, node, 0, "Failed to add skill node.");
      }

      return new SkillUnlockResult(true, node, cost, string.Empty);
    }

    public void InitializeEcho(PlayerEchoData echo, IEchoSkillTreeDefinition skillTree) {
      if (echo == null || skillTree == null) {
        return;
      }

      IReadOnlyList<SkillTreeNodeDefinition> roots = skillTree.RootNodes;
      if (roots == null || roots.Count == 0) {
        return;
      }

      for (int i = 0; i < roots.Count; i++) {
        SkillTreeNodeDefinition root = roots[i];
        if (root == null) {
          continue;
        }

        _ = echo.AddUnlockedSkillNode(root.NodeId);
      }
    }
  }
}
