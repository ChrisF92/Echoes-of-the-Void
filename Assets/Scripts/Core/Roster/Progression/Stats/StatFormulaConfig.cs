using Sirenix.OdinInspector;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster.Progression.Stats {
  [System.Serializable]
  public struct StatFormulaConfig {
    private const string LinearOrQuadraticCondition = "@FormulaType == EchoesOfTheVoid.Core.Roster.Progression.Stats.StatFormulaType.Linear || FormulaType == EchoesOfTheVoid.Core.Roster.Progression.Stats.StatFormulaType.Quadratic";
    private const string QuadraticCondition = "@FormulaType == EchoesOfTheVoid.Core.Roster.Progression.Stats.StatFormulaType.Quadratic";
    private const string ConstantLinearQuadraticCondition = "@FormulaType == EchoesOfTheVoid.Core.Roster.Progression.Stats.StatFormulaType.Constant || " +
      "FormulaType == EchoesOfTheVoid.Core.Roster.Progression.Stats.StatFormulaType.Linear || " +
      "FormulaType == EchoesOfTheVoid.Core.Roster.Progression.Stats.StatFormulaType.Quadratic";
    private const string ExponentialCondition = "@FormulaType == EchoesOfTheVoid.Core.Roster.Progression.Stats.StatFormulaType.Exponential";

    [EnumToggleButtons]
    [SerializeField] private StatFormulaType _formulaType;

    [Header("Common Parameters")]
    [ShowIf(ConstantLinearQuadraticCondition)]
    [SerializeField] private float _constant;
    [ShowIf(LinearOrQuadraticCondition)]
    [SerializeField] private float _linear;
    [ShowIf(QuadraticCondition)]
    [SerializeField] private float _quadratic;

    [Header("Exponential Parameters")]
    [ShowIf(ExponentialCondition)]
    [SerializeField] private float _exponentialMultiplier;
    [ShowIf(ExponentialCondition)]
    [SerializeField] private float _exponentialRate;

    [Header("Options")]
    [ShowIf(LinearOrQuadraticCondition)]
    [SerializeField, Tooltip("When enabled, level 1 is treated as the first step instead of zero.")] private bool _includeLevelOneStep;

    public StatFormulaType FormulaType => _formulaType;
    public float Constant => _constant;
    public float Linear => _linear;
    public float Quadratic => _quadratic;
    public float ExponentialMultiplier => _exponentialMultiplier;
    public float ExponentialRate => _exponentialRate;
    public bool IncludeLevelOneStep => _includeLevelOneStep;

    public StatFormulaConfig Validate() {
      switch (_formulaType) {
        case StatFormulaType.Exponential:
          if (_exponentialMultiplier <= 0f) {
            _exponentialMultiplier = Mathf.Max(0.0001f, Mathf.Abs(_exponentialMultiplier));
          }

          if (_exponentialRate < -0.99f) {
            _exponentialRate = -0.99f;
          }

          break;
        default:
          break;
      }

      return this;
    }

    public float Evaluate(int level, float baseValue) {
      level = Mathf.Max(1, level);
      switch (_formulaType) {
        case StatFormulaType.None:
          return 0f;
        case StatFormulaType.Constant:
          return _constant;
        case StatFormulaType.Linear:
          return EvaluateLinear(level);
        case StatFormulaType.Quadratic:
          return EvaluateQuadratic(level);
        case StatFormulaType.Exponential:
          return EvaluateExponential(level);
        default:
          return 0f;
      }
    }

    private float EvaluateLinear(int level) {
      int steps = _includeLevelOneStep ? level : Mathf.Max(0, level - 1);
      return _constant + _linear * steps;
    }

    private float EvaluateQuadratic(int level) {
      int steps = _includeLevelOneStep ? level : Mathf.Max(0, level - 1);
      float stepSquared = steps * steps;
      return _constant + _linear * steps + _quadratic * stepSquared;
    }

    private float EvaluateExponential(int level) {
      int steps = Mathf.Max(0, level - 1);
      float multiplier = Mathf.Max(0f, _exponentialMultiplier <= 0f ? 1f : _exponentialMultiplier);
      float rate = Mathf.Max(-0.99f, _exponentialRate);
      float growth = Mathf.Pow(1f + rate, steps);
      return multiplier * growth;
    }
  }
}
