﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Device.Gpio;
using System.Device.I2c;
using System.Device.I2c.Drivers;
using System.Device.Spi;
using System.Device.Spi.Drivers;
using System.IO;
using System.Threading;

namespace Iot.Device.Mcp3428
{
    public class Mcp3428 : IDisposable
    {
        private const int MaxRetries = 50;
        private readonly I2cDevice _i2cDevice;
        private readonly byte[] _readBuffer = new byte[3];

        private double _lastValue;
        private byte _lastConfigByte => _readBuffer[2];
        private bool _isReadyBit = false;

        //Config params
        private GainEnum _pgaGain = GainEnum.X1;
        private ResolutionEnum _resolution = ResolutionEnum.Bit12;
        private ModeEnum _mode = ModeEnum.Continuous;
        private byte _lastChannel = 0xFF;

        private int WaitTime() => (int)(1000.0 / UpdateFrequency(_resolution));

        public ResolutionEnum Resolution
        {
            get => _resolution;
            set
            {
                WriteConfig(SetResolutionBits(_lastConfigByte, value));
                _resolution = value;
            }
        }
        public byte LastChannel => _lastChannel;

        public GainEnum PGAGain
        {
            get => _pgaGain;
            set
            {
                WriteConfig(SetGainBits(_lastConfigByte, value));
                _pgaGain = value;
            }
        }

        public ModeEnum Mode
        {
            get => _mode;
            set
            {
                WriteConfig(SetModeBit(_lastConfigByte, value));
                _mode = value;
            }
        }

        public Mcp3428(I2cDevice i2CDevice)
        {
            _i2cDevice = i2CDevice;
            ReadValue(); // Don't like this in constructor, makes sure props are valid
        }

        public Mcp3428(I2cDevice i2CDevice, ModeEnum mode = ModeEnum.Continuous,
            ResolutionEnum resolution = ResolutionEnum.Bit12, GainEnum pgaGain = GainEnum.X1) : this(i2CDevice)
        {
            _resolution = resolution;
            _mode = mode;
            _pgaGain = pgaGain;
            SetConfig(0, resolution: _resolution, mode: _mode, pgaGain: _pgaGain);
        }

        public void Dispose()
        {
        }

        internal ReadOnlyMemory<byte> LastBytes => _readBuffer;

        public double ReadChannel(int channel) { return ReadValue(channel); }

        private double ReadValue(int channel = -1)
        {
            if (Mode == ModeEnum.OneShot)
            {
                OneShotRead(channel);
            }
            else
            {
                if (channel > 0 && channel != LastChannel)
                {
                    var conf = SetChannelBits(_lastConfigByte, channel);
                    WriteConfig(conf);
                }
                _i2cDevice.Read(_readBuffer);
                ReadConfigByte(_lastConfigByte);
            }

            //var value = BinaryPrimitives.ReadInt16LittleEndian(_readBuffer.AsSpan().Slice(1, 2));
            var value = BinaryPrimitives.ReadInt16BigEndian(_readBuffer.AsSpan().Slice(0, 2));
            _lastValue = value * LSBValue(Resolution);
            return _lastValue;
        }

        private void OneShotRead(int channel = -1)
        {
            if (Mode != ModeEnum.OneShot)
                throw new IOException("Device is not in One-Shot mode");
            var tries = 0;
            _isReadyBit = false;
            var conf = SetReadyBit(_lastConfigByte, false);
            if (channel >= 0 && channel != LastChannel)
            {
                conf = SetChannelBits(conf, channel);
            }

            var waittime = WaitTime();
            WriteConfig(conf);
            while (!_isReadyBit && tries < MaxRetries)
            {
                _i2cDevice.Read(_readBuffer);
                ReadConfigByte(_lastConfigByte);
                tries++;
                if (!_isReadyBit)
                    Thread.Sleep(waittime); //TODO Get rid of Thread.Sleep
            }

            if (!_isReadyBit)
            {
                throw new IOException($"ADC Conversion was not ready after {tries} attempts.");
            }
        }

        private void ReadConfigByte(byte config)
        {
            _isReadyBit = (config & Masks.ReadyMask) == 0; // Negated bit
            _lastChannel = (byte)((config & Masks.ChannelMask) >> 5);
            _mode = (ModeEnum)(config & Masks.ModeMask);
            _pgaGain = (GainEnum)(config & Masks.GainMask);
            _resolution = (ResolutionEnum)(config & Masks.ResolutionMask);
        }

        public enum ResolutionEnum : byte { Bit12 = 0, Bit14 = 4, Bit16 = 8 } // From datasheet 5.2
        public enum GainEnum : byte { X1 = 0, X2 = 1, X4 = 2, X8 = 3 }
        public enum ModeEnum : byte { OneShot = 0, Continuous = 16 }
        public enum PinState { Low, High, Floating }

