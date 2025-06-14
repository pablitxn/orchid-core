using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebApi.Hubs;

/// <summary>
///     SignalR hub used for WebRTC signaling and recording during a huddle session.
/// </summary>
[Authorize]
public sealed class HuddleHub(IHuddleRecordingService recordingService) : Hub
{
    private readonly IHuddleRecordingService _recording =
        recordingService ?? throw new ArgumentNullException(nameof(recordingService));

    /// <summary>Joins the caller to the specified room.</summary>
    public Task JoinRoom(string roomId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, roomId);
    }

    /// <summary>Forwards a WebRTC offer to other participants.</summary>
    public Task SendOffer(string roomId, string offer)
    {
        if (string.IsNullOrEmpty(roomId)) return Task.CompletedTask;
        return Clients.OthersInGroup(roomId).SendAsync("ReceiveOffer", offer);
    }

    /// <summary>Forwards a WebRTC answer to other participants.</summary>
    public Task SendAnswer(string roomId, string answer)
    {
        if (string.IsNullOrEmpty(roomId)) return Task.CompletedTask;
        return Clients.OthersInGroup(roomId).SendAsync("ReceiveAnswer", answer);
    }

    /// <summary>Forwards an ICE candidate to other participants.</summary>
    public Task SendIceCandidate(string roomId, string candidate)
    {
        if (string.IsNullOrEmpty(roomId)) return Task.CompletedTask;
        return Clients.OthersInGroup(roomId).SendAsync("ReceiveIceCandidate", candidate);
    }

    /// <summary>Stores a recorded media segment for the room.</summary>
    public async Task SendVideoSegment(string roomId, byte[] segment)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        using var ms = new MemoryStream(segment);
        await _recording.StoreSegmentAsync(roomId, ms);
    }
}