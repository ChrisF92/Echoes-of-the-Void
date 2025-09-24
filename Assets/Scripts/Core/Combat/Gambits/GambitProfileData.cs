using System;
using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Combat.Gambits
{
  [Serializable]
  public class GambitProfileData : IGambitRuleSource
  {
    public string profileId;
    public string displayName;
    public List<GambitRuleDefinition> rules = new();

    public GambitProfileData()
    {
    }

    public GambitProfileData(string profileId, string displayName, IEnumerable<GambitRuleDefinition> rules)
    {
      this.profileId = profileId;
      this.displayName = displayName;
      if (rules != null)
      {
        this.rules = new List<GambitRuleDefinition>(rules);
      }
    }

    public IReadOnlyList<GambitRuleDefinition> Rules => rules;

    public string DisplayName
    {
      get
      {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
          return displayName;
        }

        if (!string.IsNullOrWhiteSpace(profileId))
        {
          return profileId;
        }

        return "Runtime Gambit";
      }
    }

    public static GambitProfileData FromSource(IGambitRuleSource source, string profileId = null)
    {
      if (source == null)
      {
        return null;
      }

      return new GambitProfileData(profileId, source.DisplayName, source.Rules);
    }
  }
}