        /// <summary>
        /// Address from pin configuration. Based on documentation TABLE 5-3-
        /// </summary>
        /// <param name="Adr1">The adr1.</param>
        /// <param name="Adr0">The adr0.</param>
        /// <returns>System.Byte.</returns>
        /// <exception cref="ArgumentException">Invalid combination</exception>
        /// <autogeneratedoc />
        public static byte AddressFromPins(PinState Adr1, PinState Adr0)
        {
            byte addr = 0b1101000; // Base value from doc

            switch (new ValueTuple<PinState, PinState>(Adr0, Adr1))
            { //TODO Remove C# 8 dependency for pull request
                case (PinState.Low, PinState.Low):
                case (PinState.Floating, PinState.Floating):
                    break;
                case (PinState.Low, PinState.Floating):
                    addr += 1;
                    break;
                case (PinState.Low, PinState.High):
                    addr += 2;
                    break;
                case (PinState.High, PinState.Low):
                    addr += 4;
                    break;
                case (PinState.High, PinState.Floating):
                    addr += 5;
                    break;
                case (PinState.High, PinState.High):
                    addr += 6;
                    break;
                case (PinState.Floating, PinState.Low):
                    addr += 3;
                    break;
                case (PinState.Floating, PinState.High):
                    addr += 7;
                    break;
                default:
                    throw new ArgumentException("Invalid combination");
            }

            return addr;
        }

        private bool SetConfig(int channel = 0, ModeEnum mode = ModeEnum.Continuous,
            ResolutionEnum resolution = ResolutionEnum.Bit12, GainEnum pgaGain = GainEnum.X1)
        {
            byte conf = 0;
            var ok = true;
            conf = SetModeBit(conf, mode);
            conf = SetChannelBits(conf, channel);
            conf = SetGainBits(conf, pgaGain);
            conf = SetResolutionBits(conf, resolution);
            conf = SetReadyBit(conf, false);
            _i2cDevice.WriteByte(conf);
            for (int i = 0; i < 1000; i++)
            {
                ;
            }
            _i2cDevice.Read(_readBuffer);
            ReadConfigByte(_lastConfigByte);
            Console.WriteLine($"Sent config byte {conf:X}, received {_lastConfigByte:X}");
            if (_lastChannel != channel)
            {
                Console.WriteLine($"Channel update failed from {_lastChannel} to {channel}");
                ok = false;
            }
            if (Resolution != resolution)
            {
                Console.WriteLine($"Resolution update failed from {Resolution} to {resolution}");
                ok = false;
            }
            if (mode != Mode)
            {
                Console.WriteLine($"Mode update failed from {Mode} to {mode}");
                ok = false;
            }
            if (PGAGain != pgaGain)
            {
                Console.WriteLine($"PGAGain update failed from {PGAGain} to {pgaGain}");
                ok = false;
            }

            return ok;
        }

        private void WriteConfig(byte configByte)
        {
            _i2cDevice.WriteByte(configByte);
            _i2cDevice.Read(_readBuffer);
            ReadConfigByte(_lastConfigByte);
        }

        private static byte SetResolutionBits(byte configByte, ResolutionEnum resolution)
        {
            return (byte)((configByte & ~Masks.ResolutionMask) | (byte)resolution);
        }

        private static byte SetReadyBit(byte configByte, bool ready)
        {
            return (byte)(ready ? configByte & ~Masks.ReadyMask : configByte | Masks.ReadyMask);
        }

        private static byte SetModeBit(byte configByte, ModeEnum mode)
        {
            return (byte)((configByte & ~Masks.ModeMask) | (byte)mode);
        }

        private static byte SetGainBits(byte configByte, GainEnum gain)
        {
            return (byte)((configByte & ~Masks.GainMask) | (byte)gain);
        }

        private static byte SetChannelBits(byte configByte, int channel)
        {
            if (channel > 3 || channel < 0)
                throw new ArgumentException("Channel numbers are only valid 0 to 3", nameof(channel));
            return (byte)((configByte & ~Masks.ChannelMask) | ((byte)channel << 5));
        }

        private static double LSBValue(ResolutionEnum res)
        {
            switch (res)
            {
                case ResolutionEnum.Bit12:
                    return 1e-3;
                case ResolutionEnum.Bit14:
                    return 250e-6;
                case ResolutionEnum.Bit16:
                    return 62.5e-6;
                default:
                    throw new ArgumentOutOfRangeException(nameof(res), res, null);
            }
        }

        private static int UpdateFrequency(ResolutionEnum res)
        {
            switch (res)
            {
                case ResolutionEnum.Bit12:
                    return 240;
                case ResolutionEnum.Bit14:
                    return 60;
                case ResolutionEnum.Bit16:
                    return 15;
                default:
                    throw new ArgumentOutOfRangeException(nameof(res), res, null);
            }
        }

        // From datasheet 5.2
        private static class Masks
        {
            public const byte GainMask = 0b00000011;
            public const byte ResolutionMask = 0b00001100;
            public const byte ModeMask = 0b00010000;
            public const byte ChannelMask = 0b01100000;
            public const byte ReadyMask = 0b10000000;
        }
    }

}
