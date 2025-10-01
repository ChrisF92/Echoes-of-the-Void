using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory.Results;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Combat.Wrappers {
  public class CombatItem {
    public ItemScriptableObject Data { get; }

    public CombatItem(ItemScriptableObject data) {
      Data = data;
    }

    public ItemResult Use(ICombatant user, ICombatant target) {
      foreach (ItemEffectData effectData in Data.Effects) {
        ICombatant actualTarget = effectData.TargetSelf ? user : (target ?? user);
        ApplyItemEffect(effectData, actualTarget);
      }

      return ItemResult.Success($"{user.Name} uses {Data.DisplayName}!");
    }

    private void ApplyItemEffect(ItemEffectData effectData, ICombatant target) {
      switch (effectData.EffectType) {
        case EffectType.Heal:
          target.Heal(effectData.Value);
          break;
        case EffectType.Damage:
          target.TakeDamage(effectData.Value);
          break;
        case EffectType.Buff:
          break;
        case EffectType.Debuff:
          break;
        case EffectType.StatusEffect:
          break;
        default:
          break;
      }
    }
  }
}
