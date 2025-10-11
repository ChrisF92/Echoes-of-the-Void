using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Roster.Progression.Contracts;

namespace EchoesOfTheVoid.Core.Roster.Progression.Definitions {
  [CreateAssetMenu(fileName = "SkillTree", menuName = "Roster/Progression/Skill Tree")]
  public class SkillTreeDefinition : ScriptableObject, IEchoSkillTreeDefinition {
    [SerializeField] private string _treeId = "skill-tree";
    [SerializeField] private List<SkillTreeNodeDefinition> _nodes = new();

    private readonly Dictionary<string, SkillTreeNodeDefinition> _lookup = new();
    private readonly List<SkillTreeNodeDefinition> _rootNodes = new();

    public string TreeId => string.IsNullOrWhiteSpace(_treeId) ? name : _treeId;
    public IReadOnlyList<SkillTreeNodeDefinition> Nodes => _nodes;
    public IReadOnlyList<SkillTreeNodeDefinition> RootNodes {
      get {
        EnsureLookup();
        return _rootNodes;
      }
    }

    private void OnValidate() {
      EnsureLookup();
    }

    public bool TryGetNode(string nodeId, out SkillTreeNodeDefinition node) {
      EnsureLookup();
      if (string.IsNullOrWhiteSpace(nodeId)) {
        node = null;
        return false;
      }

      return _lookup.TryGetValue(nodeId.Trim(), out node);
    }

    private void EnsureLookup() {
      _lookup.Clear();
      _rootNodes.Clear();

      if (_nodes == null) {
        return;
      }

      for (int i = 0; i < _nodes.Count; i++) {
        SkillTreeNodeDefinition node = _nodes[i];
        if (node == null) {
          continue;
        }

        string id = node.NodeId;
        if (string.IsNullOrWhiteSpace(id)) {
          continue;
        }

        _lookup[id] = node;
        if (node.IsRoot) {
          _rootNodes.Add(node);
        }
      }
    }
  }
}
