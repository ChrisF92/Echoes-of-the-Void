using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Roster.Progression.Contracts;
using EchoesOfTheVoid.Core.Roster.Progression.Definitions;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EchoesOfTheVoid.UI.Modals {
  public class EchoSkillTreeModal : UIModal {
    private const string PointsFormat = "{0} SP";

    private Label _titleLabel;
    private Label _pointsLabel;
    private Button _closeButton;
    private ScrollView _treeScrollView;
    private VisualElement _tierContainer;
    private Label _emptyLabel;

    private PlayerEchoData _currentEcho;
    private IEchoSkillTreeDefinition _currentSkillTree;

    private readonly List<NodeViewModel> _nodeViewModels = new();
    private readonly Dictionary<string, NodeViewModel> _nodeLookupById = new(StringComparer.Ordinal);
    private readonly Dictionary<VisualElement, NodeViewModel> _nodeLookupByElement = new();

    private VisualElement _selectedNodeElement;
    private string _selectedNodeId = string.Empty;

    public event Action<NodeViewModel> OnNodeSelected;

#if UNITY_EDITOR
    private void OnValidate() {
      if (string.IsNullOrEmpty(_modalId)) {
        _modalId = "EchoSkillTreeModal";
      }

      if (_modalTemplate == null) {
        _modalTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Modals/EchoSkillTreeModal.uxml");
      }
    }
#endif

    public void ShowForEcho(PlayerEchoData echo, IEchoSkillTreeDefinition skillTree) {
      if (echo == null || skillTree == null) {
        return;
      }

      _currentEcho = echo;
      _currentSkillTree = skillTree;

      UpdateHeader();
      BuildViewModels();
      RenderTree();

      Show();
    }

    public void RefreshState(PlayerEchoData echo) {
      if (echo == null || _currentSkillTree == null) {
        return;
      }

      _currentEcho = echo;
      UpdateHeader();
      BuildViewModels();
      RenderTree();
    }

    public bool TryGetNode(string nodeId, out NodeViewModel viewModel) {
      if (string.IsNullOrWhiteSpace(nodeId)) {
        viewModel = default;
        return false;
      }

      return _nodeLookupById.TryGetValue(nodeId.Trim(), out viewModel);
    }

    protected override void SetupUI() {
      _titleLabel = FindLabel("title-label");
      _pointsLabel = FindLabel("points-label");
      _closeButton = FindButton("close-button");
      _treeScrollView = FindElement<ScrollView>("tree-scroll");
      _tierContainer = FindElement<VisualElement>("tier-container");
      _emptyLabel = FindLabel("empty-label");

      if (_treeScrollView != null) {
        _treeScrollView.mode = ScrollViewMode.Horizontal;
      }

      UpdateHeader();
      ToggleEmptyState(true);
    }

    protected override void BindEvents() {
      base.BindEvents();

      if (_closeButton != null) {
        _closeButton.clicked += Hide;
      }
    }

    protected override void OnHide() {
      base.OnHide();
      ClearSelection();
    }

    private void UpdateHeader() {
      if (_titleLabel != null) {
        string displayName = _currentEcho != null ? _currentEcho.DisplayName : "Skill Tree";
        _titleLabel.text = displayName;
      }

      if (_pointsLabel != null) {
        int points = _currentEcho != null ? _currentEcho.UnspentSkillPoints : 0;
        _pointsLabel.text = string.Format(PointsFormat, points);
      }
    }

    private void BuildViewModels() {
      _nodeViewModels.Clear();
      _nodeLookupByElement.Clear();
      _nodeLookupById.Clear();

      if (_currentSkillTree == null) {
        return;
      }

      IReadOnlyList<SkillTreeNodeDefinition> nodes = _currentSkillTree.Nodes;
      if (nodes == null || nodes.Count == 0) {
        return;
      }

      Dictionary<string, int> depthLookup = CalculateDepths(nodes);

      for (int i = 0; i < nodes.Count; i++) {
        SkillTreeNodeDefinition node = nodes[i];
        if (node == null) {
          continue;
        }

        string nodeId = node.NodeId;
        if (string.IsNullOrWhiteSpace(nodeId)) {
          continue;
        }

        bool unlocked = _currentEcho != null && _currentEcho.HasUnlockedSkillNode(nodeId);
        bool prerequisitesMet = _currentEcho != null && node.ArePrerequisitesSatisfied(_currentEcho.UnlockedSkillNodes);
        NodeState state = DetermineNodeState(unlocked, prerequisitesMet);
        bool hasEnoughSkillPoints = _currentEcho != null && _currentEcho.UnspentSkillPoints >= node.SkillPointCost;
        int depth = depthLookup.TryGetValue(nodeId, out int resolvedDepth) ? Mathf.Max(0, resolvedDepth) : 0;

        var viewModel = new NodeViewModel(
          node,
          state,
          depth,
          prerequisitesMet,
          hasEnoughSkillPoints
        );

        _nodeViewModels.Add(viewModel);
        _nodeLookupById[nodeId] = viewModel;
      }
    }

    private static NodeState DetermineNodeState(bool unlocked, bool prerequisitesMet) {
      if (unlocked) {
        return NodeState.Unlocked;
      }

      return prerequisitesMet ? NodeState.Unlockable : NodeState.Locked;
    }

    private void RenderTree() {
      if (_tierContainer == null) {
        return;
      }

      _tierContainer.Clear();
      _nodeLookupByElement.Clear();
      VisualElement nextSelection = null;

      if (_nodeViewModels.Count == 0) {
        ToggleEmptyState(true);
        return;
      }

      ToggleEmptyState(false);

      var groupedByDepth = new Dictionary<int, List<NodeViewModel>>();
      int maxDepth = 0;

      for (int i = 0; i < _nodeViewModels.Count; i++) {
        NodeViewModel viewModel = _nodeViewModels[i];
        if (!groupedByDepth.TryGetValue(viewModel.Depth, out List<NodeViewModel> list)) {
          list = new List<NodeViewModel>();
          groupedByDepth.Add(viewModel.Depth, list);
        }

        list.Add(viewModel);
        maxDepth = Mathf.Max(maxDepth, viewModel.Depth);
      }

      for (int depth = 0; depth <= maxDepth; depth++) {
        if (!groupedByDepth.TryGetValue(depth, out List<NodeViewModel> tierNodes) || tierNodes.Count == 0) {
          continue;
        }

        tierNodes.Sort((a, b) => string.Compare(a.Definition.DisplayName, b.Definition.DisplayName, StringComparison.Ordinal));

        VisualElement tierElement = CreateTierElement(depth, out VisualElement nodeListContainer);

        for (int i = 0; i < tierNodes.Count; i++) {
          NodeViewModel viewModel = tierNodes[i];
          VisualElement nodeElement = CreateNodeElement(viewModel);
          nodeListContainer.Add(nodeElement);
          _nodeLookupByElement[nodeElement] = viewModel;

          if (!string.IsNullOrEmpty(_selectedNodeId) && string.Equals(_selectedNodeId, viewModel.Definition.NodeId, StringComparison.Ordinal)) {
            nextSelection = nodeElement;
          }
        }

        _tierContainer.Add(tierElement);
      }

      if (nextSelection != null) {
        ApplySelection(nextSelection, _nodeLookupByElement[nextSelection], suppressEvent: true);
      } else {
        ClearSelection();
      }
    }

    private VisualElement CreateTierElement(int depth, out VisualElement nodeContainer) {
      var tierRoot = new VisualElement {
        name = $"tier-{depth}"
      };
      tierRoot.AddToClassList("skill-tree-tier");

      var title = new Label {
        name = "tier-title",
        text = depth == 0 ? "Root" : $"Tier {depth + 1}"
      };
      title.AddToClassList("skill-tree-tier__title");
      tierRoot.Add(title);

      nodeContainer = new VisualElement {
        name = "tier-node-container"
      };
      nodeContainer.AddToClassList("skill-tree-tier__nodes");
      tierRoot.Add(nodeContainer);

      return tierRoot;
    }

    private VisualElement CreateNodeElement(NodeViewModel viewModel) {
      var nodeElement = new VisualElement {
        name = $"node-{viewModel.Definition.NodeId}"
      };
      nodeElement.AddToClassList("skill-tree-node");
      ApplyNodeStateClasses(nodeElement, viewModel);

      var nameLabel = new Label {
        name = "node-name",
        text = viewModel.Definition.DisplayName
      };
      nameLabel.AddToClassList("skill-tree-node__name");
      nodeElement.Add(nameLabel);

      if (viewModel.Definition.SkillPointCost > 0) {
        var costLabel = new Label {
          name = "node-cost",
          text = $"{viewModel.Definition.SkillPointCost} SP"
        };
        costLabel.AddToClassList("skill-tree-node__cost");

        if (!viewModel.HasEnoughSkillPoints && viewModel.State == NodeState.Unlockable) {
          costLabel.AddToClassList("skill-tree-node__cost--insufficient");
        }

        nodeElement.Add(costLabel);
      }

      var statusLabel = new Label {
        name = "node-status",
        text = GetStatusLabel(viewModel)
      };
      statusLabel.AddToClassList("skill-tree-node__status");
      nodeElement.Add(statusLabel);

      nodeElement.RegisterCallback<ClickEvent>(OnNodeClicked);

      return nodeElement;
    }

    private void ApplyNodeStateClasses(VisualElement element, NodeViewModel viewModel) {
      element.RemoveFromClassList("skill-tree-node--locked");
      element.RemoveFromClassList("skill-tree-node--unlockable");
      element.RemoveFromClassList("skill-tree-node--unlocked");
      element.RemoveFromClassList("skill-tree-node--needs-points");

      switch (viewModel.State) {
        case NodeState.Unlocked:
          element.AddToClassList("skill-tree-node--unlocked");
          break;
        case NodeState.Unlockable:
          element.AddToClassList("skill-tree-node--unlockable");
          if (!viewModel.HasEnoughSkillPoints) {
            element.AddToClassList("skill-tree-node--needs-points");
          }

          break;
        default:
          element.AddToClassList("skill-tree-node--locked");
          break;
      }
    }

    private static string GetStatusLabel(NodeViewModel viewModel) {
      return viewModel.State switch {
        NodeState.Unlocked => "Unlocked",
        NodeState.Unlockable when viewModel.HasEnoughSkillPoints => "Unlockable",
        NodeState.Unlockable => "Need Skill Points",
        _ => viewModel.PrerequisitesMet ? "Locked" : "Prerequisites Locked"
      };
    }

    private void OnNodeClicked(ClickEvent evt) {
      if (evt.currentTarget is not VisualElement element) {
        return;
      }

      if (!_nodeLookupByElement.TryGetValue(element, out NodeViewModel viewModel)) {
        return;
      }

      ApplySelection(element, viewModel, suppressEvent: false);
    }

    private void ApplySelection(VisualElement element, NodeViewModel viewModel, bool suppressEvent) {
      if (_selectedNodeElement != null) {
        _selectedNodeElement.RemoveFromClassList("skill-tree-node--selected");
      }

      _selectedNodeElement = element;
      _selectedNodeId = viewModel.Definition.NodeId;

      if (_selectedNodeElement != null) {
        _selectedNodeElement.AddToClassList("skill-tree-node--selected");
      }

      if (!suppressEvent) {
        OnNodeSelected?.Invoke(viewModel);
      }
    }

    private void ClearSelection() {
      if (_selectedNodeElement != null) {
        _selectedNodeElement.RemoveFromClassList("skill-tree-node--selected");
      }

      _selectedNodeElement = null;
      _selectedNodeId = string.Empty;
    }

    private void ToggleEmptyState(bool isEmpty) {
      if (_emptyLabel != null) {
        _emptyLabel.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
      }

      if (_treeScrollView != null) {
        _treeScrollView.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
      }
    }

    private static Dictionary<string, int> CalculateDepths(IReadOnlyList<SkillTreeNodeDefinition> nodes) {
      var depthLookup = new Dictionary<string, int>(StringComparer.Ordinal);
      var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
      var prerequisites = new Dictionary<string, List<string>>(StringComparer.Ordinal);
      var processingQueue = new Queue<string>();

      for (int i = 0; i < nodes.Count; i++) {
        SkillTreeNodeDefinition node = nodes[i];
        if (node == null || string.IsNullOrWhiteSpace(node.NodeId)) {
          continue;
        }

        string nodeId = node.NodeId.Trim();
        var prerequisitesForNode = new List<string>();
        IReadOnlyList<string> nodePrerequisites = node.PrerequisiteNodeIds;
        if (nodePrerequisites != null) {
          for (int j = 0; j < nodePrerequisites.Count; j++) {
            string prerequisiteId = nodePrerequisites[j];
            if (string.IsNullOrWhiteSpace(prerequisiteId)) {
              continue;
            }

            string trimmedPrerequisite = prerequisiteId.Trim();
            prerequisitesForNode.Add(trimmedPrerequisite);

            if (!adjacency.TryGetValue(trimmedPrerequisite, out List<string> children)) {
              children = new List<string>();
              adjacency.Add(trimmedPrerequisite, children);
            }

            if (!children.Contains(nodeId)) {
              children.Add(nodeId);
            }
          }
        }

        prerequisites[nodeId] = prerequisitesForNode;

        if (node.IsRoot || prerequisitesForNode.Count == 0) {
          depthLookup[nodeId] = 0;
          processingQueue.Enqueue(nodeId);
        }
      }

      while (processingQueue.Count > 0) {
        string current = processingQueue.Dequeue();
        int currentDepth = depthLookup[current];

        if (!adjacency.TryGetValue(current, out List<string> children) || children.Count == 0) {
          continue;
        }

        for (int i = 0; i < children.Count; i++) {
          string childId = children[i];
          int candidateDepth = currentDepth + 1;
          if (!depthLookup.TryGetValue(childId, out int existingDepth) || candidateDepth > existingDepth) {
            depthLookup[childId] = candidateDepth;
            processingQueue.Enqueue(childId);
          }
        }
      }

      for (int i = 0; i < nodes.Count; i++) {
        SkillTreeNodeDefinition node = nodes[i];
        if (node == null || string.IsNullOrWhiteSpace(node.NodeId)) {
          continue;
        }

        string nodeId = node.NodeId.Trim();
        if (depthLookup.ContainsKey(nodeId)) {
          continue;
        }

        int derivedDepth = 0;
        if (prerequisites.TryGetValue(nodeId, out List<string> prereqs)) {
          for (int j = 0; j < prereqs.Count; j++) {
            string prerequisiteId = prereqs[j];
            if (depthLookup.TryGetValue(prerequisiteId, out int prerequisiteDepth)) {
              derivedDepth = Mathf.Max(derivedDepth, prerequisiteDepth + 1);
            }
          }
        }

        depthLookup[nodeId] = derivedDepth;
      }

      return depthLookup;
    }

    public readonly struct NodeViewModel {
      public NodeViewModel(
        SkillTreeNodeDefinition definition,
        NodeState state,
        int depth,
        bool prerequisitesMet,
        bool hasEnoughSkillPoints
      ) {
        Definition = definition;
        State = state;
        Depth = Mathf.Max(0, depth);
        PrerequisitesMet = prerequisitesMet;
        HasEnoughSkillPoints = hasEnoughSkillPoints;
      }

      public SkillTreeNodeDefinition Definition { get; }
      public NodeState State { get; }
      public int Depth { get; }
      public bool PrerequisitesMet { get; }
      public bool HasEnoughSkillPoints { get; }
    }

    public enum NodeState {
      Locked,
      Unlockable,
      Unlocked
    }
  }
}
