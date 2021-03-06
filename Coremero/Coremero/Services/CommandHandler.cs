﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coremero.Messages;
using Coremero.Registry;
using Coremero.Utilities;

namespace Coremero.Services
{
    /// <summary>
    /// Handles messages from the messagebus that may have a message and performs invocation.
    /// </summary>
    public class CommandHandler : ICommandHandler
    {
        private readonly IMessageBus _messageBus;
        private readonly CommandRegistry _commandRegistry;

        public CommandHandler(IMessageBus messageBus, CommandRegistry commandRegistry)
        {
            _messageBus = messageBus;
            _commandRegistry = commandRegistry;

            messageBus.Received += MessageBusOnReceived;
            messageBus.Sent += MessageBusOnSent;
        }

        private void MessageBusOnReceived(object sender, MessageReceivedEventArgs eventArgs)
        {
            // Unpack locally.
            IInvocationContext context = eventArgs.Context;
            IMessage message = eventArgs.Message;

            // Is it a command?
            if (message.Text?.StartsWith(".") != true)
            {
                return;
            }

            // Check if command exists.
            string command = message.Text.Split(' ').First().TrimStart('.');

            if (!_commandRegistry.Exists(command))
            {
                Debug.WriteLine($"Invalid command called: {command}");
                return;
            }


            // Ensure we do not back up the rest of the command invocation queue.
            // TODO: Per-server task pools.
            Task.Run(async () =>
            {
                try
                {
                    IMessage result = await _commandRegistry.ExecuteCommandAsync(command, context, message);
                    if (result != null)
                    {
                        _messageBus.RaiseOutgoing(context.Raiser, result);
                    }
                }
                catch (Exception e)
                {
                    // Check if there is a help function.
                    var help = _commandRegistry.GetHelp(command);
                    if (!String.IsNullOrEmpty(help))
                    {
                        _messageBus.RaiseOutgoing(context.Raiser, Message.Create(help));
                    }
                    else
                    {
                        _messageBus.RaiseOutgoing(context.Raiser, Message.Create("```\n" + e.StackTrace + "\n```", new FileAttachment(Path.Combine(PathExtensions.AppDir,"error.jpg"))));
                    }
                }
            });
        }

        private void MessageBusOnSent(object sender, MessageSentEventArgs messageSentEventArgs)
        {
            // TODO: Move this to be per-client so they can format for their audience?
            if (messageSentEventArgs.Message != null)
            {
                messageSentEventArgs.Target.Send(messageSentEventArgs.Message);
            }
        }

    }
}
