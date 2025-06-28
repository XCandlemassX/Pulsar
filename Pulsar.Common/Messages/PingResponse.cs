using System;
using ProtoBuf;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;

namespace Pulsar.Common.Messages
{
    [ProtoContract]
    public class PingResponse : IMessage
    {
    }
}
