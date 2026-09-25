using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;

namespace GhostUserRunner.Core.Planning;

public interface IActivityPlanner
{
    ProposedAction Next(ObservedContext context);
    void Record(ActionOutcome outcome);
}
