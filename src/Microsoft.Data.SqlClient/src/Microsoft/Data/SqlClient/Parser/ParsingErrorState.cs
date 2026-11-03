// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser;

internal enum ParsingErrorState
{
    Undefined = 0,
    FedAuthInfoLengthTooShortForCountOfInfoIds = 1,
    FedAuthInfoLengthTooShortForData = 2,
    FedAuthInfoFailedToReadCountOfInfoIds = 3,
    FedAuthInfoFailedToReadTokenStream = 4,
    FedAuthInfoInvalidOffset = 5,
    FedAuthInfoFailedToReadData = 6,
    FedAuthInfoDataNotUnicode = 7,
    FedAuthInfoDoesNotContainStsurlAndSpn = 8,
    FedAuthInfoNotReceived = 9,
    FedAuthNotAcknowledged = 10,
    FedAuthFeatureAckContainsExtraData = 11,
    FedAuthFeatureAckUnknownLibraryType = 12,
    UnrequestedFeatureAckReceived = 13,
    UnknownFeatureAck = 14,
    InvalidTdsTokenReceived = 15,
    SessionStateLengthTooShort = 16,
    SessionStateInvalidStatus = 17,
    CorruptedTdsStream = 18,
    ProcessSniPacketFailed = 19,
    FedAuthRequiredPreLoginResponseInvalidValue = 20,
    TceUnknownVersion = 21,
    TceInvalidVersion = 22,
    TceInvalidOrdinalIntoCipherInfoTable = 23,
    DataClassificationInvalidVersion = 24,
    DataClassificationNotExpected = 25,
    DataClassificationInvalidLabelIndex = 26,
    DataClassificationInvalidInformationTypeIndex = 27
}
