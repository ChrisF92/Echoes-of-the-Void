using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using Sirenix.OdinInspector;

namespace EchoesOfTheVoid.Core.Inventory.Data {
  [System.Serializable]
  public class ItemEffectData {
    public EffectType EffectType;

    [HideIf(nameof(RequiresStatusEffect))]
    public int Value;

    [ShowIf(nameof(RequiresStatusEffect))]
    public StatusEffectSO StatusEffect;

    public bool TargetSelf = true;

    private bool RequiresStatusEffect => EffectType == global::EchoesOfTheVoid.Core.Combat.EffectType.ApplyStatus;
  }
}

