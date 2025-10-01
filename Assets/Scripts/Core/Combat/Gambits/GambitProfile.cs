using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Gambits {
  [CreateAssetMenu(fileName = "New Gambit Profile", menuName = "Combat/Gambit Profile")]
  public class GambitProfile : ScriptableObject, IGambitRuleSource {
    [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, ShowIndexLabels = true)]
    public List<GambitRuleDefinition> rules = new();

    public IReadOnlyList<GambitRuleDefinition> Rules => rules;

    public string DisplayName => name;
  }

  [Serializable]
  public class GambitRuleDefinition {
    [LabelWidth(80f)]
    public string RuleName = "New Rule";

    [LabelWidth(80f)]
    public bool IsEnabled = true;

    [SerializeReference]
    [HideReferenceObjectPicker]
    [InlineProperty]
    [LabelText("Target / Condition")]
    public TargetConditionBlock TargetCondition;

    [SerializeReference]
    [HideReferenceObjectPicker]
    [InlineProperty]
    [LabelText("Action")]
    public GambitActionBlock Action;

    public bool HasValidBlocks => TargetCondition != null && Action != null;
  }
}
