using Microsoft.AspNetCore.SignalR;

namespace CampClotNot.Hubs;

/// <summary>
/// Real-time hub for score and board state updates.
///
/// Server broadcasts directly via IHubContext&lt;CampHub&gt; injected into services.
/// Hub methods below are client-callable mirrors (same pattern as ScoresUpdated).
///
/// Events:
///   ScoresUpdated                          — leaderboard changed
///   BlockHitTriggered(groupId, campDay)    — block hit started for a group
///   BlockHitNumberRevealed(groupId, steps) — roll number revealed
///   TokenMoveStep(groupId, spaceIndex)     — token moved to intermediate space
///   TokenMoveDone(groupId, spaceIndex)     — token landed on final space
/// </summary>
public class CampHub : Hub
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
}
