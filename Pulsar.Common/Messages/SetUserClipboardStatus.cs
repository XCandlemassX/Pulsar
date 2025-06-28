using ProtoBuf;
using Pulsar.Common.Messages.Other;

namespace Pulsar.Common.Messages
{
    [ProtoContract]
    public class SetUserClipboardStatus : IMessage
    {
        [ProtoMember(1)]
        public string ClipboardText { get; set; }
    }
}
