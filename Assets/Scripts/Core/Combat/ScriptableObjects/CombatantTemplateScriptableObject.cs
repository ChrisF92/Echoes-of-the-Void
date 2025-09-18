using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Combat.ScriptableObjects
{
  [CreateAssetMenu(fileName = "New Combatant Template", menuName = "Combat/Combatant Template")]
  public class CombatantTemplateScriptableObject : ScriptableObject
  {
    [Header("Basic Info")]
    public string combatantId;
    public string displayName;
    public Sprite portrait;
    public GameObject combatPrefab;

    [Header("Base Stats")]
    public CombatStats baseStats;

    [Header("Starting Skills")]
    public List<SkillScriptableObject> startingSkills = new();

    [Header("Starting Items")]
    public List<ItemStackData> startingItems = new();

    [Header("AI Behavior")]
    public bool isPlayerControlled = false;
  }
}
