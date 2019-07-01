﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Iot.Device.Mcp25xxx.Register;
using Iot.Device.Mcp25xxx.Register.ErrorDetection;
using Xunit;

namespace Iot.Device.Mcp25xxx.Tests.Register.ErrorDetection
{
    public class TecTests
    {
        [Fact]
        public void Get_Address()
        {
            Assert.Equal(Address.Tec, new Tec(0b0000_0000).Address);
        }

        [Theory]
        [InlineData(0b0000_0000)]
        [InlineData(0b1111_1111)]
        public void From_To_Byte(byte transmitErrorCount)
        {
            var tec = new Tec(transmitErrorCount);
            Assert.Equal(transmitErrorCount, tec.TransmitErrorCount);

            Assert.Equal(transmitErrorCount, new Tec(transmitErrorCount).ToByte());
        }
    }
}
