using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Combat.Gambits {
  public interface IGambitRuleSource {
    IReadOnlyList<GambitRuleDefinition> Rules { get; }
    string DisplayName { get; }
  }
}
