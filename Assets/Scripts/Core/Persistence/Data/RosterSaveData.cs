using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Gambits;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Persistence {
  [Serializable]
  public class RosterSaveData {
    public bool IsInitialized;
    public List<EchoSaveData> Echoes = new();
    public List<PartySlotSaveData> PartySlots = new();
  }

  [Serializable]
  public class EchoSaveData {
    public string InstanceId = string.Empty;
    public string TemplateId = string.Empty;
    public string CustomName = string.Empty;
    public int Level = 1;
    public int Experience;
    public int UnspentSkillPoints;
    public bool IsLocked;
    public Vector2Int PreferredFormationSlot = new(-1, -1);
    public List<string> UnlockedSkillNodes = new();
    public List<EquipmentAssignmentData> Equipment = new();
    public List<GambitProfileData> GambitSlots = new();
    public int ActiveGambitIndex;
  }

  [Serializable]
  public class EquipmentAssignmentData {
    public string SlotId = string.Empty;
    public string ItemId = string.Empty;
  }

  [Serializable]
  public class PartySlotSaveData {
    public int SlotIndex;
    public string EchoInstanceId = string.Empty;
  }
}
