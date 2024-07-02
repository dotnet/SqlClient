// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using Microsoft.Data.SqlClientX.Handlers.Connection.TransportCreationSubHandlers;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers.TransportCreation
{
    public class IpAddressVersionComparerTests
    {
        private static readonly IPAddress Ipv4 = IPAddress.Parse("127.0.0.1");
        private static readonly IPAddress Ipv6 = IPAddress.Parse("::1");
        
        public static TheoryData<IPAddress, IPAddress, int> InstanceV4_Compare_TestCases
        {
            get => new TheoryData<IPAddress, IPAddress, int>
            {
                { Ipv4, Ipv4, 0 },
                { Ipv4, Ipv6, 1 },
                { Ipv6, Ipv4, -1 },
                { Ipv6, Ipv6, 0 },
            };
        }
        
        [Theory]
        [MemberData(nameof(InstanceV4_Compare_TestCases))]
        public void InstanceV4_Compare(IPAddress x, IPAddress y, int expectedResult)
        {
            // Act
            var result = IpAddressVersionComparer.InstanceV4.Compare(x, y);
            
            // Assert
            Assert.Equal(expectedResult, result);
        }

        public static TheoryData<IPAddress, IPAddress, int> InstanceV6_Compare_TestCases
        {
            get => new TheoryData<IPAddress, IPAddress, int>
            {
                { Ipv4, Ipv4, 0 },
                { Ipv4, Ipv6, -1 },
                { Ipv6, Ipv4, 1 },
                { Ipv6, Ipv6, 0 },
            };
        }

        [Theory]
        [MemberData(nameof(InstanceV6_Compare_TestCases))]
        public void InstanceV6_Compare(IPAddress x, IPAddress y, int expectedResult)
        {
            // Act
            var result = IpAddressVersionComparer.InstanceV6.Compare(x, y);
            
            // Assert
            Assert.Equal(expectedResult, result);
        }
    }
}
