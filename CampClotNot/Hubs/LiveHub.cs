using Microsoft.AspNetCore.SignalR;

namespace CampClotNot.Hubs;

/// <summary>
/// Real-time hub for live score, board, and mini-game updates.
///
/// Server broadcasts directly via IHubContext&lt;LiveHub&gt; injected into services.
/// Hub methods below are client-callable mirrors (same pattern as ScoresUpdated).
///
/// Board events:
///   ScoresUpdated                          — leaderboard changed
///   BlockHitTriggered(groupId, campDay)    — block hit started for a group
///   BlockHitNumberRevealed(groupId, steps) — roll number revealed
///   TokenMoveStep(groupId, spaceIndex)     — token moved to intermediate space
///   TokenMoveDone(groupId, spaceIndex)     — token landed on final space
///
/// Mini-game events:
///   MiniGameSpinTriggered(campDay)                    — display starts cycling animation
///   MiniGameSpinRevealed(activityId, activityName)    — display shows scripted result
///   MiniGameSpinReset()                               — display returns to idle
/// </summary>
public class LiveHub : Hub
{
    public async Task ScoresUpdated() =>
        await Clients.All.SendAsync("ScoresUpdated");

    public async Task BlockHitTriggered(Guid groupId, int campDay) =>
        await Clients.All.SendAsync("BlockHitTriggered", groupId, campDay);

    public async Task BlockHitNumberRevealed(Guid groupId, int rollNumber) =>
        await Clients.All.SendAsync("BlockHitNumberRevealed", groupId, rollNumber);

    public async Task TokenMoveStep(Guid groupId, int spaceIndex) =>
        await Clients.All.SendAsync("TokenMoveStep", groupId, spaceIndex);

    public async Task TokenMoveDone(Guid groupId, int finalSpaceIndex) =>
        await Clients.All.SendAsync("TokenMoveDone", groupId, finalSpaceIndex);

    public async Task MiniGameSpinTriggered(int campDay) =>
        await Clients.All.SendAsync("MiniGameSpinTriggered", campDay);

    public async Task MiniGameSpinRevealed(Guid activityId, string activityName) =>
        await Clients.All.SendAsync("MiniGameSpinRevealed", activityId, activityName);

    public async Task MiniGameSpinReset() =>
        await Clients.All.SendAsync("MiniGameSpinReset");
}
