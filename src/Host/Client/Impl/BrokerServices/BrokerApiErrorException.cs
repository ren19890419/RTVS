﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.R.Host.Protocol;

namespace Microsoft.R.Host.Client.BrokerServices {
    public sealed class BrokerApiErrorException: Exception {
        public BrokerApiError ApiError { get; }
        public string BrokerMessage { get; }

        public BrokerApiErrorException(BrokerApiError error, string brokerMessage) {
            ApiError = error;
            BrokerMessage = brokerMessage;
        }
    }
}
