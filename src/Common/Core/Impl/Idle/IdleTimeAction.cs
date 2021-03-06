﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Common.Core.Shell;

namespace Microsoft.Common.Core.Idle {
    /// <summary>
    /// Action that should be executed on next idle after certain amount of milliseconds
    /// </summary>
    public class IdleTimeAction {
        static readonly Dictionary<object, IdleTimeAction> _idleActions = new Dictionary<object, IdleTimeAction>();

        private readonly Action _action;
        private readonly int _delay;
        private readonly IIdleTimeService _idleTime;
        private readonly object _tag;
        private bool _connectedToIdle;
        private DateTime _idleConnectTime;

        /// <summary>
        /// Create delayed idle time action
        /// </summary>
        /// <param name="action">Action to execute on idle</param>
        /// <param name="delay">Minimum number of milliseconds to wait before executing the action</param>
        /// <param name="tag">Object that uniquely identifies the action. Typically creator object.</param>
        /// <param name="idleTime">Idle time service</param>
        public static void Create(Action action, int delay, object tag, IIdleTimeService idleTime) {
            if (!_idleActions.TryGetValue(tag, out IdleTimeAction existingAction)) {
                existingAction = new IdleTimeAction(action, delay, tag, idleTime);
                _idleActions[tag] = existingAction;
            }
        }

        /// <summary>
        /// Cancels idle time action. Has no effect if action has already been executed.
        /// </summary>
        /// <param name="tag">Tag identifying the action to cancel</param>
        public static void Cancel(object tag) {
            if (_idleActions.TryGetValue(tag, out IdleTimeAction idleTimeAction)) {
                idleTimeAction.DisconnectFromIdle();
                _idleActions.Remove(tag);
            }
        }

        private IdleTimeAction(Action action, int delay, object tag, IIdleTimeService idleTime) {
            _action = action;
            _delay = delay;
            _tag = tag;
            _idleTime = idleTime;

            ConnectToIdle();
        }

        void OnIdle(object sender, EventArgs e) {
            if (_idleConnectTime.MillisecondsSinceUtc() > _delay) {
                DisconnectFromIdle();
                _action();

                _idleActions.Remove(_tag);
            }
        }

        void ConnectToIdle() {
            if (!_connectedToIdle) {
                _idleTime.Idle += OnIdle;

                _idleConnectTime = DateTime.UtcNow;
                _connectedToIdle = true;
            }
        }

        void DisconnectFromIdle() {
            if (_connectedToIdle) {
                _idleTime.Idle -= OnIdle;
                _connectedToIdle = false;
            }
        }
    }
}
