/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
* File: MonitorPoolsService.cs
* 
* Copyright (c) 2016 John Davis
*
* Permission is hereby granted, free of charge, to any person obtaining a
* copy of this software and associated documentation files (the "Software"),
* to deal in the Software without restriction, including without limitation
* the rights to use, copy, modify, merge, publish, distribute, sublicense,
* and/or sell copies of the Software, and to permit persons to whom the
* Software is furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included
* in all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
* OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
* THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
* FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
* IN THE SOFTWARE.
* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Timers;

namespace StorageSpacesMonitorService
{
    /// <summary>
    /// Represents the service used to monitor storage pools.
    /// </summary>
    internal class MonitorPoolsService : ServiceBase
    {
        #region Constants

#if I386
        /// <summary>
        /// The filename of the x86 version of InpOut.
        /// </summary>
        private const string InpOutFileName = "inpout32.dll";
#elif AMD64
        /// <summary>
        /// The filename of the x64 version of InpOut.
        /// </summary>
        private const string InpOutFileName = "inpoutx64.dll";
#endif

        #endregion

        #region Private variables

        private Timer timerMonitor;
        private Timer timerBeep;
        private ManagementObjectSearcher poolSearcher;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorPoolsService"/> class.
        /// </summary>
        public MonitorPoolsService()
        {
            // Initialize service.
            ServiceName = "MonitorPools";
        }

        #endregion

        #region Overridden methods

        /// <summary>
        /// Executes when a Start command is sent to the service by the Service Control Manager (SCM) or
        /// when the operating system starts (for a service that starts automatically). Specifies actions
        /// to take when the service starts.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
        protected override void OnStart(string[] args)
        {
            // Create management searchers.
            poolSearcher = new ManagementObjectSearcher(new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage"), new ObjectQuery("SELECT * FROM MSFT_StoragePool"));

            // Initialize monitor timer.
            timerMonitor = new Timer(5000);
            timerMonitor.Elapsed += (s, e) =>
            {
                // Variable for beep.
                var beep = false;

                // Get health of all storage pools on system.
                foreach (var pool in poolSearcher.Get())
                {
                    if ((ushort)pool["HealthStatus"] != 0)
                    {
                        beep = true;
                        break;
                    }
                }

                // Start or stop beep timer as needed.
                timerBeep.Enabled = beep;
            };

            // Initialize beep timer.
            timerBeep = new Timer(1000);
            timerBeep.Elapsed += (s, e) =>
            {
                // Beep for half-second.
                Beep(900, 500);
            };

            // Start monitor timer.
            timerMonitor.Start();
        }

        /// <summary>
        /// Executes when a Stop command is sent to the service by the Service Control Manager (SCM). Specifies
        /// actions to take when a service stops running.
        /// </summary>
        protected override void OnStop()
        {
            // Stop timers.
            timerMonitor.Stop();
            timerBeep.Stop();

            // Dispose of timers.
            timerMonitor.Dispose();
            timerBeep.Dispose();
        }

        #endregion

        #region Interop

        [DllImport(InpOutFileName)]
        private extern static void Out32(short PortAddress, short Data);

        [DllImport(InpOutFileName)]
        private extern static char Inp32(short PortAddress);

        private static void Beep(uint freq, int ms)
        {
            // Prep speaker.
            Out32(0x43, 0xB6);
            Out32(0x42, (byte)(freq & 0xFF));
            Out32(0x42, (byte)(freq >> 9));
            System.Threading.Thread.Sleep(10);

            // Start beep and wait duration.
            Out32(0x61, (byte)(Convert.ToByte(Inp32(0x61)) | 0x03));
            System.Threading.Thread.Sleep(ms);

            // Stop beep.
            Out32(0x61, (byte)(Convert.ToByte(Inp32(0x61)) & 0xFC));
        }

        #endregion
    }
}
