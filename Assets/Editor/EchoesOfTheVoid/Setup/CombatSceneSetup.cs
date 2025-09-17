using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Combat;
using EchoesOfTheVoid.Core;
using EchoesOfTheVoid.UI.UITK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace EchoesOfTheVoid.Editor.Setup
{
  /// <summary>
  /// Editor utilities to scaffold and wire the combat testing scene.
  /// </summary>
  public static class CombatSceneSetup
  {
    private const string TestingScenePath = "Assets/Scenes/Testing.unity";
    private const string CombatHudUxmlPath = "Assets/UI Toolkit/Combat/CombatHUD.uxml";

    [MenuItem("Tools/EOTV/Setup Combat In Testing Scene")]
    public static void SetupTestingScene()
    {
      if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      {
        return;
      }

      var scene = EditorSceneManager.OpenScene(TestingScenePath, OpenSceneMode.Single);
      SetupActiveSceneInternal(scene);
      EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/EOTV/Setup Combat In Current Scene")]
    public static void SetupCurrentScene()
    {
      var scene = SceneManager.GetActiveScene();
      if (!scene.IsValid())
      {
        Debug.LogError("No active scene to set up.");
        return;
      }
      SetupActiveSceneInternal(scene);
      EditorSceneManager.SaveScene(scene);
    }

    private static void SetupActiveSceneInternal(Scene scene)
    {
      // Ensure PanelSettings exists
      PanelSettings panel = GetOrCreatePanelSettings();

      // Create or get core systems
      var turnManager = GetOrAddComponent<TurnManager>(GetOrCreate("Turn Manager"));
      var targetingSystem = GetOrAddComponent<TargetingSystem>(GetOrCreate("Targeting System"));
      var actionExecutor = GetOrAddComponent<ActionExecutor>(GetOrCreate("Action Executor"));

      // UI: Single HUD
      var combatUiGO = GetOrCreate("Combat HUD");
      var uiDoc = GetOrAddComponent<UIDocument>(combatUiGO);
      uiDoc.panelSettings = panel;
      // Single panel on top
      uiDoc.sortingOrder = 100;
      uiDoc.visualTreeAsset = null; // Controller will instantiate its own assets
      var uiController = GetOrAddComponent<CombatUIController>(combatUiGO);
      var highlightView = GetOrAddComponent<TargetHighlightView>(combatUiGO);
      AssignCombatUIControllerAssets(uiController, uiDoc, turnManager, targetingSystem);
      AssignTargetHighlightAssets(highlightView, uiDoc, targetingSystem, actionExecutor);

      // Remove legacy separate UI objects if present
      var legacyUi = GameObject.Find("Combat UI");
      if (legacyUi != null && legacyUi != combatUiGO)
      {
        Undo.DestroyObjectImmediate(legacyUi);
      }
      var legacyGridUi = GameObject.Find("Target Grid UI");
      if (legacyGridUi != null)
      {
        Undo.DestroyObjectImmediate(legacyGridUi);
      }

      // Managers
      var itemsGO = GetOrCreate("Item Manager");
      var itemManager = GetOrAddComponent<EchoesOfTheVoid.Items.ItemManager>(itemsGO);
      var skillsGO = GetOrCreate("Skill Manager");
      var skillManager = GetOrAddComponent<EchoesOfTheVoid.Skills.SkillManager>(skillsGO);

      // Game Manager orchestrates wiring and starting combat
      var systemsGO = GetOrCreate("Combat Systems");
      var gameManager = GetOrAddComponent<EchoesOfTheVoid.Core.CombatGameManager>(systemsGO);
      AssignCombatGameManagerRefs(gameManager, turnManager, targetingSystem, actionExecutor, uiController, highlightView, itemManager, skillManager);

      // Optionally add a few sample combatants if none exist
      EnsureSampleCombatantsExist();

      // Mark scene dirty for saving
      EditorSceneManager.MarkSceneDirty(scene);
      Debug.Log("Combat scene setup completed.");
    }

    private static GameObject GetOrCreate(string name)
    {
      var go = GameObject.Find(name);
      if (go == null)
      {
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
      }
      return go;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
      var c = go.GetComponent<T>();
      if (c == null)
      {
        c = Undo.AddComponent<T>(go);
      }
      return c;
    }

    private static void AssignCombatUIControllerAssets(CombatUIController controller, UIDocument doc, TurnManager tm, TargetingSystem ts)
    {
      var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CombatHudUxmlPath);

      var so = new SerializedObject(controller);
      so.FindProperty("_uiDocument").objectReferenceValue = doc;
      so.FindProperty("_combatHudUxml").objectReferenceValue = hud;
      so.FindProperty("_turnManager").objectReferenceValue = tm;
      so.FindProperty("_targetingSystem").objectReferenceValue = ts;

      // Optional managers may be filled later by game manager assignment
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(controller);
    }

    private static void AssignTargetHighlightAssets(TargetHighlightView view, UIDocument doc, TargetingSystem ts, ActionExecutor executor)
    {
      var so = new SerializedObject(view);
      so.FindProperty("_uiDocument").objectReferenceValue = doc;
      so.FindProperty("_targetingSystem").objectReferenceValue = ts;
      var actionProp = so.FindProperty("_actionExecutor");
      if (actionProp != null)
      {
        actionProp.objectReferenceValue = executor;
      }
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(view);
    }

    private static void AssignCombatGameManagerRefs(
      EchoesOfTheVoid.Core.CombatGameManager gm,
      TurnManager tm,
      TargetingSystem ts,
      ActionExecutor executor,
      CombatUIController ui,
      TargetHighlightView highlight,
      EchoesOfTheVoid.Items.ItemManager items,
      EchoesOfTheVoid.Skills.SkillManager skills)
    {
      var so = new SerializedObject(gm);
      so.FindProperty("_turnManager").objectReferenceValue = tm;
      so.FindProperty("_targetingSystem").objectReferenceValue = ts;
      so.FindProperty("_actionExecutor").objectReferenceValue = executor;
      so.FindProperty("_uiController").objectReferenceValue = ui;
      so.FindProperty("_highlightView").objectReferenceValue = highlight;
      so.FindProperty("_itemManager").objectReferenceValue = items;
      so.FindProperty("_skillManager").objectReferenceValue = skills;
      so.FindProperty("_autoDiscoverSceneCombatants").boolValue = true;
      so.FindProperty("_autoStartCombat").boolValue = true;
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(gm);

      // Also inject references to other systems where applicable
      var exSO = new SerializedObject(executor);
      exSO.FindProperty("_turnManager").objectReferenceValue = tm;
      exSO.FindProperty("_targetingSystem").objectReferenceValue = ts;
      exSO.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(executor);

      var uiSO = new SerializedObject(ui);
      uiSO.FindProperty("_turnManager").objectReferenceValue = tm;
      uiSO.FindProperty("_targetingSystem").objectReferenceValue = ts;
      uiSO.FindProperty("_itemManager").objectReferenceValue = items;
      uiSO.FindProperty("_skillManager").objectReferenceValue = skills;
      uiSO.FindProperty("_actionExecutor").objectReferenceValue = executor;
      uiSO.FindProperty("_highlightView").objectReferenceValue = highlight;
      uiSO.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(ui);
    }

    private static PanelSettings GetOrCreatePanelSettings()
    {
      string[] guids = AssetDatabase.FindAssets("t:PanelSettings");
      PanelSettings panel = null;
      if (guids != null && guids.Length > 0)
      {
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
      }
      if (panel == null)
      {
        panel = ScriptableObject.CreateInstance<PanelSettings>();
        string dir = "Assets/UI Toolkit";
        if (!AssetDatabase.IsValidFolder(dir))
        {
          AssetDatabase.CreateFolder("Assets", "UI Toolkit");
        }
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(dir + "/DefaultPanelSettings.asset");
        AssetDatabase.CreateAsset(panel, assetPath);
        AssetDatabase.SaveAssets();
      }
      // Configure for mobile-friendly scaling.
      try
      {
        var so = new SerializedObject(panel);
        // scaleMode enum: 0 = ConstantPixelSize, 1 = ScaleWithScreenSize
        var scaleProp = so.FindProperty("m_ScaleMode");
        if (scaleProp != null) scaleProp.intValue = 1;
        // Reference DPI default 96
        var dpiProp = so.FindProperty("m_ReferenceDpi");
        if (dpiProp != null) dpiProp.floatValue = 96f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);
      }
      catch {}
      return panel;
    }

    private static void EnsureSampleCombatantsExist()
    {
      // Create minimal logical-only combatants if none found in the scene.
      bool anyCombatant = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        .AnyMB(mb => mb is EchoesOfTheVoid.Combat.PlayerCharacter || mb is EchoesOfTheVoid.Combat.EnemyCharacter);
      if (anyCombatant)
      {
        return;
      }

      var playersRoot = GetOrCreate("Players");
      for (int i = 0; i < 3; i++)
      {
        var go = GetOrCreateChild(playersRoot, $"Player {i + 1}");
        GetOrAddComponent<EchoesOfTheVoid.Combat.PlayerCharacter>(go);
      }

      var enemiesRoot = GetOrCreate("Enemies");
      for (int i = 0; i < 3; i++)
      {
        var go = GetOrCreateChild(enemiesRoot, $"Enemy {i + 1}");
        GetOrAddComponent<EchoesOfTheVoid.Combat.EnemyCharacter>(go);
      }
    }

    private static bool AnyMB(this MonoBehaviour[] list, Func<MonoBehaviour, bool> pred)
    {
      foreach (var mb in list)
      {
        if (pred(mb)) return true;
      }
      return false;
    }

    private static GameObject GetOrCreateChild(GameObject parent, string name)
    {
      var child = parent.transform.Find(name)?.gameObject;
      if (child == null)
      {
        child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        child.transform.SetParent(parent.transform);
      }
      return child;
    }
  }
}
