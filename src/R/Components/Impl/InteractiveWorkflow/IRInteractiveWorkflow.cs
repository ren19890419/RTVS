﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Common.Core.Shell;
using Microsoft.R.Components.ConnectionManager;
using Microsoft.R.Components.PackageManager;
using Microsoft.R.Components.Plots;
using Microsoft.R.Host.Client;

namespace Microsoft.R.Components.InteractiveWorkflow {
    public interface IRInteractiveWorkflow : IDisposable {
        ICoreShell Shell { get; }
        IConnectionManager Connections { get; }
        IRSessionProvider RSessions { get; }
        IRSession RSession { get; }
        IRPackageManager Packages { get; }
        IRPlotManager Plots { get; }
        IConsole Console { get; }
        IRInteractiveWorkflowOperations Operations { get; }
    }
}