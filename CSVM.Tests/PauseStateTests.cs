using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Splitscreen pause ownership (<see cref="PauseState"/>, E43), off-engine: any player
/// pauses, only the pauser resumes, a rejected unpause is a silent no-op that fires no event.
/// </summary>
public class PauseStateTests
{
    [Fact]
    public void AnyPlayerCanPauseFromRunning()
    {
        var state = new PauseState();

        bool changed = state.TryToggle(playerIndex: 1);

        Assert.True(changed);
        Assert.True(state.Paused);
        Assert.Equal(1, state.OwnerPlayerIndex);
    }

    [Fact]
    public void OnlyTheOwnerCanResume()
    {
        var state = new PauseState();
        state.TryToggle(playerIndex: 1);

        bool changed = state.TryToggle(playerIndex: 1);

        Assert.True(changed);
        Assert.False(state.Paused);
        Assert.Equal(-1, state.OwnerPlayerIndex);
    }

    [Fact]
    public void AnotherPlayersUnpauseAttemptIsRejected()
    {
        var state = new PauseState();
        state.TryToggle(playerIndex: 1);

        bool changed = state.TryToggle(playerIndex: 2);

        Assert.False(changed);
        Assert.True(state.Paused);
        Assert.Equal(1, state.OwnerPlayerIndex);
    }

    [Fact]
    public void ChangedFiresOnlyOnAcceptedTransitions()
    {
        var state = new PauseState();
        int fired = 0;
        state.Changed += () => fired++;

        state.TryToggle(playerIndex: 1);       // pause: fires
        state.TryToggle(playerIndex: 2);       // rejected unpause: does not fire
        state.TryToggle(playerIndex: 1);       // resume: fires

        Assert.Equal(2, fired);
    }
}
