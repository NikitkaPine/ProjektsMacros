using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;

namespace ProjektsMacros
{
    public partial class Form1 : Form
    {
        private List<string> macroRecording;

        private bool isRecording;
        private bool isPlaying;
        private string currentMacroFile;
        private IntPtr hookIDKeyboard;
        private IntPtr hookIDMouse;

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }


        private const int WH_KEYBOARD_GLOBAL = 2;
        private const int WH_MOUSE_GLOBAL = 7;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;

        private LowLevelKeyboardProc keyboardProc;
        private LowLevelMouseProc mouseProc;


        public Form1()
        {
            InitializeComponent();
            macroRecording = new List<string>();
            isRecording = false;
            isPlaying = false;
            currentMacroFile = "";

            hookIDKeyboard = IntPtr.Zero;
            hookIDMouse = IntPtr.Zero;

            keyboardProc = HookCallbackKeyboard;
            mouseProc = HookCallbackMouse;

            // Устанавливаем хуки на нажатия клавиш и кнопок мыши
            hookIDKeyboard = SetHook(WH_KEYBOARD_LL, keyboardProc);
            hookIDMouse = SetHook(WH_MOUSE_LL, mouseProc);
        }

        private IntPtr SetHook(int idHook, Delegate hookProc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                IntPtr moduleHandle = GetModuleHandle(currentModule.ModuleName);
                if (idHook == WH_KEYBOARD_LL)
                {
                    return SetWindowsHookEx(idHook, (LowLevelKeyboardProc)hookProc, moduleHandle, 0);
                }
                else if (idHook == WH_MOUSE_LL)
                {
                    return SetWindowsHookEx(idHook, (LowLevelMouseProc)hookProc, moduleHandle, 0);
                }
                else
                {
                    return IntPtr.Zero;
                }
            }
        }
        private IntPtr HookCallbackKeyboard(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (isRecording)
                {
                    string action = wParam == (IntPtr)WM_KEYDOWN ? $"KEY_PRESS,{((Keys)vkCode).ToString()}" : "";
                    macroRecording.Add(action);
                    Console.WriteLine(action); // Вывод в консоль
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr HookCallbackMouse(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_MOUSEMOVE || wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_LBUTTONUP || wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONUP))
            {
                MSLLHOOKSTRUCT mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                // Проверяем, что координаты мыши находятся в пределах формы
                if (this.Bounds.Contains(mouseHookStruct.pt.x, mouseHookStruct.pt.y))
                {
                    
                }
                if (isRecording)
                {
                    string action = "";
                    if (wParam == (IntPtr)0x0200)
                    {
                        action = $"MOUSE_MOVE,{mouseHookStruct.pt.x},{mouseHookStruct.pt.y}";
                    }
                    else if (wParam == (IntPtr)WM_LBUTTONDOWN)
                    {
                        action = "MOUSE_LEFT_CLICK";
                    }
                    else if (wParam == (IntPtr)WM_LBUTTONUP)
                    {
                        action = "";
                    }
                    else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                    {
                        action = "MOUSE_RIGHT_CLICK";
                    }
                    else if (wParam == (IntPtr)WM_RBUTTONUP)
                    {
                        action = "";
                    }
                    macroRecording.Add(action);
                    Console.WriteLine(action); // Вывод в консоль
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
        private void LogAction(string action)
        {
            // Создаем новый список для логгирования
            List<string> loggedActions = new List<string>();

            bool recordingMouseMovement = false;

            // Проходим по всем действиям в macroRecording
            foreach (string recordedAction in macroRecording)
            {
                string[] parts = recordedAction.Split(',');

                // Если это действие MOUSE_MOVE
                if (parts.Length > 0 && parts[0] == "MOUSE_MOVE")
                {
                    // Если мы не начали записывать движения мыши перед кликом или нажатием клавиши, добавляем это действие в логгер
                    if (!recordingMouseMovement)
                    {
                        loggedActions.Add(recordedAction);
                    }
                    else
                    {
                        // Если мы начали записывать движения мыши, пропускаем это действие
                        continue;
                    }
                }
                // Если это действие клика мыши или нажатие клавиши
                else if (parts.Length > 0 && (parts[0] == "MOUSE_LEFT_CLICK" || parts[0] == "MOUSE_RIGHT_CLICK" || parts[0] == "KEY_PRESS"))
                {
                    // Если мы до этого записывали движения мыши, значит наступил момент перед кликом или нажатием клавиши
                    // Поэтому добавляем последнее действие MOUSE_MOVE и текущее действие в логгер
                    if (recordingMouseMovement)
                    {
                        loggedActions.Add(macroRecording[macroRecording.IndexOf(recordedAction) - 1]); // Предыдущее действие MOUSE_MOVE
                        loggedActions.Add(recordedAction); // Текущее действие клика мыши или нажатия клавиши
                    }

                    // Сбрасываем флаг записи движений мыши
                    recordingMouseMovement = false;
                }
                else
                {
                    // Если это другое действие, добавляем его в логгер
                    loggedActions.Add(recordedAction);
                }

                // Если это действие MOUSE_MOVE и мы еще не начали записывать движения мыши, устанавливаем флаг записи
                if (parts.Length > 0 && parts[0] == "MOUSE_MOVE" && !recordingMouseMovement)
                {
                    recordingMouseMovement = true;
                }
            }

            // Очищаем логгер и выводим все действия из нового списка
            listBoxLogger.Items.Clear();
            foreach (string loggedAction in loggedActions)
            {
                listBoxLogger.Items.Add(loggedAction);
            }

            // Прокручиваем логгер вниз, чтобы новые записи были видны kuku
            //listBoxLogger.TopIndex = listBoxLogger.Items.Count - 1;
        }
        private void RecordButton_Click(object sender, EventArgs e)
        {
            if (!isRecording)
            {
                isRecording = true;
                macroRecording.Clear();
                listBoxLogger.Items.Clear();
                RecordButton.Text = "Stop Recording";
                MessageBox.Show("Recording started. Press Ctrl + Shift + R to stop recording.");
            }
            else
            {
                isRecording = false;
                RecordButton.Text = "Start Recording";
                SaveMacro(macroRecording);
                MessageBox.Show("Recording stopped. Macro saved.");
            }
        }

        private void LoadMacroButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Macro Files (*.macro)|*.macro";
            openFileDialog.DefaultExt = "macro";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                LoadMacro(filePath);
                PathTextBox.Text = filePath;
                MessageBox.Show("Macro loaded.");
            }
        }

        private void SaveMacro(List<string> actions)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Macro Files (*.macro)|*.macro";
            saveFileDialog.DefaultExt = "macro";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = saveFileDialog.FileName;

                // Пользователь вводит ключ шифрования
                string encryptionKey = GetInput("Enter encryption key:", "Encryption Key");

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Шифрование и запись в файл
                    foreach (string action in macroRecording)
                    {
                        string encryptedAction = EncryptString(action, encryptionKey);//kuku
                        writer.WriteLine(encryptedAction);
                    }
                }

                // Вывод ключа дешифрования
                MessageBox.Show($"Encryption key: {encryptionKey}");

                currentMacroFile = filePath;
                // Логгирование действий
                foreach (string action in actions)
                {
                    LogAction(action);
                }
                //kuku
            }
        }

        private void LoadMacro(string filePath)
        {
            if (File.Exists(filePath))
            {
                // Пользователь вводит ключ дешифрования
                string decryptionKey = GetInput("Enter decryption key:", "Decryption Key");

                macroRecording.Clear();
                listBoxLogger.Items.Clear();
                //kuku
                string[] commands = File.ReadAllLines(filePath);

                foreach (string encryptedAction in commands)
                {
                    // Дешифрование и добавление в список
                    string action = DecryptString(encryptedAction, decryptionKey);
                    macroRecording.Add(action);
                    LogAction(action);
                }

               
            }
            else
            {
                MessageBox.Show("Macro file not found.");
            }
        }
        private string EncryptString(string input, string key)
        {
            // Пример простейшего шифрования - XOR каждого символа с символом ключа
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                sb.Append((char)(input[i] ^ key[i % key.Length]));
            }
            return sb.ToString();
        }

        private string DecryptString(string input, string key)
        {
            // Дешифрование - применение XOR ещё раз
            return EncryptString(input, key);
        }
        private string GetInput(string prompt, string title)
        {
            Form inputForm = new Form();
            inputForm.Text = title;
            Label label = new Label();
            label.Text = prompt;
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            buttonOk.Text = "OK";
            buttonOk.DialogResult = DialogResult.OK;
            inputForm.Controls.Add(label);
            inputForm.Controls.Add(textBox);
            inputForm.Controls.Add(buttonOk);
            inputForm.AcceptButton = buttonOk;

            if (inputForm.ShowDialog() == DialogResult.OK)
            {
                return textBox.Text;
            }
            else
            {
                return null;
            }
        }
        public void PlayMacro()
        {
            foreach (string command in macroRecording)
            {
                string[] parts = command.Split(',');
                if (parts.Length > 0)
                {
                    string action = parts[0];
                    switch (action)
                    {
                        case "MOUSE_MOVE":
                            if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                            {
                                Cursor.Position = new System.Drawing.Point(x, y);
                            }
                            break;
                        case "MOUSE_LEFT_CLICK":
                            MouseEvent(MOUSEEVENTF_LEFTDOWN);
                            MouseEvent(MOUSEEVENTF_LEFTUP);
                            break;
                        case "MOUSE_RIGHT_CLICK":
                            MouseEvent(MOUSEEVENTF_RIGHTDOWN);
                            MouseEvent(MOUSEEVENTF_RIGHTUP);
                            break;
                        case "KEY_PRESS":
                            if (parts.Length == 2 && Enum.TryParse<Keys>(parts[1], out Keys key))
                            {
                                SendKeys.Send(key.ToString());
                            }
                            break;
                        default:
                            // Неизвестная команда, пропускаем
                            break;
                    }
                }
                Thread.Sleep(5);
            }
        }

        private const int INPUT_MOUSE = 0;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        //mouseevent
        private void MouseEvent(int dwFlags)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = new INPUT();
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi = new MOUSEINPUT();
            inputs[0].mi.dwFlags = dwFlags;

            inputs[1] = new INPUT();
            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi = new MOUSEINPUT();
            inputs[1].mi.dwFlags = dwFlags == MOUSEEVENTF_LEFTDOWN ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTDOWN;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            PlayMacro();
        }

        private void PathTextBox_TextChanged(object sender, EventArgs e)
        {

        }
        //kuku
    }
}