using Pulsar.Common.Messages;

namespace Pulsar.Client.Messages
{
    public abstract class NotificationMessageProcessor : MessageProcessorBase<string>
    {
        protected NotificationMessageProcessor() : base(true)
        {
        }
    }
}
