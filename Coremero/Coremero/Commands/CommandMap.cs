﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Coremero.Utilities;

namespace Coremero.Commands
{
    public class CommandMap
    {
        private readonly Dictionary<CommandAttribute, Func<IMessageContext, object>> _commandMap =
            new Dictionary<CommandAttribute, Func<IMessageContext, object>>();

        private List<Type> _validCommandReturnTypes = new List<Type>()
        {
            typeof(int),
            typeof(string),
            typeof(void),
            typeof(Task<string>),
            typeof(Task),
            typeof(Task<int>)
        };

        public void RegisterPluginCommands(IPlugin plugin)
        {
            Type pluginType = plugin.GetType();
            foreach (var methodInfo in pluginType.GetRuntimeMethods())
            {
                CommandAttribute attribute = methodInfo.GetCustomAttribute<CommandAttribute>();
                if (attribute != null)
                {
                    // Check what it returns.
                    if (!_validCommandReturnTypes.Contains(methodInfo.ReturnType))
                    {
                        Debug.Fail(
                            $"Command {attribute.Name} has an invalid return type of {methodInfo.ReturnType.FullName}");
                        continue;
                    }

                    // Don't trust the developer to remember to set HasSideEffects. Sorry.
                    if (methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task))
                    {
                        attribute.HasSideEffects = true;
                    }

                    // Check if the parameters are right.
                    ParameterInfo[] methodParams = methodInfo.GetParameters();
                    if (methodParams.Length != 1)
                    {
                        Debug.Fail($"Command {attribute.Name} has an invalid parameter count of {methodParams.Length}.");
                        continue;
                    }

                    if (methodParams[0].ParameterType != typeof(IMessageContext))
                    {
                        Debug.Fail(
                            $"Command {attribute.Name} has an invalid parameter argument. You should only use MethodName(IMessageContext context).");
                        continue;
                    }

                    // Register command
                    _commandMap[attribute] = delegate(IMessageContext context)
                    {
                        // Force a local copy on the stack of the delegate.
                        // TODO: Does this go out of scope?
                        IPlugin localPlugin = plugin;
                        return methodInfo.Invoke(localPlugin, new object[] { context });
                    };

                }
            }
        }

        public async Task<object> ExecuteCommandAsync(string commandName, IMessageContext context)
        {
            CommandAttribute selectedCommand = _commandMap.Keys.OrderBy(x => x.Name.DamerauLevenshteinDistance(commandName, 3)).FirstOrDefault();
            if (!selectedCommand.Name.StartsWith(commandName))
            {
                // Not even close. Go away.
                return null;
            }

            return await Task.Run(async () =>
            {
                var result = _commandMap[selectedCommand](context);

                // Check if the command is actually a task, if so, start that bad boy up and return result.
                if (result is Task)
                {
                    await (Task) result;
                    return result.GetType().GetRuntimeProperty("Result")?.GetValue(result);
                }

                return result;
            });
        }

        public object ExecuteCommand(string commandName, IMessageContext context)
        {
            return ExecuteCommandAsync(commandName, context).Result;
        }

        public bool IsCommandNullOrVoid()
        {
            return false;
        }
    }
}
