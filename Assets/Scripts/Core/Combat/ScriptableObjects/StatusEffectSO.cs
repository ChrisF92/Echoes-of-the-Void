using EchoesOfTheVoid.Core.Combat.Systems;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.ScriptableObjects {
  /// <summary>
  /// Designer-friendly status effect definition.
  /// </summary>
  [CreateAssetMenu(fileName = "New Status Effect", menuName = "Combat/Status Effect")]
  public class StatusEffectSO : ScriptableObject {
    [Header("Basic Info")]
    [SerializeField] private string _effectId;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea(2, 4)] private string _description;
    [SerializeField] private Sprite _icon;

    [Header("Effect Properties")]
    [SerializeField] private StatusEffectType _effectType;
    [SerializeField] private int _baseValue;
    [SerializeField] private StatType _targetStat;

    [Header("Duration & Timing")]
    [SerializeField, Min(1)] private int _duration = 3;
    [SerializeField] private EffectTriggerTiming _triggerTiming = EffectTriggerTiming.TurnEnd;

    [Header("Stacking")]
    [SerializeField] private StackBehavior _stackBehavior = StackBehavior.Refresh;
    [SerializeField, Min(1)] private int _maxStacks = 1;

    [Header("Classification")]
    [SerializeField] private bool _isDebuff;

    [Header("Visual & Audio")]
    [SerializeField] private GameObject _visualEffect;
    [SerializeField] private AudioClip _applySound;
    [SerializeField] private AudioClip _tickSound;

    public string EffectId => _effectId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public StatusEffectType EffectType => _effectType;
    public int BaseValue => _baseValue;
    public StatType TargetStat => _targetStat;
    public int Duration => _duration;
    public EffectTriggerTiming TriggerTiming => _triggerTiming;
    public StackBehavior StackBehavior => _stackBehavior;
    public int MaxStacks => _maxStacks;
    public bool IsDebuff => _isDebuff;

    /// <summary>
    /// Create a runtime status effect instance.
    /// </summary>
    public StatusEffect CreateInstance() {
      return new StatusEffect {
        Id = _effectId,
        DisplayName = _displayName,
        Description = _description,
        Icon = _icon,
        EffectType = _effectType,
        BaseValue = _baseValue,
        TargetStat = _targetStat,
        Duration = _duration,
        RemainingTurns = _duration,
        TriggerTiming = _triggerTiming,
        StackBehavior = _stackBehavior,
        MaxStacks = _maxStacks,
        StackCount = 1,
        IsDebuff = _isDebuff
      };
    }

    private void OnValidate() {
      // Auto-generate ID if empty
      if (string.IsNullOrEmpty(_effectId)) {
        _effectId = name.Replace(" ", "_").ToLower();
      }

      // Auto-set display name
      if (string.IsNullOrEmpty(_displayName)) {
        _displayName = name;
      }

      // Ensure logical constraints
      if (_duration < 1) {
        _duration = 1;
      }

      if (_maxStacks < 1) {
        _maxStacks = 1;
      }
    }
  }
}