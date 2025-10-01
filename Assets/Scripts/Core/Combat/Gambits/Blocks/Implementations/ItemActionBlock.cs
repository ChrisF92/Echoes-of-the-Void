using System;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations {
  [Serializable]
  public class ItemActionBlock : GambitActionBlock {
    public ItemScriptableObject item;
    public bool requireAvailability = true;

    public override string Summary => item != null ? $"Use Item: {item.DisplayName}" : "Use Item";

    public override bool TryBuildAction(GambitRuntimeContext context, ICombatant target, out CombatAction action, out string failureReason) {
      action = null;
      if (item == null) {
        failureReason = "Item not set";
        return false;
      }

      InventoryComponent inventory = context?.Actor?.GetComponent<InventoryComponent>();
      if (inventory == null) {
        failureReason = "No inventory component";
        return false;
      }

      if (requireAvailability && !inventory.HasItem(item.ItemId)) {
        failureReason = "Item not available";
        return false;
      }

      action = new CombatAction {
        ActionType = CombatActionType.Item,
        ItemData = item,
        Target = target
      };

      failureReason = null;
      return true;
    }
  }
}
