namespace EchoesOfTheVoid.Core.Combat.Data {
  [System.Serializable]
  public class CombatStats {
    public int Health = 100;
    public int Mana = 50;
    public int Attack = 15;
    public int Defense = 10;
    public int Speed = 12;
    public int Luck = 5;

    public CombatStats Clone() {
      return new CombatStats {
        Health = Health,
        Mana = Mana,
        Attack = Attack,
        Defense = Defense,
        Speed = Speed,
        Luck = Luck
      };
    }
  }
}
