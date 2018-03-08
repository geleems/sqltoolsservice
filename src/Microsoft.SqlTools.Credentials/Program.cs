//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.SqlTools.Hosting;
using Microsoft.SqlTools.Hosting.Utility;

namespace Microsoft.SqlTools.Credentials
{
    /// <summary>
    /// Main application class for Credentials Service Host executable
    /// </summary>
    internal class Program
    {
        private const string ServiceName = "MicrosoftSqlToolsCredentials.exe";

        /// <summary>
        /// Main entry point into the Credentials Service Host
        /// </summary>
        internal static void Main(string[] args)
        {
            try
            {
                // read command-line arguments
//                CommandOptions commandOptions = new CommandOptions(args, ServiceName);
//                if (commandOptions.ShouldExit)
//                {
//                    return;
//                }

//                string logFilePath = "credentials";
//                if (!string.IsNullOrWhiteSpace(commandOptions.LoggingDirectory))
//                {
//                    logFilePath = Path.Combine(commandOptions.LoggingDirectory, logFilePath);
//                }

                // turn on Verbose logging during early development
                // we need to switch to Normal when preparing for public preview
                Logger.Initialize(logFilePath: "credentials", minimumLogLevel: LogLevel.Verbose, isEnabled: true);
                Logger.Write(LogLevel.Normal, "Starting SqlTools Credentials Provider");

                string directory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
                string[] assemblies = {"Microsoft.SqlTools.Credentials.dll"};
                ExtensibleServiceHost serviceHost = ExtensibleServiceHost.CreateDefaultExtensibleServer(directory, assemblies);
                serviceHost.Start();
                serviceHost.WaitForExit();
            }
            catch (Exception e)
            {
                Logger.Write(LogLevel.Error, string.Format("An unhandled exception occurred: {0}", e));
                Environment.Exit(1);               
            }
        }
    }
}
