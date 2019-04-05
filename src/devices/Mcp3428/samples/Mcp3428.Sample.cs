﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Device.I2c;
using System.Device.I2c.Drivers;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Iot.Device.Mcp3428.Samples
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("Hello Mcp3428 Sample!");
            var options = new I2cConnectionSettings(1,
                Mcp3428.AddressFromPins(Mcp3428.PinState.Low, Mcp3428.PinState.Low));
            using (var dev = new UnixI2cDevice(options))
            {
                using (var adc = new Mcp3428(dev, Mcp3428.ModeEnum.OneShot, resolution: Mcp3428.ResolutionEnum.Bit16, pgaGain: Mcp3428.GainEnum.X1))
                {
                    var watch = new Stopwatch();
                    watch.Start();
                    while (true)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var last = watch.ElapsedMilliseconds;
                            var value = adc.ReadChannel(i);

                            foreach (var b in adc.LastBytes.ToArray())
                            {
                                Console.Write($"{b:X} ");
                            }
                            Console.WriteLine();
                            Console.WriteLine($"ADC Channel[{adc.LastChannel + 1}] read in {watch.ElapsedMilliseconds - last} ms, value: {value} V");
                            await Task.Delay(500);
                        }
                        Console.WriteLine($"mode {adc.Mode}, gain {adc.PGAGain}, res {adc.Resolution}");
                        await Task.Delay(1000);
                    }
                }
            }
        }
    }
}
