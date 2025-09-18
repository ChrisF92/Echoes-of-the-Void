using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory.Results;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Combat;

namespace EchoesOfTheVoid.Core.Combat.Wrappers
{
  public class CombatItem
  {
    public ItemScriptableObject Data { get; }

    public CombatItem(ItemScriptableObject data)
    {
      Data = data;
    }

    public ItemResult Use(ICombatant user, ICombatant target)
    {
      foreach (var effectData in Data.effects)
      {
        var actualTarget = effectData.targetSelf ? user : (target ?? user);
        ApplyItemEffect(effectData, actualTarget);
      }

      return ItemResult.Success($"{user.Name} uses {Data.displayName}!");
    }

    private void ApplyItemEffect(ItemEffectData effectData, ICombatant target)
    {
      switch (effectData.effectType)
      {
        case EffectType.Heal:
          target.Heal(effectData.value);
          break;
        case EffectType.Damage:
          target.TakeDamage(effectData.value);
          break;
      }
    }
  }
}
