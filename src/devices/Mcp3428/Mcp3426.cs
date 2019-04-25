﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Device.I2c;

namespace Iot.Device.Mcp3428
{
    public class Mcp3426 : Mcp3428
    {
        /// <summary>
        /// The number of channels
        /// </summary>
        /// <autogeneratedoc />
        private const int NumChannels = 2;

        public const int I2CAddress = 0x68;

        /// <inheritdoc />
        public Mcp3426(I2cDevice i2CDevice) : base(i2CDevice, NumChannels)
        {
        }

        /// <inheritdoc />
        public Mcp3426(I2cDevice i2CDevice, ModeEnum mode = ModeEnum.Continuous, ResolutionEnum resolution = ResolutionEnum.Bit12, GainEnum pgaGain = GainEnum.X1) : this(i2CDevice)
        {
            SetConfig(0, mode: mode, resolution: resolution, pgaGain: pgaGain);
        }

#pragma warning disable RCS1163 // Unused parameter.
#pragma warning disable IDE0060 // Remove unused parameter
        public new static int I2CAddressFromPins(PinState _ = PinState.Low, PinState _NA = PinState.Low) { return I2CAddress; }
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore RCS1163 // Unused parameter.
    }
}
