using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Cheat_VIP
{
    public class MemoryManager
    {
        protected Process process;
        private IntPtr processHandle = IntPtr.Zero;
        private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        private Form parentForm;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        public MemoryManager(Form parent = null)
        {
            this.parentForm = parent;
        }

        private void ShowMessage(string message, string caption, MessageDialogIcon icon = MessageDialogIcon.Information)
        {
            if (parentForm != null)
            {
                Guna2MessageDialog dialog = new Guna2MessageDialog
                {
                    Buttons = MessageDialogButtons.OK,
                    Icon = icon,
                    Style = MessageDialogStyle.Dark,
                    Parent = parentForm
                };
                dialog.Show(message, caption);
            }
            else
            {
                MessageBox.Show(message, caption); // Fallback apenas se parentForm for null
            }
        }

        public bool AttachToProcess(string processName)
        {
            try
            {
                Process[] processos = Process.GetProcessesByName(processName);
                if (processos.Length > 0)
                {
                    process = processos[0];
                    processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);
                    if (processHandle == IntPtr.Zero)
                    {
                        ShowMessage("Falha ao abrir o processo. Erro: " + Marshal.GetLastWin32Error(), "Erro", MessageDialogIcon.Error);
                        return false;
                    }
                    return true;
                }
                else
                {
                    ShowMessage("Processo não encontrado: " + processName, "Erro", MessageDialogIcon.Error);
                    return false;
                }
            }
            catch (Win32Exception ex)
            {
                ShowMessage("Erro ao anexar ao processo: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao anexar ao processo: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return false;
            }
        }

        public void DetachProcess()
        {
            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
                process = null;
            }
        }

        public bool IsAttached()
        {
            return processHandle != IntPtr.Zero;
        }

        private bool CheckApiCall(bool success, string errorMessage)
        {
            if (!success)
            {
                ShowMessage(errorMessage + " Erro: " + Marshal.GetLastWin32Error(), "Erro", MessageDialogIcon.Error);
                return false;
            }
            return true;
        }

        public bool WriteMemory(IntPtr address, byte[] value)
        {
            try
            {
                if (!IsAttached())
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return false;
                }

                int bytesWritten;
                bool success = WriteProcessMemory(processHandle, address, value, value.Length, out bytesWritten);
                return CheckApiCall(success, "Falha ao escrever na memória.");
            }
            catch (Win32Exception ex)
            {
                ShowMessage("Erro ao escrever na memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao escrever na memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return false;
            }
        }

        public byte[] ReadMemory(IntPtr address, int size)
        {
            try
            {
                if (!IsAttached())
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return null;
                }

                byte[] buffer = new byte[size];
                int bytesRead;
                bool success = ReadProcessMemory(processHandle, address, buffer, size, out bytesRead);
                if (!CheckApiCall(success, "Falha ao ler da memória."))
                {
                    return null;
                }
                return buffer;
            }
            catch (Win32Exception ex)
            {
                ShowMessage("Erro ao ler da memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return null;
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao ler da memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return null;
            }
        }

        public IntPtr AllocateMemory(uint size)
        {
            try
            {
                if (!IsAttached())
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return IntPtr.Zero;
                }
                if (size == 0)
                {
                    ShowMessage("Tamanho da alocação não pode ser 0.", "Erro", MessageDialogIcon.Error);
                    return IntPtr.Zero;
                }

                IntPtr allocatedMemory = VirtualAllocEx(processHandle, IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (!CheckApiCall(allocatedMemory != IntPtr.Zero, "Falha ao alocar memória no processo."))
                {
                    return IntPtr.Zero;
                }
                return allocatedMemory;
            }
            catch (Win32Exception ex)
            {
                ShowMessage("Erro ao alocar memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao alocar memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return IntPtr.Zero;
            }
        }

        public int GetMainThreadId()
        {
            if (!IsAttached())
            {
                return 0;
            }

            if (process.Threads.Count > 0)
            {
                return process.Threads[0].Id;
            }
            else
            {
                ShowMessage("O processo não possui threads.", "Erro", MessageDialogIcon.Error);
                return 0;
            }
        }

        public bool FreeMemory(IntPtr address, uint size)
        {
            try
            {
                if (!IsAttached())
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return false;
                }

                bool success = VirtualFreeEx(processHandle, address, size, 0x8000); // MEM_RELEASE
                return CheckApiCall(success, "Falha ao liberar memória no processo.");
            }
            catch (Win32Exception ex)
            {
                ShowMessage("Erro ao liberar memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao liberar memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return false;
            }
        }

        public IntPtr GetProcessHandle()
        {
            return processHandle;
        }


        public IntPtr GetProcessBaseAddress()
        {
            return process != null ? process.MainModule.BaseAddress : IntPtr.Zero;
        }
    }
}