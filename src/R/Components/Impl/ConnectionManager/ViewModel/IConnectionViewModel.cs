// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading;

namespace Microsoft.R.Components.ConnectionManager.ViewModel {
    public interface IConnectionViewModel : IConnectionInfo, INotifyPropertyChanged {
        bool IsActive { get; set; }
        bool IsEditing { get; set; }
        bool IsConnected { get; set; }
        bool IsRunning { get; set; }
        CancellationTokenSource TestingConnectionCts { get; set; }
        bool IsTestConnectionSucceeded { get; set; }
        string TestConnectionFailedText { get; set; }

        string OriginalName { get; }
        string SaveButtonTooltip { get; }
        bool HasChanges { get; }
        bool IsValid { get; }
        bool IsRenamed { get; }
        bool IsRemote { get; }
        
        void Reset();
        string ConnectionTooltip { get; }
        
        /// <summary>
        /// Update the path with a default scheme and port, if possible.
        /// </summary>
        void UpdatePath();
    }
}