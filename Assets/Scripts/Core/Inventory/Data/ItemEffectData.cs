using EchoesOfTheVoid.Core.Combat;

namespace EchoesOfTheVoid.Core.Inventory.Data {
  [System.Serializable]
  public class ItemEffectData {
    public EffectType EffectType;
    public int Value;
    public bool TargetSelf = true;
    public float Duration = 0f;
  }
}
