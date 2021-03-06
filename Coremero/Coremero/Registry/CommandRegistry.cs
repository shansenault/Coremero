﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Coremero.Commands;
using Coremero.Messages;
using Coremero.Utilities;

namespace Coremero.Registry
{
    public class CommandRegistry
    {
        private readonly Dictionary<CommandAttribute, Func<IInvocationContext,IMessage, object>> _commandMap =
            new Dictionary<CommandAttribute, Func<IInvocationContext,IMessage, object>>();

        /// <summary>
        /// Registers all methods with the [Command] attribute in to the command map.
        /// </summary>
        /// <param name="plugin">The instance of the plugin to register in the command map.</param>
        public void Register(IPlugin plugin)
        {
            Type pluginType = plugin.GetType();
            foreach (var methodInfo in pluginType.GetRuntimeMethods())
            {
                CommandAttribute attribute = methodInfo.GetCustomAttribute<CommandAttribute>();
                if (attribute != null)
                {
                    // Don't trust the developer to remember to set HasSideEffects. Sorry.
                    if (methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task))
                    {
                        attribute.HasSideEffects = true;
                    }

                    // Check if the parameters are right.
                    ParameterInfo[] methodParams = methodInfo.GetParameters();
                    if (methodParams.Length != 2)
                    {
                        Debug.Fail($"Command {attribute.Name} has an invalid parameter count of {methodParams.Length}.");
                        continue;
                    }

                    if (methodParams[0].ParameterType != typeof(IInvocationContext))
                    {
                        Debug.Fail(
                            $"Command {attribute.Name} has an invalid first argument. You should only use MethodName(IInvocationContext context, IMessage message).");
                        continue;
                    }

                    if (methodParams[1].ParameterType != typeof(IMessage))
                    {
                        Debug.Fail(
                            $"Command {attribute.Name} has an invalid second argument. You should only use MethodName(IInvocationContext context, IMessage message).");
                        continue;
                    }


                    // If attribute exists, clear.
                    // TODO: Check if no leaks after due to delegates being delegates.
                    if (_commandMap.ContainsKey(attribute))
                    {
                        _commandMap.Remove(attribute);
                    }

                    // Register command
                    _commandMap[attribute] = delegate(IInvocationContext context, IMessage message)
                    {
                        return methodInfo.Invoke(plugin, new object[] { context, message });
                    };

                }
            }
        }

        /// <summary>
        /// Retrieves the command attribute for a command string.
        /// </summary>
        /// <param name="commandName">The string to search.</param>
        /// <returns>CommandAttribute if found else null.</returns>
        private CommandAttribute GetCommand(string commandName)
        {
            CommandAttribute selectedCommand =
                _commandMap.Keys.Where(x => x.Name.DamerauLevenshteinDistance(commandName, 1) != int.MaxValue).FirstOrDefault();

            // TODO: Can we avoid double calculation by stuffing the result of the calculation temporarily during the LINQ query?
            if (selectedCommand == null || selectedCommand.Name.DamerauLevenshteinDistance(commandName, 1) == int.MaxValue)
            {
                // Not even close. Go away.
                return null;
            }
            return selectedCommand;
        }

        public async Task<IMessage> ExecuteCommandAsync(string commandName, IInvocationContext context, IMessage message)
        {
            var selectedCommand = GetCommand(commandName);

            if (selectedCommand == null)
            {
                return null;
            }

            return await Task.Run(async () =>
            {
                var result = _commandMap[selectedCommand](context, message);

                // Check if the command is actually a task, if so, start that bad boy up and return result.
                if (result is Task)
                {
                    await (Task) result;
                    result = result.GetType().GetRuntimeProperty("Result")?.GetValue(result);
                }

                if (result == null)
                {
                    return Message.Create(String.Empty);
                }

                if (result is IMessage)
                {
                    return (IMessage) result;
                }

                return Message.Create(result.ToString());
            });
        }

        public IMessage ExecuteCommand(string commandName, IInvocationContext context, IMessage message)
        {
            return ExecuteCommandAsync(commandName, context, message).Result;
        }

        public bool IsCommandComplexOrNull(string commandName)
        {
            var cmd = GetCommand(commandName);
            return !(cmd?.HasSideEffects ?? true);
        }

        public bool Exists(string commandName)
        {
            return GetCommand(commandName) != null;
        }

        public string GetHelp(string commandName)
        {
            return GetCommand(commandName)?.Help;
        }

        public List<CommandAttribute> CommandAttributes
        {
            get { return _commandMap.Keys.ToList(); }
        }
    }
}
