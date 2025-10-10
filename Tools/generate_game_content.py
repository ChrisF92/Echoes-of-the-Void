#!/usr/bin/env python3
from __future__ import annotations

import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Sequence

PROJECT_ROOT = Path(__file__).resolve().parents[1]

STATUS_DIR = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Combat" / "Status Effects"
SKILL_DIR = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Combat" / "Skills"
ENEMY_DIR = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Combat" / "Combatants" / "Enemies"
GAMBIT_DIR = ENEMY_DIR / "GambitProfiles"
RUN_DIR = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Combat" / "Run"
CONSUMABLE_DIR = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Inventory" / "Consumables"
KEYITEM_DIR = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Inventory" / "KeyItems"
EQUIPMENT_BASE_DIR = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Inventory" / "Equipment"

SKILL_SO_SCRIPT_GUID = "ca359c66cf5055e48919f584d4c91fc3"
STATUS_SO_SCRIPT_GUID = "2e6f655bf948d1e44ad064c5ff2a2a61"
COMBATANT_SO_SCRIPT_GUID = "6671d6f73ca30a646b59203412e78db0"
GAMBIT_PROFILE_SCRIPT_GUID = "7dfb06a7667131f429e53801a5615c83"
ITEM_SO_SCRIPT_GUID = "632e24aa3fb616e4a94be0ac0ece6b8d"
EQUIPMENT_ITEM_SO_SCRIPT_GUID = "3b4e68cd40d31fc459ec1288d7437ea9"
COMBAT_RUN_SCRIPT_GUID = "f2b9a55f6563a45499148bd8a3e3edff"

STAT_TYPES = {
    "Health": 0,
    "Mana": 1,
    "Attack": 2,
    "Defense": 3,
    "Speed": 4,
    "Luck": 5,
}

EFFECT_TYPES = {
    "Damage": 0,
    "Heal": 1,
    "ApplyStatus": 2,
}

TARGET_TYPES = {
    "Single": 0,
    "Multiple": 1,
    "Self": 2,
    "AllAllies": 3,
    "AllEnemies": 4,
    "All": 5,
}

STATUS_EFFECT_TYPES = {
    "DamageOverTime": 0,
    "HealOverTime": 1,
    "StatModifier": 2,
    "Stun": 3,
    "Silence": 4,
    "Blind": 5,
    "Custom": 6,
}

EFFECT_TRIGGER_TIMING = {
    "TurnStart": 0,
    "TurnEnd": 1,
    "OnHit": 2,
    "OnDamaged": 3,
}

STACK_BEHAVIORS = {
    "Refresh": 0,
    "Stack": 1,
    "Extend": 2,
    "Replace": 3,
    "Ignore": 4,
}

ITEM_TYPES = {
    "Consumable": 0,
    "Equipment": 1,
    "KeyItem": 2,
}

EQUIPMENT_SLOTS = {
    "Head": 0,
    "Chest": 1,
    "Legs": 2,
    "MainHand": 3,
    "OffHand": 4,
    "Accessory": 5,
    "Relic": 6,
}

EQUIPMENT_SLOT_DIRS = {
    "Head": EQUIPMENT_BASE_DIR / "Head",
    "Chest": EQUIPMENT_BASE_DIR / "Chest",
    "Legs": EQUIPMENT_BASE_DIR / "Legs",
    "MainHand": EQUIPMENT_BASE_DIR / "MainHand",
    "OffHand": EQUIPMENT_BASE_DIR / "OffHand",
    "Accessory": EQUIPMENT_BASE_DIR / "Accessory",
    "Relic": EQUIPMENT_BASE_DIR / "Relic",
}

DAMAGE_CURVE_BLOCK = """\
      serializedVersion: 2
      m_Curve:
      - serializedVersion: 3
        time: 0
        value: 1
        inSlope: 0
        outSlope: 0
        tangentMode: 0
        weightedMode: 0
        inWeight: 0
        outWeight: 0
      - serializedVersion: 3
        time: 1
        value: 1
        inSlope: 0
        outSlope: 0
        tangentMode: 0
        weightedMode: 0
        inWeight: 0
        outWeight: 0
      m_PreInfinity: 2
      m_PostInfinity: 2
      m_RotationOrder: 4"""


def deterministic_guid(name: str) -> str:
    return uuid.uuid5(uuid.NAMESPACE_DNS, f"EchoesOfTheVoid:{name}").hex


