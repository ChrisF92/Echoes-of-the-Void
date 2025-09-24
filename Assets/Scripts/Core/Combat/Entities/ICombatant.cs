using System;
using EchoesOfTheVoid.Core.Combat.Components;

namespace EchoesOfTheVoid.Core.Combat.Entities
{
  public interface ICombatant
  {
    string Name { get; }
    bool IsAlive { get; }
    bool IsPlayerControlled { get; }
    bool IsAutoCombatEnabled { get; }
    bool IsDefending { get; }
    CombatTeam Team { get; }

    int GetStat(StatType statType);
    int GetMaxStat(StatType statType);
    bool CanUseSkill(string skillId);

    void SetTeam(CombatTeam team);
    void SetDefending(bool defending);
    void SetAutoCombatEnabled(bool enabled);
    void TakeDamage(int damage);
    void Heal(int amount);
    void ConsumeMana(int amount);

    T GetComponent<T>() where T : CombatComponent;
    void UpdateComponents(float deltaTime);

    event Action OnDefeated;
    event Action<int> OnDamaged;
    event Action<int> OnHealed;
    event Action<StatType, int, int> OnStatChanged;
  }
}
