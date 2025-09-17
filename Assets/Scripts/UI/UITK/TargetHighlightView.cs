using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core;
using EchoesOfTheVoid.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace EchoesOfTheVoid.UI.UITK
{
  /// <summary>
  /// Builds and manages a 6x6 grid view for target highlighting inside the unified HUD (single UIDocument).
  /// Subscribes to <see cref="TargetingSystem"/> to apply the 'highlighted' USS class to valid targets.
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(UIDocument))]
  public sealed class TargetHighlightView : MonoBehaviour
  {
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;

    [Header("Systems")]
    [SerializeField] private TargetingSystem _targetingSystem;
    [SerializeField] private ActionExecutor _actionExecutor;

    private const int Rows = 3;
    private const int Columns = 6;

    private VisualElement _root;
    private VisualElement _gridRoot;
    private readonly VisualElement[,] _cells = new VisualElement[Rows, Columns];
    private readonly ICombatant[,] _gridMap = new ICombatant[Rows, Columns];
    private bool _subscribed;
    private bool _actionSubscribed;

    /// <summary>
    /// Fired when a grid cell is clicked. Argument is the occupant (can be null).
    /// </summary>
    public event Action<ICombatant> CellClicked;

    private void Awake()
    {
      if (_uiDocument == null)
      {
        _uiDocument = GetComponent<UIDocument>();
      }
    }

    private void OnEnable()
    {
      // If already bound to the HUD grid, ensure cached cells and subscribe.
      if (_gridRoot == null)
      {
        TryBindFromDocument();
      }
      if (_gridRoot != null)
      {
        EnsureCells();
        Subscribe();
        SubscribeAction();
        if (_targetingSystem != null)
        {
          ApplyHighlights(_targetingSystem.HighlightedTargets);
        }
        UpdateAllLabels();
      }
    }

    private void OnDisable()
    {
      Unsubscribe();
      UnsubscribeAction();
      if (_gridRoot != null)
      {
        // No need to clear cells; they will be re-bound on next enable.
      }
    }

    /// <summary>
    /// Sets the full grid combatant mapping.
    /// </summary>
    public void SetGrid(ICombatant[,] grid)
    {
      if (grid == null)
      {
        throw new ArgumentNullException(nameof(grid));
      }
      int rMax = Math.Min(Rows, grid.GetLength(0));
      int cMax = Math.Min(Columns, grid.GetLength(1));
      for (int r = 0; r < rMax; r++)
      {
        for (int c = 0; c < cMax; c++)
        {
          _gridMap[r, c] = grid[r, c];
        }
      }

      // Re-apply highlights with the new mapping.
      if (_targetingSystem != null)
      {
        ApplyHighlights(_targetingSystem.HighlightedTargets);
      }
      UpdateAllLabels();
    }

    /// <summary>
    /// Sets a single grid cell mapping.
    /// </summary>
    public void SetCombatantAt(int row, int column, ICombatant combatant)
    {
      if (row < 0 || row >= Rows || column < 0 || column >= Columns)
      {
        return;
      }
      _gridMap[row, column] = combatant;
      UpdateCellLabels(row, column, combatant);
    }

    /// <summary>
    /// Injects the targeting system reference.
    /// </summary>
    public void Configure(TargetingSystem targetingSystem, ActionExecutor actionExecutor = null)
    {
      bool targetingChanged = _targetingSystem != targetingSystem;
      if (targetingChanged)
      {
        Unsubscribe();
        _targetingSystem = targetingSystem;
        Subscribe();
      }

      if (actionExecutor != null && _actionExecutor != actionExecutor)
      {
        UnsubscribeAction();
        _actionExecutor = actionExecutor;
      }

      // Ensure subscriptions are active for both systems.
      SubscribeAction();
    }

    /// <summary>
    /// Allows the controller to inject the HUD objects deterministically.
    /// </summary>
    public void BindToGrid(VisualElement gridRoot, UIDocument document = null)
    {
      if (document != null)
      {
        _uiDocument = document;
      }
      _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
      _gridRoot = gridRoot;
      if (_gridRoot == null)
      {
        return;
      }
      EnsureCells();
      Subscribe();
      // Ensure we are subscribed to action outcome events even if OnEnable ran before the HUD existed.
      SubscribeAction();
      if (_targetingSystem != null)
      {
        ApplyHighlights(_targetingSystem.HighlightedTargets);
      }
      // Now that we're bound to the HUD grid, populate the name/HP labels
      // from the current grid mapping.
      UpdateAllLabels();
    }

    private void TryBindFromDocument()
    {
      if (_uiDocument == null)
      {
        Debug.LogError("TargetHighlightView: UIDocument is missing.");
        return;
      }
      _root = _uiDocument.rootVisualElement;
      if (_root == null)
      {
        return;
      }
      _gridRoot = _root.Q<VisualElement>("combat-grid");
      if (_gridRoot != null)
      {
        EnsureCells();
        UpdateAllLabels();
      }
    }

    private void CreateCells(VisualElement container)
    {
      container.Clear();
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          string name = CellName(r, c);
          var cell = new VisualElement { name = name };
          cell.AddToClassList("grid-cell");
          int rr = r;
          int cc = c;
          cell.RegisterCallback<ClickEvent>(_ => OnCellClicked(rr, cc));
          container.Add(cell);
        }
      }
    }

    private void CacheCells()
    {
      if (_gridRoot == null)
      {
        return;
      }
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          _cells[r, c] = _gridRoot.Q<VisualElement>(CellName(r, c));
          if (_cells[r, c] == null)
          {
            // Create missing cell to maintain a full grid.
            var cell = new VisualElement { name = CellName(r, c) };
            cell.AddToClassList("grid-cell");
            _gridRoot.Add(cell);
            _cells[r, c] = cell;
          }
          // Ensure click handler is registered once per build.
          int rr = r;
          int cc = c;
          _cells[r, c].RegisterCallback<ClickEvent>(_ => OnCellClicked(rr, cc));
        }
      }
    }

    private void EnsureCells()
    {
      if (_gridRoot == null)
      {
        return;
      }
      // Only create cells if the expected named elements are not present in the HUD.
      if (_gridRoot.Q<VisualElement>(CellName(0, 0)) == null)
      {
        CreateCells(_gridRoot);
      }
      CacheCells();
    }

    private void OnCellClicked(int row, int column)
    {
      ICombatant target = null;
      if (row >= 0 && row < Rows && column >= 0 && column < Columns)
      {
        target = _gridMap[row, column];
      }
      Debug.Log($"[UI] Cell clicked r{row} c{column} -> {(target != null ? target.Name : "empty")}");
      CellClicked?.Invoke(target);
    }

    private void Subscribe()
    {
      if (_targetingSystem != null && !_subscribed)
      {
        _targetingSystem.TargetsHighlighted += OnTargetsHighlighted;
        _subscribed = true;
      }
    }

    private void Unsubscribe()
    {
      if (_targetingSystem != null && _subscribed)
      {
        _targetingSystem.TargetsHighlighted -= OnTargetsHighlighted;
        _subscribed = false;
      }
    }

    private void SubscribeAction()
    {
      if (_actionExecutor != null && !_actionSubscribed)
      {
        _actionExecutor.DamageReported += OnDamageReported;
        _actionExecutor.HealReported += OnHealReported;
        _actionExecutor.ActionCompleted += OnActionCompleted;
        _actionSubscribed = true;
      }
    }

    private void UnsubscribeAction()
    {
      if (_actionExecutor != null && _actionSubscribed)
      {
        _actionExecutor.DamageReported -= OnDamageReported;
        _actionExecutor.HealReported -= OnHealReported;
        _actionExecutor.ActionCompleted -= OnActionCompleted;
        _actionSubscribed = false;
      }
    }

    private void OnActionCompleted(ActionExecutor.ActionExecutionResult result)
    {
      try
      {
        var ctx = result?.Context;
        if (ctx == null) return;
        if (ctx.Target != null && TryFindPosition(ctx.Target, out int tr, out int tc))
        {
          UpdateCellLabels(tr, tc, ctx.Target);
        }
        if (ctx.User != null && TryFindPosition(ctx.User, out int ur, out int uc))
        {
          UpdateCellLabels(ur, uc, ctx.User);
        }
      }
      catch { }
    }

    private void OnTargetsHighlighted(IReadOnlyList<ICombatant> targets)
    {
      ApplyHighlights(targets);
    }

    private void OnDamageReported(ActionExecutor.DamageReport report)
    {
      if (report?.Target == null) return;
      if (TryFindPosition(report.Target, out int r, out int c))
      {
        UpdateCellLabels(r, c, report.Target);
      }
    }

    private void OnHealReported(ActionExecutor.HealReport report)
    {
      if (report?.Target == null) return;
      if (TryFindPosition(report.Target, out int r, out int c))
      {
        UpdateCellLabels(r, c, report.Target);
      }
    }

    private void ApplyHighlights(IReadOnlyList<ICombatant> targets)
    {
      // Remove previous highlight classes.
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          VisualElement cell = _cells[r, c];
          if (cell != null)
          {
            cell.RemoveFromClassList("highlighted");
            cell.RemoveFromClassList("highlighted-ally");
            cell.RemoveFromClassList("highlighted-enemy");
          }
        }
      }

      if (targets == null || targets.Count == 0)
      {
        return;
      }

      var set = new HashSet<ICombatant>(targets);
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          ICombatant occupant = _gridMap[r, c];
          if (occupant != null && set.Contains(occupant))
          {
            VisualElement cell = _cells[r, c];
            cell?.AddToClassList("highlighted");
            if (cell != null)
            {
              bool isEnemySide = c >= (Columns / 2);
              cell.AddToClassList(isEnemySide ? "highlighted-enemy" : "highlighted-ally");
            }
          }
        }
      }
    }

    private static string CellName(int row, int column)
    {
      return $"cell-r{row}-c{column}";
    }

    private static string NameElementName(int row, int column)
    {
      return $"cell-name-r{row}-c{column}";
    }

    private static string HpElementName(int row, int column)
    {
      return $"cell-hp-r{row}-c{column}";
    }
    private static string HpBarElementName(int row, int column)
    {
      return $"cell-hpbar-r{row}-c{column}";
    }
    private static string HpFillElementName(int row, int column)
    {
      return $"cell-hpfill-r{row}-c{column}";
    }

    private void UpdateAllLabels()
    {
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          UpdateCellLabels(r, c, _gridMap[r, c]);
        }
      }
    }

    private void UpdateCellLabels(int row, int column, ICombatant occupant)
    {
      if (_gridRoot == null)
      {
        return;
      }
      VisualElement cell = _cells[row, column] ?? _gridRoot.Q<VisualElement>(CellName(row, column));
      if (cell == null)
      {
        return;
      }

      Label nameLabel = cell.Q<Label>(NameElementName(row, column));
      Label hpLabel = cell.Q<Label>(HpElementName(row, column));
      VisualElement hpBar = cell.Q<VisualElement>(HpBarElementName(row, column));
      VisualElement hpFill = cell.Q<VisualElement>(HpFillElementName(row, column));
      if (nameLabel == null)
      {
        nameLabel = new Label { name = NameElementName(row, column) };
        nameLabel.AddToClassList("cell-name");
        cell.Add(nameLabel);
      }
      if (hpLabel == null)
      {
        hpLabel = new Label { name = HpElementName(row, column) };
        hpLabel.AddToClassList("cell-hp");
        cell.Add(hpLabel);
      }
      if (hpBar == null)
      {
        hpBar = new VisualElement { name = HpBarElementName(row, column) };
        hpBar.AddToClassList("hp-bar");
        if (hpFill == null)
        {
          hpFill = new VisualElement { name = HpFillElementName(row, column) };
          hpFill.AddToClassList("hp-fill");
          hpBar.Add(hpFill);
        }
        cell.Add(hpBar);
      }
      else if (hpFill == null)
      {
        hpFill = new VisualElement { name = HpFillElementName(row, column) };
        hpFill.AddToClassList("hp-fill");
        hpBar.Add(hpFill);
      }

      if (occupant == null)
      {
        nameLabel.text = string.Empty;
        hpLabel.text = string.Empty;
        if (hpFill != null)
        {
          hpFill.style.width = new Length(0f, LengthUnit.Percent);
        }
        return;
      }

      nameLabel.text = occupant.Name ?? string.Empty;
      int maxHp = 0;
      try
      {
        maxHp = occupant.MaxHealth;
      }
      catch
      {
        maxHp = occupant.Health;
      }
      hpLabel.text = $"{occupant.Health}/{maxHp}";
      if (hpFill != null)
      {
        float pct = (maxHp > 0) ? Mathf.Clamp01((float)occupant.Health / Mathf.Max(1, maxHp)) : 0f;
        hpFill.style.width = new Length(pct * 100f, LengthUnit.Percent);
      }
    }

    private bool TryFindPosition(ICombatant target, out int row, out int column)
    {
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          if (ReferenceEquals(_gridMap[r, c], target))
          {
            row = r;
            column = c;
            return true;
          }
        }
      }
      row = -1;
      column = -1;
      return false;
    }
  }
}
