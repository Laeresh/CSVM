using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;

namespace CSVM.Master.Tests;

/// <summary>A clock a test moves by hand.</summary>
internal sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
}

/// <summary>A socket that keeps what the hub sent it.</summary>
internal sealed class FakeClient : IMasterClient
{
    public FakeClient(string address = "203.0.113.5") => Address = address;

    public string Address { get; }

    public List<MasterMessage> Inbox { get; } = new();

    public string? ClosedWhy { get; private set; }

    public MasterMessage Last => Inbox[^1];

    public void Send(MasterMessage message) => Inbox.Add(message);

    public void Close(string why) => ClosedWhy = why;

    public IEnumerable<MasterMessage> Of(string type) => Inbox.Where(message => message.T == type);
}
