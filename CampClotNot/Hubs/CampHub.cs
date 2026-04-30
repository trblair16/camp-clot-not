using Microsoft.AspNetCore.SignalR;

namespace CampClotNot.Hubs;

/// <summary>
/// Real-time hub for score and board state updates.
///
/// v0.2.0 events: ScoresUpdated
/// v0.3.0 events (block hit animation — plan before building):
///   BlockHitTriggered(groupId, campDay)
///   BlockHitNumberRevealed(groupId, number)
///   TokenMoveStep(groupId, spaceIndex)
///   TokenMoveDone(groupId, finalSpaceIndex)
///
/// All events broadcast to all clients. The /display route subscribes and drives
/// projector animations. The triggering tablet calls admin endpoints only.
/// </summary>
public class CampHub : Hub
{
    public async Task ScoresUpdated() =>
        await Clients.All.SendAsync("ScoresUpdated");
}
