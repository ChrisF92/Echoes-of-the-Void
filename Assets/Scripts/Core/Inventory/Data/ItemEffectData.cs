using EchoesOfTheVoid.Core.Combat;

namespace EchoesOfTheVoid.Core.Inventory.Data
{
  [System.Serializable]
  public class ItemEffectData
  {
    public EffectType effectType;
    public int value;
    public bool targetSelf = true;
    public float duration = 0f;
  }
}
