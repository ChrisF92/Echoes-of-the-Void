using System;
using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Roster.Progression.Payloads;

namespace EchoesOfTheVoid.Core.Roster.Progression.Definitions {
  [Serializable]
  public class SkillTreeNodeDefinition {
    [SerializeField] private string _nodeId = Guid.NewGuid().ToString("N");
    [SerializeField] private string _displayName = "Node";
    [TextArea]
    [SerializeField] private string _description = string.Empty;
    [SerializeField] private bool _isRoot;
    [SerializeField, Min(0)] private int _skillPointCost = 1;
    [SerializeField] private List<string> _prerequisiteNodeIds = new();
    [SerializeField] private EchoSkillNodePayload _payload;

    public string NodeId => string.IsNullOrWhiteSpace(_nodeId) ? string.Empty : _nodeId.Trim();
    public string DisplayName => _displayName;
    public string Description => _description;
    public bool IsRoot => _isRoot;
    public int SkillPointCost => Mathf.Max(0, _skillPointCost);
    public EchoSkillNodePayload Payload => _payload;
    public IReadOnlyList<string> PrerequisiteNodeIds => _prerequisiteNodeIds;

    public bool HasPrerequisites {
      get {
        if (_prerequisiteNodeIds == null) {
          return false;
        }

        for (int i = 0; i < _prerequisiteNodeIds.Count; i++) {
          if (!string.IsNullOrWhiteSpace(_prerequisiteNodeIds[i])) {
            return true;
          }
        }

        return false;
      }
    }

    public bool ArePrerequisitesSatisfied(IReadOnlyList<string> unlockedNodes) {
      if (!HasPrerequisites) {
        return true;
      }

      if (unlockedNodes == null) {
        return false;
      }

      for (int i = 0; i < _prerequisiteNodeIds.Count; i++) {
        string prerequisite = _prerequisiteNodeIds[i];
        if (string.IsNullOrWhiteSpace(prerequisite)) {
          continue;
        }

        string trimmed = prerequisite.Trim();
        bool found = false;
        for (int j = 0; j < unlockedNodes.Count; j++) {
          string unlocked = unlockedNodes[j];
          if (string.Equals(unlocked, trimmed, StringComparison.Ordinal)) {
            found = true;
            break;
          }
        }

        if (!found) {
          return false;
        }
      }

      return true;
    }
  }
}
