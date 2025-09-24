using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Gambits
{
  [CreateAssetMenu(fileName = "New Gambit Profile", menuName = "Combat/Gambit Profile")]
  public class GambitProfile : ScriptableObject, IGambitRuleSource
  {
    [ListDrawerSettings(DraggableItems = true, ShowFoldout = true, ShowIndexLabels = true)]
    public List<GambitRuleDefinition> rules = new();

    public IReadOnlyList<GambitRuleDefinition> Rules => rules;

    public string DisplayName => name;
  }

  [Serializable]
  public class GambitRuleDefinition
  {
    [LabelWidth(80f)]
    public string ruleName = "New Rule";

    [LabelWidth(80f)]
    public bool isEnabled = true;

    [SerializeReference]
    [HideReferenceObjectPicker]
    [InlineProperty]
    [LabelText("Target / Condition")]
    public TargetConditionBlock targetCondition;

    [SerializeReference]
    [HideReferenceObjectPicker]
    [InlineProperty]
    [LabelText("Action")]
    public GambitActionBlock action;

    public bool HasValidBlocks => targetCondition != null && action != null;
  }
}
