﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.VisualStudio.R.Package.Sql.Publish {
    /// <summary>
    /// Entry in the list of potential stored procedures.
    /// </summary>
    internal sealed class SProcInfo {
        /// <summary>
        /// File which content will go into the variable
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Relative to the project folder
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Stored procedure name
        /// </summary>
        public string SProcName { get; set; }
    }
}