def ensure_folder_with_meta(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    meta_path = path.with_suffix(path.suffix + ".meta")
    if meta_path.exists():
        return
    guid = deterministic_guid(f"folder:{path.as_posix()}")
    content = "\n".join([
        "fileFormatVersion: 2",
        f"guid: {guid}",
        "folderAsset: yes",
        "DefaultImporter:",
        "  externalObjects: {}",
        "  userData: ",
        "  assetBundleName: ",
        "  assetBundleVariant: ",
        "",
    ])
    meta_path.write_text(content, encoding="utf-8")


def write_meta_file(path: Path, guid: str) -> None:
    content = "\n".join([
        "fileFormatVersion: 2",
        f"guid: {guid}",
        "NativeFormatImporter:",
        "  externalObjects: {}",
        "  mainObjectFileID: 11400000",
        "  userData: ",
        "  assetBundleName: ",
        "  assetBundleVariant: ",
        "",
    ])
    path.write_text(content, encoding="utf-8")


def format_bool(value: bool) -> str:
    return "1" if value else "0"


def format_float(value: float) -> str:
    text = f"{value:.4f}"
    text = text.rstrip("0").rstrip(".")
    return text if text else "0"


@dataclass
class StatusDefinition:
    file_name: str
    display_name: str
    description: str
    effect_type: str
    base_value: int
    target_stat: str
    duration: int
    trigger_timing: str
    stack_behavior: str
    max_stacks: int
    is_debuff: bool


@dataclass
class SkillEffectDefinition:
    effect_type: str
    base_value: int = 0
    stat_scaling: float = 0.0
    scaling_stat: str = "Attack"
    target_self: bool = False
    status_ref: str | None = None


@dataclass
class SkillDefinition:
    file_name: str
    display_name: str
    description: str
    mana_cost: int
    stamina_cost: int
    cooldown: int
    target_type: str
    can_target_self: bool
    can_target_allies: bool
    can_target_enemies: bool
    effects: Sequence[SkillEffectDefinition]
    animation_trigger: str = ""


@dataclass
class ItemEffectDefinition:
    effect_type: str
    value: int = 0
    status_ref: str | None = None
    target_self: bool = True


@dataclass
class ItemDefinition:
    file_name: str
    display_name: str
    description: str
    item_type: str
    consumable_in_combat: bool
    usable_outside_combat: bool
    max_stack: int
    effects: Sequence[ItemEffectDefinition]


@dataclass
class EquipmentStatModifier:
    stat: str
    flat_bonus: int
    percent_bonus: float = 0.0


@dataclass
class EquipmentDefinition(ItemDefinition):
    slot: str = "Head"
    occupies_both_hands: bool = False
    stat_modifiers: Sequence[EquipmentStatModifier] = ()


@dataclass
class GambitRuleTarget:
    type: str
    threshold: float | None = None
    include_self: bool = True


@dataclass
class GambitRuleAction:
    type: str
    skill: str | None = None
    require: bool = True


@dataclass
class GambitRuleDefinition:
    name: str
    target: GambitRuleTarget
    action: GambitRuleAction


@dataclass
class EnemyDefinition:
    file_name: str
    display_name: str
    combatant_id: str
    stats: Dict[str, int]
    skills: Sequence[str]
    gambit_rules: Sequence[GambitRuleDefinition]


@dataclass
class RewardBundle:
    experience: int
    currency: int
    items: Sequence[tuple[str, int]]


@dataclass
class RunFloorConfig:
    floor_id: str
    display_name: str
    number: int
    enemies: Sequence[str]
    rewards: RewardBundle
    heal_on_start: bool
    restore_ratio: float


def write_status_asset(status: StatusDefinition, guid: str) -> None:
    ensure_folder_with_meta(STATUS_DIR)
    asset_path = STATUS_DIR / f"{status.file_name}.asset"
    meta_path = STATUS_DIR / f"{status.file_name}.asset.meta"
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {STATUS_SO_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {status.file_name}",
        "  m_EditorClassIdentifier: EchoesOfTheVoid.Core::EchoesOfTheVoid.Core.Combat.ScriptableObjects.StatusEffectSO",
        f"  _effectId: {status.file_name}",
        f"  _displayName: {status.display_name}",
        f"  _description: {status.description}",
        "  _icon: {fileID: 0}",
        f"  _effectType: {STATUS_EFFECT_TYPES[status.effect_type]}",
        f"  _baseValue: {status.base_value}",
        f"  _targetStat: {STAT_TYPES[status.target_stat]}",
        f"  _duration: {status.duration}",
        f"  _triggerTiming: {EFFECT_TRIGGER_TIMING[status.trigger_timing]}",
        f"  _stackBehavior: {STACK_BEHAVIORS[status.stack_behavior]}",
        f"  _maxStacks: {status.max_stacks}",
        f"  _isDebuff: {format_bool(status.is_debuff)}",
        "  _visualEffect: {fileID: 0}",
        "  _applySound: {fileID: 0}",
        "  _tickSound: {fileID: 0}",
        "",
    ]
    asset_path.write_text("\n".join(lines), encoding="utf-8")
    write_meta_file(meta_path, guid)


def write_skill_asset(skill: SkillDefinition, guid: str, status_guids: Dict[str, str]) -> None:
    ensure_folder_with_meta(SKILL_DIR)
    asset_path = SKILL_DIR / f"{skill.file_name}.asset"
    meta_path = SKILL_DIR / f"{skill.file_name}.asset.meta"
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {SKILL_SO_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {skill.file_name}",
        "  m_EditorClassIdentifier: EchoesOfTheVoid.Core::EchoesOfTheVoid.Core.Combat.ScriptableObjects.SkillSO",
        f"  SkillId: {skill.file_name}",
        f"  DisplayName: {skill.display_name}",
        f"  Description: {skill.description}",
        "  Icon: {fileID: 0}",
        f"  ManaCost: {skill.mana_cost}",
        f"  StaminaCost: {skill.stamina_cost}",
        f"  CooldownTurns: {skill.cooldown}",
        f"  TargetType: {TARGET_TYPES[skill.target_type]}",
        f"  CanTargetSelf: {format_bool(skill.can_target_self)}",
        f"  CanTargetAllies: {format_bool(skill.can_target_allies)}",
        f"  CanTargetEnemies: {format_bool(skill.can_target_enemies)}",
        "  Effects:",
    ]

    for effect in skill.effects:
        status_block = "{fileID: 0}"
        if effect.status_ref:
            status_guid = status_guids.get(effect.status_ref)
            if not status_guid:
                raise ValueError(f"Unknown status reference: {effect.status_ref}")
            status_block = f"{{fileID: 11400000, guid: {status_guid}, type: 2}}"

        lines.extend([
            f"  - EffectType: {EFFECT_TYPES[effect.effect_type]}",
            f"    StatusEffect: {status_block}",
            f"    BaseValue: {effect.base_value}",
            f"    StatScaling: {format_float(effect.stat_scaling)}",
            f"    ScalingStat: {STAT_TYPES[effect.scaling_stat]}",
            f"    TargetSelf: {format_bool(effect.target_self)}",
            "    DamageCurve:",
            DAMAGE_CURVE_BLOCK,
        ])

    lines.extend([
        f"  AnimationTrigger: {skill.animation_trigger}",
        "  SoundEffect: {fileID: 0}",
        "  VisualEffect: {fileID: 0}",
        "",
    ])
    asset_path.write_text("\n".join(lines), encoding="utf-8")
    write_meta_file(meta_path, guid)


def write_item_asset_to_dir(item: ItemDefinition, guid: str, status_guids: Dict[str, str], directory: Path) -> None:
    ensure_folder_with_meta(directory)
    asset_path = directory / f"{item.file_name}.asset"
    meta_path = directory / f"{item.file_name}.asset.meta"
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {ITEM_SO_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {item.file_name}",
        "  m_EditorClassIdentifier: EchoesOfTheVoid.Core::EchoesOfTheVoid.Core.Inventory.ScriptableObjects.ItemScriptableObject",
        f"  ItemId: {item.file_name}",
        f"  DisplayName: {item.display_name}",
        f"  Description: {item.description}",
        "  Icon: {fileID: 0}",
        f"  ItemType: {ITEM_TYPES[item.item_type]}",
        f"  ConsumableInCombat: {format_bool(item.consumable_in_combat)}",
        f"  UsableOutsideCombat: {format_bool(item.usable_outside_combat)}",
        f"  MaxStackSize: {item.max_stack}",
    ]

    if item.effects:
        lines.append("  Effects:")
        for effect in item.effects:
            status_block = "{fileID: 0}"
            if effect.status_ref:
                status_guid = status_guids.get(effect.status_ref)
                if not status_guid:
                    raise ValueError(f"Unknown status reference: {effect.status_ref}")
                status_block = f"{{fileID: 11400000, guid: {status_guid}, type: 2}}"
            lines.extend([
                f"  - EffectType: {EFFECT_TYPES[effect.effect_type]}",
                f"    Value: {effect.value}",
                f"    StatusEffect: {status_block}",
                f"    TargetSelf: {format_bool(effect.target_self)}",
            ])
    else:
        lines.append("  Effects: []")

    lines.extend([
        "  UseSound: {fileID: 0}",
        "  UseEffect: {fileID: 0}",
        "",
    ])
    asset_path.write_text("\n".join(lines), encoding="utf-8")
    write_meta_file(meta_path, guid)


def write_equipment_asset(equipment: EquipmentDefinition, guid: str, status_guids: Dict[str, str]) -> None:
    directory = EQUIPMENT_SLOT_DIRS[equipment.slot]
    ensure_folder_with_meta(directory)
    asset_path = directory / f"{equipment.file_name}.asset"
    meta_path = directory / f"{equipment.file_name}.asset.meta"
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {EQUIPMENT_ITEM_SO_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {equipment.file_name}",
        "  m_EditorClassIdentifier: EchoesOfTheVoid.Core::EchoesOfTheVoid.Core.Inventory.ScriptableObjects.EquipmentItemScriptableObject",
        f"  ItemId: {equipment.file_name}",
        f"  DisplayName: {equipment.display_name}",
        f"  Description: {equipment.description}",
        "  Icon: {fileID: 0}",
        f"  ItemType: {ITEM_TYPES[equipment.item_type]}",
        f"  ConsumableInCombat: {format_bool(equipment.consumable_in_combat)}",
        f"  UsableOutsideCombat: {format_bool(equipment.usable_outside_combat)}",
        f"  MaxStackSize: {equipment.max_stack}",
    ]

    if equipment.effects:
        lines.append("  Effects:")
        for effect in equipment.effects:
            status_block = "{fileID: 0}"
            if effect.status_ref:
                status_guid = status_guids.get(effect.status_ref)
                if not status_guid:
                    raise ValueError(f"Unknown status reference: {effect.status_ref}")
                status_block = f"{{fileID: 11400000, guid: {status_guid}, type: 2}}"
            lines.extend([
                f"  - EffectType: {EFFECT_TYPES[effect.effect_type]}",
                f"    Value: {effect.value}",
                f"    StatusEffect: {status_block}",
                f"    TargetSelf: {format_bool(effect.target_self)}",
            ])
    else:
        lines.append("  Effects: []")

    lines.extend([
        "  UseSound: {fileID: 0}",
        "  UseEffect: {fileID: 0}",
        f"  Slot: {EQUIPMENT_SLOTS[equipment.slot]}",
        f"  OccupiesBothHands: {format_bool(equipment.occupies_both_hands)}",
    ])

    if equipment.stat_modifiers:
        lines.append("  StatModifiers:")
        for modifier in equipment.stat_modifiers:
            lines.extend([
                f"  - Stat: {STAT_TYPES[modifier.stat]}",
                f"    FlatBonus: {modifier.flat_bonus}",
                f"    PercentBonus: {format_float(modifier.percent_bonus)}",
            ])
    else:
        lines.append("  StatModifiers: []")

    lines.append("")
    asset_path.write_text("\n".join(lines), encoding="utf-8")
    write_meta_file(meta_path, guid)


def write_gambit_asset(enemy: EnemyDefinition, guid: str, skill_guids: Dict[str, str]) -> None:
    ensure_folder_with_meta(GAMBIT_DIR)
    asset_path = GAMBIT_DIR / f"gambit_profile_{enemy.file_name}.asset"
    meta_path = GAMBIT_DIR / f"gambit_profile_{enemy.file_name}.asset.meta"

    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {GAMBIT_PROFILE_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: gambit_profile_{enemy.file_name}",
        "  m_EditorClassIdentifier: EchoesOfTheVoid.Core::EchoesOfTheVoid.Core.Combat.Gambits.GambitProfile",
        "  rules:",
    ]

    ref_entries: List[Dict[str, Iterable[str]]] = []
    next_rid = 1
    ns = "EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations"

    for rule in enemy.gambit_rules:
        target_rid = next_rid
        action_rid = next_rid + 1
        next_rid += 2

        lines.extend([
            f"  - RuleName: {rule.name}",
            "    IsEnabled: 1",
            "    TargetCondition:",
            f"      rid: {target_rid}",
            "    Action:",
            f"      rid: {action_rid}",
        ])

        if rule.target.type == "Self":
            target_class = "SelfTargetBlock"
            target_data: List[str] = []
        elif rule.target.type == "RandomEnemy":
            target_class = "RandomEnemyTargetBlock"
            target_data = []
        elif rule.target.type == "AllyBelow":
            target_class = "AllyHealthBelowPercentBlock"
            threshold = rule.target.threshold if rule.target.threshold is not None else 0.5
            target_data = [
                f"      Threshold: {format_float(threshold)}",
                f"      IncludeSelf: {format_bool(rule.target.include_self)}",
            ]
        else:
            raise ValueError(f"Unsupported gambit target type: {rule.target.type}")

        ref_entries.append({
            "rid": target_rid,
            "class": target_class,
            "data": target_data,
        })

        if rule.action.type == "Skill":
            action_class = "SkillActionBlock"
            if not rule.action.skill:
                raise ValueError(f"Skill action missing skill reference for {enemy.file_name}")
            skill_guid = skill_guids.get(rule.action.skill)
            if not skill_guid:
                raise ValueError(f"Unknown skill reference '{rule.action.skill}' in gambit for {enemy.file_name}")
            action_data = [
                f"      skill: {{fileID: 11400000, guid: {skill_guid}, type: 2}}",
                f"      requireCanUse: {format_bool(rule.action.require)}",
            ]
        elif rule.action.type == "Attack":
            action_class = "AttackActionBlock"
            action_data = []
        elif rule.action.type == "Defend":
            action_class = "DefendActionBlock"
            action_data = []
        else:
            raise ValueError(f"Unsupported gambit action type: {rule.action.type}")

        ref_entries.append({
            "rid": action_rid,
            "class": action_class,
            "data": action_data,
        })

    lines.extend([
        "  references:",
        "    version: 2",
        "    RefIds:",
    ])

    for entry in ref_entries:
        lines.append(f"    - rid: {entry['rid']}")
        lines.append(f"      type: {{class: {entry['class']}, ns: {ns}, asm: EchoesOfTheVoid.Core}}")
        data_lines = entry["data"]
        if data_lines:
            lines.append("      data:")
            lines.extend(data_lines)
        else:
            lines.append("      data: ")

    lines.append("")
    asset_path.write_text("\n".join(lines), encoding="utf-8")
    write_meta_file(meta_path, guid)


def write_enemy_asset(enemy: EnemyDefinition, guid: str, skill_guids: Dict[str, str], gambit_guid: str) -> None:
    ensure_folder_with_meta(ENEMY_DIR)
    asset_path = ENEMY_DIR / f"{enemy.file_name}.asset"
    meta_path = ENEMY_DIR / f"{enemy.file_name}.asset.meta"

    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {COMBATANT_SO_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {enemy.file_name}",
        "  m_EditorClassIdentifier: ",
        "  IsPlayerControlled: 0",
        f"  CombatantId: {enemy.combatant_id}",
        f"  DisplayName: {enemy.display_name}",
        "  Portrait: {fileID: 0}",
        "  CombatPrefab: {fileID: 0}",
        "  BaseStats:",
        f"    Health: {enemy.stats['Health']}",
        f"    Mana: {enemy.stats['Mana']}",
        f"    Attack: {enemy.stats['Attack']}",
        f"    Defense: {enemy.stats['Defense']}",
        f"    Speed: {enemy.stats['Speed']}",
        f"    Luck: {enemy.stats['Luck']}",
    ]

    if enemy.skills:
        lines.append("  StartingSkills:")
        for skill in enemy.skills:
            skill_guid = skill_guids.get(skill)
            if not skill_guid:
                raise ValueError(f"Unknown skill '{skill}' for enemy {enemy.file_name}")
            lines.append(f"  - {{fileID: 11400000, guid: {skill_guid}, type: 2}}")
    else:
        lines.append("  StartingSkills: []")

    lines.extend([
        "  StartingItems: []",
        "  StartingEquipment: []",
        f"  GambitProfile: {{fileID: 11400000, guid: {gambit_guid}, type: 2}}",
        "",
    ])

    asset_path.write_text("\n".join(lines), encoding="utf-8")
    write_meta_file(meta_path, guid)


def write_run_asset(run_name: str, guid: str, floors: Sequence[RunFloorConfig], completion: RewardBundle, enemy_guids: Dict[str, str], item_guids: Dict[str, str]) -> None:
    ensure_folder_with_meta(RUN_DIR)
    asset_path = RUN_DIR / f"{run_name}.asset"
    meta_path = RUN_DIR / f"{run_name}.asset.meta"

    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {COMBAT_RUN_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {run_name}",
        "  m_EditorClassIdentifier: ",
        f"  _runId: {run_name}",
        "  _displayName: Eternal Gauntlet",
        "  _description: A relentless ascent through one hundred increasingly perilous floors.",
        "  _icon: {fileID: 0}",
        "  _floors:",
    ]

    for floor in floors:
        lines.extend([
            f"  - _floorId: {floor.floor_id}",
            f"    _displayName: {floor.display_name}",
            f"    _floorNumber: {floor.number}",
        ])
        if floor.enemies:
            lines.append("    _enemyTemplates:")
            for enemy in floor.enemies:
                enemy_guid = enemy_guids.get(enemy)
                if not enemy_guid:
                    raise ValueError(f"Unknown enemy '{enemy}' referenced in combat run")
                lines.append(f"    - {{fileID: 11400000, guid: {enemy_guid}, type: 2}}")
        else:
            lines.append("    _enemyTemplates: []")

        lines.append("    _rewards:")
        lines.append(f"      _experience: {floor.rewards.experience}")
        lines.append(f"      _currency: {floor.rewards.currency}")
        if floor.rewards.items:
            lines.append("      _items:")
            for item_id, quantity in floor.rewards.items:
                item_guid = item_guids.get(item_id)
                if not item_guid:
                    raise ValueError(f"Unknown item '{item_id}' referenced in combat run rewards")
                lines.append(f"      - Item: {{fileID: 11400000, guid: {item_guid}, type: 2}}")
                lines.append(f"        Quantity: {quantity}")
        else:
            lines.append("      _items: []")

        lines.append(f"    _healPartyOnStart: {format_bool(floor.heal_on_start)}")
        lines.append(f"    _playerHealthRestoreRatio: {format_float(floor.restore_ratio)}")

    lines.append("  _completionRewards:")
    lines.append(f"    _experience: {completion.experience}")
    lines.append(f"    _currency: {completion.currency}")
    if completion.items:
        lines.append("    _items:")
        for item_id, quantity in completion.items:
            item_guid = item_guids.get(item_id)
            if not item_guid:
                raise ValueError(f"Unknown completion reward item '{item_id}'")
            lines.append(f"    - Item: {{fileID: 11400000, guid: {item_guid}, type: 2}}")
            lines.append(f"      Quantity: {quantity}")
    else:
        lines.append("    _items: []")

    lines.append("")
    asset_path.write_text("\n".join(lines), encoding="utf-8")
    write_meta_file(meta_path, guid)


statuses: List[StatusDefinition] = [
    StatusDefinition("status_effect_scorch", "Scorch", "Burns the target with searing flames each turn.", "DamageOverTime", 12, "Health", 3, "TurnEnd", "Stack", 3, True),
    StatusDefinition("status_effect_ashen_plague", "Ashen Plague", "A virulent blaze that eats through armor over time.", "DamageOverTime", 18, "Health", 4, "TurnEnd", "Stack", 2, True),
    StatusDefinition("status_effect_shadow_decay", "Shadow Decay", "Void corruption drains vitality relentlessly.", "DamageOverTime", 14, "Health", 3, "TurnEnd", "Extend", 1, True),
    StatusDefinition("status_effect_moonlit_resurgence", "Moonlit Resurgence", "A soothing lunar glow steadily mends wounds.", "HealOverTime", 10, "Health", 3, "TurnEnd", "Refresh", 1, False),
    StatusDefinition("status_effect_vital_surge", "Vital Surge", "Life energy pulses to restore health rapidly.", "HealOverTime", 6, "Health", 2, "TurnStart", "Stack", 2, False),
    StatusDefinition("status_effect_battle_trance", "Battle Trance", "Heightens aggression and martial focus.", "StatModifier", 8, "Attack", 3, "TurnStart", "Refresh", 1, False),
    StatusDefinition("status_effect_crystalline_ward", "Crystalline Ward", "Faceted light hardens into protective armor.", "StatModifier", 12, "Defense", 4, "TurnStart", "Refresh", 1, False),
    StatusDefinition("status_effect_gale_haste", "Gale Haste", "Tempest winds quicken movements and reactions.", "StatModifier", 6, "Speed", 3, "TurnStart", "Refresh", 1, False),
    StatusDefinition("status_effect_focus_mind", "Focus Mind", "Steady breaths align intuition and clarity.", "StatModifier", 5, "Luck", 4, "TurnStart", "Refresh", 1, False),
    StatusDefinition("status_effect_null_field", "Null Field", "Dampens mystical output, preventing spellcasting.", "Silence", 0, "Mana", 2, "TurnStart", "Refresh", 1, True),
    StatusDefinition("status_effect_starblind", "Starblind", "Brilliant motes obscure sight and accuracy.", "Blind", 0, "Speed", 2, "TurnEnd", "Refresh", 1, True),
    StatusDefinition("status_effect_static_cage", "Static Cage", "Paralyzing arcs of lightning halt all movement.", "Stun", 0, "Health", 1, "TurnStart", "Refresh", 1, True),
    StatusDefinition("status_effect_glacial_burden", "Glacial Burden", "Frigid bindings sap speed and resolve.", "StatModifier", -8, "Speed", 2, "TurnEnd", "Refresh", 1, True),
    StatusDefinition("status_effect_steadfast_barrier", "Steadfast Barrier", "An anchored ward bolsters steadfast defense.", "StatModifier", 15, "Defense", 2, "TurnStart", "Refresh", 1, False),
    StatusDefinition("status_effect_zephyr_step", "Zephyr Step", "Harnessed wind propels swift repositioning.", "StatModifier", 8, "Speed", 2, "TurnStart", "Refresh", 1, False),
    StatusDefinition("status_effect_iron_resolve", "Iron Resolve", "Unbreakable conviction sharpens every strike.", "StatModifier", 10, "Attack", 2, "TurnStart", "Refresh", 1, False),
]

consumables: List[ItemDefinition] = [
    ItemDefinition("item_consumable_starlight_tonic", "Starlight Tonic", "A radiant draught that restores a large amount of health.", "Consumable", True, False, 5, [ItemEffectDefinition("Heal", value=45)]),
    ItemDefinition("item_consumable_moonpetal_draught", "Moonpetal Draught", "Floral essence that revitalizes body and spirit.", "Consumable", True, True, 4, [
        ItemEffectDefinition("Heal", value=30),
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_vital_surge", target_self=True),
    ]),
    ItemDefinition("item_consumable_emberbomb", "Emberbomb", "Explodes on impact to scorch foes with embers.", "Consumable", True, False, 3, [
        ItemEffectDefinition("Damage", value=40, target_self=False),
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_scorch", target_self=False),
    ]),
    ItemDefinition("item_consumable_celestial_ration", "Celestial Ration", "Dense star-grain that swiftly restores vigor.", "Consumable", True, True, 6, [
        ItemEffectDefinition("Heal", value=25),
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_moonlit_resurgence"),
    ]),
    ItemDefinition("item_consumable_ironroot_elixir", "Ironroot Elixir", "Fortifies the drinker with resilient barkskin.", "Consumable", True, False, 3, [
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_crystalline_ward"),
    ]),
    ItemDefinition("item_consumable_windsprint_serum", "Windsprint Serum", "Infuses limbs with celerity drawn from storm fronts.", "Consumable", True, False, 3, [
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_gale_haste"),
    ]),
    ItemDefinition("item_consumable_voidsalt_phial", "Voidsalt Phial", "A caustic vial that disrupts hostile incantations.", "Consumable", True, False, 4, [
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_null_field", target_self=False),
    ]),
    ItemDefinition("item_consumable_arcane_lantern", "Arcane Lantern", "Illuminates minds with a steady guiding flame.", "Consumable", True, True, 5, [
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_focus_mind"),
    ]),
    ItemDefinition("item_consumable_safeguard_balm", "Safeguard Balm", "Thick salve that mends wounds and hardens resolve.", "Consumable", True, True, 5, [
        ItemEffectDefinition("Heal", value=20),
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_steadfast_barrier"),
    ]),
    ItemDefinition("item_consumable_starshard_ampoule", "Starshard Ampoule", "Concentrated stellar ichor for emergency recovery.", "Consumable", True, False, 2, [
        ItemEffectDefinition("Heal", value=55),
        ItemEffectDefinition("ApplyStatus", status_ref="status_effect_battle_trance"),
    ]),
]

key_items: List[ItemDefinition] = [
    ItemDefinition("item_key_astral_compass", "Astral Compass", "Points toward tears in the firmament.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_void_relic", "Void Relic", "A relic humming with distant whispers.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_luminary_fragment", "Luminary Fragment", "Shard of an ancient beacon, still faintly glowing.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_celestial_chart", "Celestial Chart", "Maps shifting star currents within the Void.", "KeyItem", False, True, 1, []),
    ItemDefinition("item_key_echo_core", "Echo Core", "Stores resonant memories from echoing battles.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_singularity_map", "Singularity Map", "Annotated routes through gravitational anomalies.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_ancient_cipher", "Ancient Cipher", "Encoded glyphs required to access sealed archives.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_planar_anchor", "Planar Anchor", "Stabilizes transit between fractured realms.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_mnemonic_orb", "Mnemonic Orb", "Preserves key memories needed to open forbidden doors.", "KeyItem", False, False, 1, []),
    ItemDefinition("item_key_skyseal_brooch", "Skyseal Brooch", "Grants safe passage through the upper firmament.", "KeyItem", False, False, 1, []),
]

equipment_items: List[EquipmentDefinition] = [
    EquipmentDefinition("item_head_nebula_hood", "Nebula Hood", "Veil woven from stardust that sharpens arcane instincts.", "Equipment", False, False, 1, [], slot="Head", stat_modifiers=[
        EquipmentStatModifier("Mana", 20),
        EquipmentStatModifier("Luck", 4),
    ]),
    EquipmentDefinition("item_chest_dawnguard_plate", "Dawnguard Plate", "Radiant plates that ward off the void's chill.", "Equipment", False, False, 1, [], slot="Chest", stat_modifiers=[
        EquipmentStatModifier("Defense", 22),
        EquipmentStatModifier("Attack", 4),
    ]),
    EquipmentDefinition("item_legs_astral_stride", "Astral Stride Greaves", "Greaves that lighten every step with astral winds.", "Equipment", False, False, 1, [], slot="Legs", stat_modifiers=[
        EquipmentStatModifier("Speed", 6),
        EquipmentStatModifier("Defense", 8),
    ]),
    EquipmentDefinition("item_mainhand_starforged_blade", "Starforged Blade", "A blade tempered in falling stars for decisive strikes.", "Equipment", False, False, 1, [], slot="MainHand", stat_modifiers=[
        EquipmentStatModifier("Attack", 24, 0.18),
    ]),
    EquipmentDefinition("item_mainhand_thunder_maul", "Thunder Maul", "Massive warhammer crackling with bottled storms.", "Equipment", False, False, 1, [], slot="MainHand", occupies_both_hands=True, stat_modifiers=[
        EquipmentStatModifier("Attack", 32, 0.12),
        EquipmentStatModifier("Defense", 8),
    ]),
    EquipmentDefinition("item_offhand_mirror_shield", "Mirror Shield", "Reflective barrier that bends hostile energies aside.", "Equipment", False, False, 1, [], slot="OffHand", stat_modifiers=[
        EquipmentStatModifier("Defense", 20, 0.1),
    ]),
    EquipmentDefinition("item_accessory_void_pendant", "Void Pendant", "Pendant that harmonizes wielder with the void current.", "Equipment", False, False, 1, [], slot="Accessory", stat_modifiers=[
        EquipmentStatModifier("Mana", 15),
        EquipmentStatModifier("Attack", 5),
    ]),
    EquipmentDefinition("item_accessory_celestial_loop", "Celestial Loop", "Looped constellation sigil inspiring daring feats.", "Equipment", False, False, 1, [], slot="Accessory", stat_modifiers=[
        EquipmentStatModifier("Luck", 8),
        EquipmentStatModifier("Speed", 2),
    ]),
    EquipmentDefinition("item_relic_singularity_core", "Singularity Core", "Miniature singularity that empowers every action.", "Equipment", False, False, 1, [], slot="Relic", stat_modifiers=[
        EquipmentStatModifier("Attack", 10, 0.1),
        EquipmentStatModifier("Mana", 20),
    ]),
    EquipmentDefinition("item_chest_radiant_mantle", "Radiant Mantle", "Mantle that converts starlight into steadfast protection.", "Equipment", False, False, 1, [], slot="Chest", stat_modifiers=[
        EquipmentStatModifier("Defense", 18),
        EquipmentStatModifier("Speed", 3),
    ]),
]

skills: List[SkillDefinition] = [
    SkillDefinition("skill_void_lance", "Void Lance", "Pierces a single foe with condensed void energy.", 6, 0, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=28, stat_scaling=0.7, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_scorch"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_starfall_barrage", "Starfall Barrage", "Rains searing starlight across every enemy.", 12, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=22, stat_scaling=0.55, scaling_stat="Mana"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_emberstorm", "Emberstorm", "Summons a storm of embers that engulfs foes.", 10, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=18, stat_scaling=0.5, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_ashen_plague"),
    ], animation_trigger="CastFire"),
    SkillDefinition("skill_gravity_well", "Gravity Well", "Crushes enemies within a localized singularity.", 9, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=14, stat_scaling=0.4, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_glacial_burden"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_thunder_chain", "Thunder Chain", "Arcs lightning between clustered adversaries.", 8, 0, 1, "Multiple", False, False, True, [
        SkillEffectDefinition("Damage", base_value=19, stat_scaling=0.65, scaling_stat="Attack"),
    ], animation_trigger="CastLightning"),
    SkillDefinition("skill_tidal_surge", "Tidal Surge", "A crushing wave drenches and disorients foes.", 7, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=16, stat_scaling=0.45, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_starblind"),
    ], animation_trigger="CastWater"),
    SkillDefinition("skill_umbral_spike", "Umbral Spike", "Drives a shadowy spike through a target.", 6, 0, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=32, stat_scaling=0.85, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_shadow_decay"),
    ], animation_trigger="CastDark"),
    SkillDefinition("skill_soulrend_gaze", "Soulrend Gaze", "Gaze of the void that silences hostile magic.", 9, 0, 2, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=24, stat_scaling=0.5, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_null_field"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_luminous_ray", "Luminous Ray", "Sears a foe with light while mending the caster.", 5, 0, 1, "Single", True, False, True, [
        SkillEffectDefinition("Damage", base_value=18, stat_scaling=0.65, scaling_stat="Attack"),
        SkillEffectDefinition("Heal", base_value=10, stat_scaling=0.3, scaling_stat="Attack", target_self=True),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_searing_comet", "Searing Comet", "A blazing comet crashes through enemy lines.", 13, 0, 3, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=26, stat_scaling=0.6, scaling_stat="Attack"),
    ], animation_trigger="CastFire"),
    SkillDefinition("skill_galeforce_blades", "Galeforce Blades", "Wind-lashed strikes cleave clustered enemies.", 0, 4, 2, "Multiple", False, False, True, [
        SkillEffectDefinition("Damage", base_value=20, stat_scaling=0.55, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_zephyr_step", target_self=True),
    ], animation_trigger="SlashDual"),
    SkillDefinition("skill_voidflare_burst", "Voidflare Burst", "Explodes with void fire to stagger opponents.", 11, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=20, stat_scaling=0.5, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_null_field"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_dread_rip", "Dread Rip", "A ruthless cleave that invigorates the attacker.", 0, 3, 2, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=27, stat_scaling=0.75, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_battle_trance", target_self=True),
    ], animation_trigger="SlashHeavy"),
    SkillDefinition("skill_rift_rend", "Rift Rend", "Rips open space to unbalance every foe.", 10, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=21, stat_scaling=0.55, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_glacial_burden"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_entropy_wave", "Entropy Wave", "Wave of ruin that spreads void blight.", 12, 0, 3, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=18, stat_scaling=0.5, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_ashen_plague"),
    ], animation_trigger="CastDark"),
    SkillDefinition("skill_meteor_crash", "Meteor Crash", "Calls down meteors to crush nearby foes.", 0, 5, 3, "Multiple", False, False, True, [
        SkillEffectDefinition("Damage", base_value=24, stat_scaling=0.65, scaling_stat="Attack"),
    ], animation_trigger="CastFire"),
    SkillDefinition("skill_savage_maul", "Savage Maul", "Ferocious blow that overwhelms a single enemy.", 0, 4, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=30, stat_scaling=0.8, scaling_stat="Attack"),
    ], animation_trigger="SlashHeavy"),
    SkillDefinition("skill_zephyr_blade", "Zephyr Blade", "Quick strike that leaves the wielder swift.", 0, 2, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=20, stat_scaling=0.6, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_zephyr_step", target_self=True),
    ], animation_trigger="Slash"),
    SkillDefinition("skill_dusk_vortex", "Dusk Vortex", "Dark winds swallow enemies in twilight.", 8, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=17, stat_scaling=0.45, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_starblind"),
    ], animation_trigger="CastDark"),
    SkillDefinition("skill_astral_javelin", "Astral Javelin", "A spear of starlight impales the chosen foe.", 6, 0, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=25, stat_scaling=0.7, scaling_stat="Attack"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_lunar_revival", "Lunar Revival", "Bathes an ally in rejuvenating lunar radiance.", 8, 0, 2, "Single", True, True, False, [
        SkillEffectDefinition("Heal", base_value=32, stat_scaling=0.6, scaling_stat="Mana"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_aurora_mend", "Aurora Mend", "Sweeping aurora washes over allies to heal wounds.", 12, 0, 3, "AllAllies", True, True, False, [
        SkillEffectDefinition("Heal", base_value=18, stat_scaling=0.5, scaling_stat="Mana"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_harmonic_salve", "Harmonic Salve", "Resonant chant binds injuries with gentle light.", 7, 0, 1, "Single", True, True, False, [
        SkillEffectDefinition("Heal", base_value=22, stat_scaling=0.55, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_moonlit_resurgence", target_self=False),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_starlit_barrier", "Starlit Barrier", "Shields an ally with shimmering constellations.", 6, 0, 1, "Single", True, True, False, [
        SkillEffectDefinition("Heal", base_value=10, stat_scaling=0.3, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_crystalline_ward", target_self=False),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_warding_song", "Warding Song", "Enfolds allies in a harmonized defensive hymn.", 9, 0, 2, "AllAllies", True, True, False, [
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_steadfast_barrier"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_gale_hymn", "Gale Hymn", "Song of tempests granting blinding speed.", 8, 0, 2, "AllAllies", True, True, False, [
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_gale_haste"),
    ], animation_trigger="CastWind"),
    SkillDefinition("skill_battle_anthem", "Battle Anthem", "Resonant anthem that emboldens every ally.", 8, 0, 2, "AllAllies", True, True, False, [
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_battle_trance"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_guardian_oath", "Guardian Oath", "Renews the user's vitality and fortitude.", 5, 0, 1, "Self", True, False, False, [
        SkillEffectDefinition("Heal", base_value=18, stat_scaling=0.4, scaling_stat="Defense", target_self=True),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_crystalline_ward", target_self=True),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_crescent_refresh", "Crescent Refresh", "Lunar wash restores allies between breaths.", 6, 0, 2, "AllAllies", True, True, False, [
        SkillEffectDefinition("Heal", base_value=14, stat_scaling=0.35, scaling_stat="Mana"),
    ], animation_trigger="CastWater"),
    SkillDefinition("skill_radiant_convergence", "Radiant Convergence", "Converging light bolsters minds and hearts.", 10, 0, 3, "AllAllies", True, True, False, [
        SkillEffectDefinition("Heal", base_value=16, stat_scaling=0.4, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_focus_mind"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_serene_breath", "Serene Breath", "Centered breathing restores the user's vitality.", 4, 0, 1, "Self", True, False, False, [
        SkillEffectDefinition("Heal", base_value=20, stat_scaling=0.4, scaling_stat="Mana", target_self=True),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_vital_surge", target_self=True),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_resolute_guard", "Resolute Guard", "Bolsters an ally against impending blows.", 7, 0, 1, "Single", True, True, False, [
        SkillEffectDefinition("Heal", base_value=18, stat_scaling=0.4, scaling_stat="Defense"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_steadfast_barrier"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_tempest_guard", "Tempest Guard", "Harnesses storms to empower the caster.", 6, 0, 2, "Self", True, False, False, [
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_gale_haste", target_self=True),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_battle_trance", target_self=True),
    ], animation_trigger="CastWind"),
    SkillDefinition("skill_echoing_pulse", "Echoing Pulse", "Pulse of harmonic energy mends and sharpens senses.", 7, 0, 2, "AllAllies", True, True, False, [
        SkillEffectDefinition("Heal", base_value=12, stat_scaling=0.3, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_focus_mind"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_phoenix_rally", "Phoenix Rally", "Ignites allies with reborn vigor.", 12, 0, 3, "AllAllies", True, True, False, [
        SkillEffectDefinition("Heal", base_value=20, stat_scaling=0.45, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_battle_trance"),
    ], animation_trigger="CastFire"),
    SkillDefinition("skill_null_silence", "Null Silence", "Dampens a foe's magic while dealing damage.", 6, 0, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=16, stat_scaling=0.5, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_null_field"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_stasis_field", "Stasis Field", "Freezes surrounding enemies in crackling stasis.", 11, 0, 3, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=8, stat_scaling=0.3, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_static_cage"),
    ], animation_trigger="CastLightning"),
    SkillDefinition("skill_shadow_binds", "Shadow Binds", "Binding shadows hamper foes and sap vitality.", 7, 0, 2, "Multiple", False, False, True, [
        SkillEffectDefinition("Damage", base_value=15, stat_scaling=0.45, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_shadow_decay"),
    ], animation_trigger="CastDark"),
    SkillDefinition("skill_crystal_seal", "Crystal Seal", "Encases an enemy in frost-laden crystal.", 9, 0, 2, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=14, stat_scaling=0.4, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_glacial_burden"),
    ], animation_trigger="CastIce"),
    SkillDefinition("skill_mind_fog", "Mind Fog", "Confounding mist blinds enemy lines.", 8, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=10, stat_scaling=0.3, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_starblind"),
    ], animation_trigger="CastDark"),
    SkillDefinition("skill_bleak_rot", "Bleak Rot", "Saturates a foe with corrosive decay.", 7, 0, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=18, stat_scaling=0.5, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_ashen_plague"),
    ], animation_trigger="CastDark"),
    SkillDefinition("skill_temporal_anchor", "Temporal Anchor", "Binds an enemy in suspended time.", 10, 0, 2, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=12, stat_scaling=0.4, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_static_cage"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_siphon_mark", "Siphon Mark", "Marks the target to siphon their life force.", 8, 0, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=20, stat_scaling=0.55, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_shadow_decay"),
    ], animation_trigger="CastDark"),
    SkillDefinition("skill_gravatic_pull", "Gravatic Pull", "Draws the battlefield into crushing gravity.", 9, 0, 2, "AllEnemies", False, False, True, [
        SkillEffectDefinition("Damage", base_value=12, stat_scaling=0.35, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_glacial_burden"),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_waning_light", "Waning Light", "Dim light saps a foe's sight and courage.", 6, 0, 1, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=16, stat_scaling=0.45, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_starblind"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_arcane_uprising", "Arcane Uprising", "A surge of arcane power invigorates allies.", 11, 0, 3, "AllAllies", True, True, False, [
        SkillEffectDefinition("Heal", base_value=14, stat_scaling=0.35, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_battle_trance"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_focus_mind"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_seraphic_dive", "Seraphic Dive", "Leap of radiant steel that empowers the wielder.", 0, 4, 2, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=26, stat_scaling=0.7, scaling_stat="Attack"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_zephyr_step", target_self=True),
    ], animation_trigger="SlashHeavy"),
    SkillDefinition("skill_echo_overdrive", "Echo Overdrive", "Amplifies the user's speed and ferocity.", 9, 0, 3, "Self", True, False, False, [
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_battle_trance", target_self=True),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_gale_haste", target_self=True),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_iron_resolve", target_self=True),
    ], animation_trigger="CastVoid"),
    SkillDefinition("skill_solar_inflection", "Solar Inflection", "Solar resonance protects and uplifts allies.", 10, 0, 3, "AllAllies", True, True, False, [
        SkillEffectDefinition("Heal", base_value=18, stat_scaling=0.4, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_crystalline_ward"),
    ], animation_trigger="CastLight"),
    SkillDefinition("skill_void_mirror", "Void Mirror", "Reflective burst of void energy silences a foe.", 9, 0, 2, "Single", False, False, True, [
        SkillEffectDefinition("Damage", base_value=22, stat_scaling=0.6, scaling_stat="Mana"),
        SkillEffectDefinition("ApplyStatus", status_ref="status_effect_null_field"),
    ], animation_trigger="CastVoid"),
]

enemies: List[EnemyDefinition] = [
    EnemyDefinition(
        file_name="enemy_ash_wraith",
        display_name="Ash Wraith",
        combatant_id="ash_wraith",
        stats={"Health": 120, "Mana": 80, "Attack": 26, "Defense": 12, "Speed": 18, "Luck": 6},
        skills=["skill_void_lance", "skill_emberstorm", "skill_shadow_binds"],
        gambit_rules=[
            GambitRuleDefinition("Engulf with Emberstorm", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_emberstorm")),
            GambitRuleDefinition("Void Lance Assault", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_void_lance")),
            GambitRuleDefinition("Fallback Slash", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Attack")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_lunar_priestess",
        display_name="Lunar Priestess",
        combatant_id="lunar_priestess",
        stats={"Health": 95, "Mana": 120, "Attack": 14, "Defense": 16, "Speed": 14, "Luck": 12},
        skills=["skill_aurora_mend", "skill_radiant_convergence", "skill_starlit_barrier"],
        gambit_rules=[
            GambitRuleDefinition("Tend Wounded", GambitRuleTarget("AllyBelow", threshold=0.6, include_self=True), GambitRuleAction("Skill", skill="skill_aurora_mend")),
            GambitRuleDefinition("Raise Barrier", GambitRuleTarget("AllyBelow", threshold=0.8, include_self=True), GambitRuleAction("Skill", skill="skill_starlit_barrier")),
            GambitRuleDefinition("Radiant Renewal", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_radiant_convergence")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_voidwalker",
        display_name="Voidwalker",
        combatant_id="voidwalker",
        stats={"Health": 150, "Mana": 90, "Attack": 28, "Defense": 18, "Speed": 15, "Luck": 8},
        skills=["skill_gravity_well", "skill_null_silence", "skill_rift_rend"],
        gambit_rules=[
            GambitRuleDefinition("Silence Threats", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_null_silence")),
            GambitRuleDefinition("Collapse Formation", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_gravity_well")),
            GambitRuleDefinition("Rift Cleave", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_rift_rend")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_crystal_titan",
        display_name="Crystal Titan",
        combatant_id="crystal_titan",
        stats={"Health": 220, "Mana": 60, "Attack": 32, "Defense": 28, "Speed": 8, "Luck": 4},
        skills=["skill_meteor_crash", "skill_crystal_seal", "skill_guardian_oath"],
        gambit_rules=[
            GambitRuleDefinition("Guarded Core", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_guardian_oath")),
            GambitRuleDefinition("Crystal Imprisonment", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_crystal_seal")),
            GambitRuleDefinition("Meteor Slam", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_meteor_crash")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_stormcaller",
        display_name="Stormcaller",
        combatant_id="stormcaller",
        stats={"Health": 130, "Mana": 110, "Attack": 24, "Defense": 14, "Speed": 20, "Luck": 10},
        skills=["skill_thunder_chain", "skill_stasis_field", "skill_tidal_surge"],
        gambit_rules=[
            GambitRuleDefinition("Stasis Net", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_stasis_field")),
            GambitRuleDefinition("Chain Lightning", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_thunder_chain")),
            GambitRuleDefinition("Surge Finale", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_tidal_surge")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_ember_drake",
        display_name="Ember Drake",
        combatant_id="ember_drake",
        stats={"Health": 180, "Mana": 70, "Attack": 30, "Defense": 18, "Speed": 17, "Luck": 6},
        skills=["skill_emberstorm", "skill_savage_maul", "skill_bleak_rot"],
        gambit_rules=[
            GambitRuleDefinition("Bleak Mark", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_bleak_rot")),
            GambitRuleDefinition("Savage Maul", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_savage_maul")),
            GambitRuleDefinition("Scorching Tempest", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_emberstorm")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_gloom_ranger",
        display_name="Gloom Ranger",
        combatant_id="gloom_ranger",
        stats={"Health": 110, "Mana": 60, "Attack": 26, "Defense": 12, "Speed": 19, "Luck": 9},
        skills=["skill_zephyr_blade", "skill_null_silence", "skill_siphon_mark"],
        gambit_rules=[
            GambitRuleDefinition("Suppress Mage", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_null_silence")),
            GambitRuleDefinition("Siphon Mark", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_siphon_mark")),
            GambitRuleDefinition("Zephyr Strike", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_zephyr_blade")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_starbound_sentinel",
        display_name="Starbound Sentinel",
        combatant_id="starbound_sentinel",
        stats={"Health": 160, "Mana": 80, "Attack": 22, "Defense": 24, "Speed": 12, "Luck": 8},
        skills=["skill_warding_song", "skill_guardian_oath", "skill_tempest_guard"],
        gambit_rules=[
            GambitRuleDefinition("Warding Chorus", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_warding_song")),
            GambitRuleDefinition("Tempest Empowerment", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_tempest_guard")),
            GambitRuleDefinition("Hold the Line", GambitRuleTarget("Self"), GambitRuleAction("Defend")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_dread_marauder",
        display_name="Dread Marauder",
        combatant_id="dread_marauder",
        stats={"Health": 140, "Mana": 50, "Attack": 34, "Defense": 16, "Speed": 18, "Luck": 7},
        skills=["skill_dread_rip", "skill_meteor_crash", "skill_savage_maul"],
        gambit_rules=[
            GambitRuleDefinition("Dread Rend", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_dread_rip")),
            GambitRuleDefinition("Meteor Hammer", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_meteor_crash")),
            GambitRuleDefinition("Relentless Assault", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Attack")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_auric_savant",
        display_name="Auric Savant",
        combatant_id="auric_savant",
        stats={"Health": 105, "Mana": 130, "Attack": 18, "Defense": 14, "Speed": 16, "Luck": 14},
        skills=["skill_arcane_uprising", "skill_radiant_convergence", "skill_mind_fog"],
        gambit_rules=[
            GambitRuleDefinition("Mind Shroud", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_mind_fog")),
            GambitRuleDefinition("Arcane Surge", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_arcane_uprising")),
            GambitRuleDefinition("Radiant Chorus", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_radiant_convergence")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_shadow_binder",
        display_name="Shadow Binder",
        combatant_id="shadow_binder",
        stats={"Health": 125, "Mana": 100, "Attack": 22, "Defense": 15, "Speed": 18, "Luck": 9},
        skills=["skill_shadow_binds", "skill_void_mirror", "skill_dusk_vortex"],
        gambit_rules=[
            GambitRuleDefinition("Void Mirror", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_void_mirror")),
            GambitRuleDefinition("Shadow Bind", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_shadow_binds")),
            GambitRuleDefinition("Dusk Vortex", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_dusk_vortex")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_glacial_colossus",
        display_name="Glacial Colossus",
        combatant_id="glacial_colossus",
        stats={"Health": 240, "Mana": 80, "Attack": 28, "Defense": 32, "Speed": 9, "Luck": 5},
        skills=["skill_gravatic_pull", "skill_crystal_seal", "skill_guardian_oath"],
        gambit_rules=[
            GambitRuleDefinition("Gravitic Crush", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_gravatic_pull")),
            GambitRuleDefinition("Frozen Bind", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_crystal_seal")),
            GambitRuleDefinition("Fortify Core", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_guardian_oath")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_soul_harvester",
        display_name="Soul Harvester",
        combatant_id="soul_harvester",
        stats={"Health": 150, "Mana": 100, "Attack": 27, "Defense": 16, "Speed": 17, "Luck": 10},
        skills=["skill_siphon_mark", "skill_entropy_wave", "skill_null_silence"],
        gambit_rules=[
            GambitRuleDefinition("Null Ward", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_null_silence")),
            GambitRuleDefinition("Entropy Collapse", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_entropy_wave")),
            GambitRuleDefinition("Harvest Mark", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_siphon_mark")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_arcane_duelist",
        display_name="Arcane Duelist",
        combatant_id="arcane_duelist",
        stats={"Health": 135, "Mana": 85, "Attack": 30, "Defense": 18, "Speed": 21, "Luck": 11},
        skills=["skill_zephyr_blade", "skill_seraphic_dive", "skill_void_lance"],
        gambit_rules=[
            GambitRuleDefinition("Seraphic Dive", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_seraphic_dive")),
            GambitRuleDefinition("Void Pierce", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_void_lance")),
            GambitRuleDefinition("Swift Cut", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_zephyr_blade")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_chronicle_keeper",
        display_name="Chronicle Keeper",
        combatant_id="chronicle_keeper",
        stats={"Health": 120, "Mana": 140, "Attack": 16, "Defense": 18, "Speed": 14, "Luck": 12},
        skills=["skill_temporal_anchor", "skill_arcane_uprising", "skill_echoing_pulse"],
        gambit_rules=[
            GambitRuleDefinition("Temporal Bind", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_temporal_anchor")),
            GambitRuleDefinition("Arcane Chronicle", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_arcane_uprising")),
            GambitRuleDefinition("Echoing Pulse", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_echoing_pulse")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_thorn_matron",
        display_name="Thorn Matron",
        combatant_id="thorn_matron",
        stats={"Health": 170, "Mana": 90, "Attack": 24, "Defense": 20, "Speed": 16, "Luck": 9},
        skills=["skill_bleak_rot", "skill_resolute_guard", "skill_crescent_refresh"],
        gambit_rules=[
            GambitRuleDefinition("Protect the Brood", GambitRuleTarget("AllyBelow", threshold=0.7, include_self=False), GambitRuleAction("Skill", skill="skill_resolute_guard")),
            GambitRuleDefinition("Bleak Poison", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_bleak_rot")),
            GambitRuleDefinition("Renewing Chorus", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_crescent_refresh")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_siren_of_depths",
        display_name="Siren of Depths",
        combatant_id="siren_of_depths",
        stats={"Health": 130, "Mana": 125, "Attack": 21, "Defense": 14, "Speed": 18, "Luck": 13},
        skills=["skill_mind_fog", "skill_harmonic_salve", "skill_tidal_surge"],
        gambit_rules=[
            GambitRuleDefinition("Harmonic Aid", GambitRuleTarget("AllyBelow", threshold=0.65, include_self=True), GambitRuleAction("Skill", skill="skill_harmonic_salve")),
            GambitRuleDefinition("Mind Veil", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_mind_fog")),
            GambitRuleDefinition("Tidal Cast", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_tidal_surge")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_ember_alchemist",
        display_name="Ember Alchemist",
        combatant_id="ember_alchemist",
        stats={"Health": 115, "Mana": 110, "Attack": 23, "Defense": 14, "Speed": 17, "Luck": 12},
        skills=["skill_emberstorm", "skill_harmonic_salve", "skill_voidflare_burst"],
        gambit_rules=[
            GambitRuleDefinition("Stabilize Ally", GambitRuleTarget("AllyBelow", threshold=0.6, include_self=True), GambitRuleAction("Skill", skill="skill_harmonic_salve")),
            GambitRuleDefinition("Voidflare Burst", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_voidflare_burst")),
            GambitRuleDefinition("Ember Cycle", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_emberstorm")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_ironclad_guardian",
        display_name="Ironclad Guardian",
        combatant_id="ironclad_guardian",
        stats={"Health": 210, "Mana": 60, "Attack": 26, "Defense": 30, "Speed": 10, "Luck": 6},
        skills=["skill_guardian_oath", "skill_warding_song", "skill_resolute_guard"],
        gambit_rules=[
            GambitRuleDefinition("Shield the Weak", GambitRuleTarget("AllyBelow", threshold=0.5, include_self=False), GambitRuleAction("Skill", skill="skill_resolute_guard")),
            GambitRuleDefinition("Warding Canticle", GambitRuleTarget("Self"), GambitRuleAction("Skill", skill="skill_warding_song")),
            GambitRuleDefinition("Hold Position", GambitRuleTarget("Self"), GambitRuleAction("Defend")),
        ],
    ),
    EnemyDefinition(
        file_name="enemy_voidling_swarm",
        display_name="Voidling Swarm",
        combatant_id="voidling_swarm",
        stats={"Health": 90, "Mana": 60, "Attack": 22, "Defense": 12, "Speed": 22, "Luck": 7},
        skills=["skill_dusk_vortex", "skill_entropy_wave", "skill_void_lance"],
        gambit_rules=[
            GambitRuleDefinition("Entropy Pulse", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_entropy_wave")),
            GambitRuleDefinition("Dusk Swarm", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_dusk_vortex")),
            GambitRuleDefinition("Void Needle", GambitRuleTarget("RandomEnemy"), GambitRuleAction("Skill", skill="skill_void_lance")),
        ],
    ),
]


def main() -> int:
    ensure_folder_with_meta(STATUS_DIR)
    ensure_folder_with_meta(SKILL_DIR)
    ensure_folder_with_meta(ENEMY_DIR)
    ensure_folder_with_meta(GAMBIT_DIR)
    ensure_folder_with_meta(RUN_DIR)
    ensure_folder_with_meta(CONSUMABLE_DIR)
    ensure_folder_with_meta(KEYITEM_DIR)
    for directory in EQUIPMENT_SLOT_DIRS.values():
        ensure_folder_with_meta(directory)

    status_guids: Dict[str, str] = {}
    for status in statuses:
        guid = deterministic_guid(f"asset:{status.file_name}")
        status_guids[status.file_name] = guid
        write_status_asset(status, guid)

    item_guids: Dict[str, str] = {}
    for item in consumables:
        guid = deterministic_guid(f"asset:{item.file_name}")
        item_guids[item.file_name] = guid
        write_item_asset_to_dir(item, guid, status_guids, CONSUMABLE_DIR)
    for item in key_items:
        guid = deterministic_guid(f"asset:{item.file_name}")
        item_guids[item.file_name] = guid
        write_item_asset_to_dir(item, guid, status_guids, KEYITEM_DIR)

    equipment_guids: Dict[str, str] = {}
    for equipment in equipment_items:
        guid = deterministic_guid(f"asset:{equipment.file_name}")
        equipment_guids[equipment.file_name] = guid
        write_equipment_asset(equipment, guid, status_guids)

    item_guids.update(equipment_guids)

    skill_guids: Dict[str, str] = {}
    for skill in skills:
        guid = deterministic_guid(f"asset:{skill.file_name}")
        skill_guids[skill.file_name] = guid
        write_skill_asset(skill, guid, status_guids)

    gambit_guids: Dict[str, str] = {}
    for enemy in enemies:
        guid = deterministic_guid(f"asset:gambit:{enemy.file_name}")
        gambit_guids[enemy.file_name] = guid
        write_gambit_asset(enemy, guid, skill_guids)

    enemy_guids: Dict[str, str] = {}
    for enemy in enemies:
        guid = deterministic_guid(f"asset:{enemy.file_name}")
        enemy_guids[enemy.file_name] = guid
        gambit_guid = gambit_guids[enemy.file_name]
        write_enemy_asset(enemy, guid, skill_guids, gambit_guid)

    enemy_cycle = [enemy.file_name for enemy in enemies]
    reward_sequence = [
        "item_consumable_starlight_tonic",
        "item_consumable_celestial_ration",
        "item_consumable_ironroot_elixir",
        "item_consumable_windsprint_serum",
        "item_consumable_safeguard_balm",
        "item_consumable_starshard_ampoule",
        "item_head_nebula_hood",
        "item_mainhand_starforged_blade",
        "item_accessory_void_pendant",
        "item_relic_singularity_core",
        "item_key_astral_compass",
        "item_key_planar_anchor",
    ]

    floors: List[RunFloorConfig] = []
    for idx in range(100):
        number = idx + 1
        base_count = 1 + (idx % 3)
        extra = number // 40
        enemy_count = min(4, base_count + extra)
        start_index = (idx * 2) % len(enemy_cycle)
        enemy_refs = [enemy_cycle[(start_index + offset) % len(enemy_cycle)] for offset in range(enemy_count)]

        reward_items: List[tuple[str, int]] = []
        if number % 5 == 0:
            reward_key = reward_sequence[(number // 5 - 1) % len(reward_sequence)]
            quantity = 1 + number // 50
            reward_items.append((reward_key, quantity))
        if number % 20 == 0:
            reward_key = equipment_items[(number // 20 - 1) % len(equipment_items)].file_name
            reward_items.append((reward_key, 1))

        experience = 120 + number * 25
        currency = 80 + number * 15
        heal_on_start = number % 10 == 0
        restore_ratio = 0.5 if heal_on_start else 0.0

        floors.append(RunFloorConfig(
            floor_id=f"floor_{number:03d}",
            display_name=f"Floor {number}",
            number=number,
            enemies=enemy_refs,
            rewards=RewardBundle(experience=experience, currency=currency, items=reward_items),
            heal_on_start=heal_on_start,
            restore_ratio=restore_ratio,
        ))

    completion_rewards = RewardBundle(
        experience=12000,
        currency=6000,
        items=[
            ("item_key_mnemonic_orb", 1),
            ("item_key_skyseal_brooch", 1),
            ("item_relic_singularity_core", 1),
        ],
    )

    run_name = "combat_run_eternal_gauntlet"
    run_guid = deterministic_guid(f"asset:{run_name}")
    write_run_asset(run_name, run_guid, floors, completion_rewards, enemy_guids, item_guids)

    print(f"Generated {len(statuses)} status effects, {len(skills)} skills, {len(enemies)} enemies, {len(consumables)} consumables, {len(key_items)} key items, {len(equipment_items)} equipment items, and a combat run with {len(floors)} floors.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
