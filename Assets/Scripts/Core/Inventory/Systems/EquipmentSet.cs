using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Inventory.Systems {
  public class EquipmentSet {
    private static readonly EquipmentSlotType[] _defaultLayout =
    {
      EquipmentSlotType.Head,
      EquipmentSlotType.Chest,
      EquipmentSlotType.Legs,
      EquipmentSlotType.MainHand,
      EquipmentSlotType.OffHand,
      EquipmentSlotType.Accessory,
      EquipmentSlotType.Relic
    };

    private readonly Dictionary<EquipmentSlotType, EquipmentSlot> _slots;
    private EquipmentItemScriptableObject _twoHandOccupant;
    private EquipmentSlotType? _twoHandAnchor;

    public EquipmentSet() : this(_defaultLayout) {
    }

    public EquipmentSet(IEnumerable<EquipmentSlotType> slotLayout) {
      _slots = new Dictionary<EquipmentSlotType, EquipmentSlot>();
      foreach (EquipmentSlotType slotType in slotLayout) {
        if (_slots.ContainsKey(slotType)) {
          continue;
        }

        _slots.Add(slotType, new EquipmentSlot(slotType));
      }
    }

    public IEnumerable<EquipmentSlot> Slots => _slots.Values;

    public bool TryGetSlot(EquipmentSlotType slotType, out EquipmentSlot slot) {
      return _slots.TryGetValue(slotType, out slot);
    }

    public bool HasSlot(EquipmentSlotType slotType) {
      return _slots.ContainsKey(slotType);
    }

    public EquipmentItemScriptableObject GetEquippedItem(EquipmentSlotType slotType) {
      return _slots.TryGetValue(slotType, out EquipmentSlot slot) ? slot.Item : null;
    }

    public IEnumerable<EquipmentItemScriptableObject> GetEquippedItems() {
      var seen = new HashSet<EquipmentItemScriptableObject>();
      foreach (EquipmentSlot slot in _slots.Values) {
        if (slot.Item == null) {
          continue;
        }

        if (seen.Add(slot.Item)) {
          yield return slot.Item;
        }
      }
    }

    public bool IsSlotBlocked(EquipmentSlotType slotType) {
      return _twoHandAnchor.HasValue && IsHandSlot(slotType) && IsHandSlot(_twoHandAnchor.Value) && _twoHandAnchor.Value != slotType;
    }

    public bool TryEquip(EquipmentItemScriptableObject item, EquipmentSlotType targetSlot, out List<EquipmentDisplacement> displacedItems) {
      displacedItems = new List<EquipmentDisplacement>();

      if (item == null) {
        return false;
      }

      if (!_slots.TryGetValue(targetSlot, out EquipmentSlot slot)) {
        return false;
      }

      if (!slot.CanAccept(item)) {
        return false;
      }

      if (item.OccupiesBothHands) {
        return TryEquipTwoHanded(item, targetSlot, displacedItems);
      }

      if (_twoHandAnchor.HasValue && IsHandSlot(targetSlot) && _twoHandAnchor.Value != targetSlot) {
        _ = RemoveSlotItem(_twoHandAnchor.Value, displacedItems);
      }

      EquipmentItemScriptableObject removed = slot.Replace(item);
      if (removed != null) {
        displacedItems.Add(new EquipmentDisplacement(targetSlot, removed));
        if (removed == _twoHandOccupant) {
          _twoHandOccupant = null;
          _twoHandAnchor = null;
        }
      } else if (_twoHandAnchor.HasValue && _twoHandAnchor.Value == targetSlot) {
        _twoHandOccupant = null;
        _twoHandAnchor = null;
      }

      return true;
    }

    public bool TryUnequip(EquipmentSlotType slotType, out EquipmentDisplacement displacement) {
      displacement = default;

      if (!_slots.ContainsKey(slotType)) {
        return false;
      }

      if (_twoHandAnchor.HasValue && IsHandSlot(slotType) && _twoHandAnchor.Value != slotType) {
        EquipmentSlotType anchorSlot = _twoHandAnchor.Value;
        EquipmentItemScriptableObject removedTwoHand = RemoveSlotItem(anchorSlot);
        if (removedTwoHand == null) {
          return false;
        }

        displacement = new EquipmentDisplacement(anchorSlot, removedTwoHand);
        return true;
      }

      EquipmentItemScriptableObject removed = RemoveSlotItem(slotType);
      if (removed == null) {
        return false;
      }

      displacement = new EquipmentDisplacement(slotType, removed);
      return true;
    }

    public void Clear() {
      foreach (EquipmentSlotType slotType in _slots.Keys.ToList()) {
        _ = RemoveSlotItem(slotType);
      }
    }

    private bool TryEquipTwoHanded(EquipmentItemScriptableObject item, EquipmentSlotType targetSlot, List<EquipmentDisplacement> displacedItems) {
      if (!IsHandSlot(targetSlot)) {
        return false;
      }

      if (!_slots.TryGetValue(targetSlot, out EquipmentSlot anchorSlot)) {
        return false;
      }

      EquipmentSlotType oppositeSlotType = targetSlot == EquipmentSlotType.MainHand ? EquipmentSlotType.OffHand : EquipmentSlotType.MainHand;
      if (!_slots.ContainsKey(oppositeSlotType)) {
        return false;
      }

      _ = RemoveSlotItem(targetSlot, displacedItems);
      _ = RemoveSlotItem(oppositeSlotType, displacedItems);

      _ = anchorSlot.Replace(item);
      _twoHandOccupant = item;
      _twoHandAnchor = targetSlot;

      return true;
    }

    private EquipmentItemScriptableObject RemoveSlotItem(EquipmentSlotType slotType, List<EquipmentDisplacement> displacedItems = null) {
      if (!_slots.TryGetValue(slotType, out EquipmentSlot slot)) {
        return null;
      }

      EquipmentItemScriptableObject removed = slot.Replace(null);
      if (removed == null) {
        return null;
      }

      displacedItems?.Add(new EquipmentDisplacement(slotType, removed));

      if (removed == _twoHandOccupant) {
        _twoHandOccupant = null;
        _twoHandAnchor = null;
      }

      return removed;
    }

    private static bool IsHandSlot(EquipmentSlotType slotType) {
      return slotType is EquipmentSlotType.MainHand or EquipmentSlotType.OffHand;
    }
  }
}
