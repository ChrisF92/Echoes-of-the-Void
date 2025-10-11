using System.Collections.Generic;
using EchoesOfTheVoid.Core.Roster.Progression.Definitions;

namespace EchoesOfTheVoid.Core.Roster.Progression.Contracts {
  public interface IEchoSkillTreeDefinition {
    string TreeId { get; }
    IReadOnlyList<SkillTreeNodeDefinition> Nodes { get; }
    IReadOnlyList<SkillTreeNodeDefinition> RootNodes { get; }
    bool TryGetNode(string nodeId, out SkillTreeNodeDefinition node);
  }
}
