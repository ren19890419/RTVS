﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Common.Core;
using Microsoft.Common.Core.Logging;
using Microsoft.Common.Core.OS;
using Microsoft.Extensions.Logging;
using Microsoft.R.Host.Broker.Interpreters;
using Microsoft.R.Host.Broker.Pipes;
using Microsoft.R.Host.Broker.Security;
using Microsoft.R.Host.Broker.Services;
using Microsoft.R.Host.Protocol;
using static System.FormattableString;

namespace Microsoft.R.Host.Broker.Sessions {
    public class Session {
        private readonly IRHostProcessService _processService;
        private readonly IApplicationLifetime _applicationLifetime;
        private readonly bool _isInteractive;
        private readonly ILogger _sessionLogger;
        private readonly MessagePipe _pipe;
        private readonly ClaimsPrincipal _principal;
        private volatile IMessagePipeEnd _hostEnd;
        private IProcess _process;

        public SessionManager Manager { get; }

        public IIdentity User { get; }

        /// <remarks>
        /// Unique for a given <see cref="User"/> only.
        /// </remarks>
        public string Id { get; }

        public Interpreter Interpreter { get; }

        public string CommandLineArguments { get; }

        private int _state;

        public SessionState State {
            get => (SessionState)_state;
            set {
                var oldState = (SessionState)Interlocked.Exchange(ref _state, (int)value);
                if (oldState != value) {
                    StateChanged?.Invoke(this, new SessionStateChangedEventArgs(oldState, value));
                }
            }
        }

        public event EventHandler<SessionStateChangedEventArgs> StateChanged;

        public IProcess Process => _process;

        public SessionInfo Info => new SessionInfo {
            Id = Id,
            InterpreterId = Interpreter.Id,
            CommandLineArguments = CommandLineArguments,
            State = State,
        };

        internal Session(SessionManager manager
            , IRHostProcessService processService
            , IApplicationLifetime applicationLifetime
            , ILogger sessionLogger
            , ILogger messageLogger
            , ClaimsPrincipal principal
            , Interpreter interpreter
            , string id
            , string commandLineArguments
            , bool isInteractive) {
            _principal = principal;
            Manager = manager;
            Interpreter = interpreter;
            User = principal.Identity;
            Id = id;
            CommandLineArguments = commandLineArguments;
            _processService = processService;
            _applicationLifetime = applicationLifetime;
            _isInteractive = isInteractive;
            _sessionLogger = sessionLogger;

            _pipe = new MessagePipe(messageLogger);
        }

        public void StartHost(string logFolder, ILogger outputLogger, LogVerbosity verbosity) {
            if (_hostEnd != null) {
                throw new InvalidOperationException("Host process is already running");
            }

            string profilePath = _principal.FindFirst(Claims.RUserProfileDir)?.Value;
            var useridentity = User as WindowsIdentity;
            // In remote broker User Identity type is always WindowsIdentity
            string suppressUI = useridentity == null ? string.Empty : "--rhost-suppress-ui ";
            string isRepl = _isInteractive ? "--rhost-interactive " : string.Empty;
            string logFolderParam = string.IsNullOrEmpty(logFolder) ? string.Empty : Invariant($"--rhost-log-dir \"{logFolder}\"");
            string arguments = Invariant($"{suppressUI}{isRepl}--rhost-r-dir \"{Interpreter.BinPath}\" --rhost-name \"{Id}\" {logFolderParam} --rhost-log-verbosity {(int)verbosity} {CommandLineArguments}");

            _sessionLogger.LogInformation(Resources.Info_StartingRHost, Id, User.Name, arguments);
            _process = _processService.StartHost(Interpreter, profilePath, User.Name, _principal, arguments);

            _process.Exited += delegate {
                _hostEnd?.Dispose();
                _hostEnd = null;
                State = SessionState.Terminated;
                if (_process.ExitCode != 0) {
                    _sessionLogger.LogInformation(Resources.Error_ExitRHost, _process.ExitCode);
                }
            };

            _sessionLogger.LogInformation(Resources.Info_StartedRHost, Id, User.Name);

            var hostEnd = _pipe.ConnectHost(_process.Id);
            _hostEnd = hostEnd;

            ClientToHostWorker(_process.StandardInput, hostEnd).DoNotWait();
            HostToClientWorker(_process.StandardOutput, hostEnd).DoNotWait();

            // ToDo: Remove this after https://github.com/Microsoft/RTVS/issues/3626 
            HostToClientErrorWorker(_process.StandardError, _process.Id, 
                (processid, errdata) => outputLogger?.LogTrace(Resources.Trace_ErrorDataReceived, processid, errdata)).DoNotWait();
        }

        public void KillHost() {
            _sessionLogger.LogTrace("Killing host process for session '{0}'.", Id);

            try {
                _process?.Kill();
            } catch (Exception ex) {
                _sessionLogger.LogError(0, ex, "Failed to kill host process for session '{0}'.", Id);
                throw;
            }
        }

        public IMessagePipeEnd ConnectClient() {
            _sessionLogger.LogTrace("Connecting client to message pipe for session '{0}'.", Id);

            if (_pipe == null) {
                _sessionLogger.LogError("Session '{0}' already has a client pipe connected.", Id);
                throw new InvalidOperationException(Resources.Error_RHostFailedToStart.FormatInvariant(Id));
            }

            return _pipe.ConnectClient();
        }

        private async Task HostToClientErrorWorker(Stream stream, int processid, Action<int, string> opp) {
            using (StreamReader reader = new StreamReader(stream)) {
                while (true) {
                    try {
                        string data = await reader.ReadLineAsync();
                        if (data != null && data.Length > 0) {
                            opp?.Invoke(processid, data);
                        }
                    } catch (IOException) {
                        break;
                    }
                }
            }
        }

        private async Task ClientToHostWorker(Stream stream, IMessagePipeEnd pipe) {
            using (stream) {
                while (true) {
                    byte[] message;
                    try {
                        message = await pipe.ReadAsync(_applicationLifetime.ApplicationStopping);
                    } catch (PipeDisconnectedException) {
                        break;
                    }

                    var sizeBuf = BitConverter.GetBytes(message.Length);
                    try {
                        await stream.WriteAsync(sizeBuf, 0, sizeBuf.Length);
                        await stream.WriteAsync(message, 0, message.Length);
                        await stream.FlushAsync();
                    } catch (IOException) {
                        break;
                    }
                }
            }
        }

        private async Task HostToClientWorker(Stream stream, IMessagePipeEnd pipe) {
            var sizeBuf = new byte[sizeof(int)];
            while (true) {
                if (!await FillFromStreamAsync(stream, sizeBuf)) {
                    break;
                }
                int size = BitConverter.ToInt32(sizeBuf, 0);

                var message = new byte[size];
                if (!await FillFromStreamAsync(stream, message)) {
                    break;
                }

                pipe.Write(message);
            }
        }

        private static async Task<bool> FillFromStreamAsync(Stream stream, byte[] buffer) {
            for (int index = 0, count = buffer.Length; count != 0;) {
                int read = await stream.ReadAsync(buffer, index, count);
                if (read == 0) {
                    return false;
                }

                index += read;
                count -= read;
            }

            return true;
        }
    }
}