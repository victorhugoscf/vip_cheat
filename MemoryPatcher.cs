using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Cheat_VIP
{
    public class MemoryPatcher
    {
        private readonly MemoryManager memoryManager;
        private static readonly Dictionary<string, byte> RegisterOpcodes = new Dictionary<string, byte>
        {
            { "eax", 0xB8 }, { "ecx", 0xB9 }, { "edx", 0xBA }, { "ebx", 0xBB }, { "esi", 0xBE }, { "edi", 0xBF }
        };
        private Dictionary<IntPtr, Tuple<byte[], string, int>> hooks = new Dictionary<IntPtr, Tuple<byte[], string, int>>();
        private Form parentForm;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        public const uint MEM_COMMIT = 0x00001000;
        public const uint MEM_RESERVE = 0x00002000;
        public const uint MEM_RELEASE = 0x00008000;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;
        public const uint PAGE_READWRITE = 0x04;

        public MemoryPatcher(MemoryManager memoryManager, Form parent = null)
        {
            this.memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
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

        public bool WriteMemory(IntPtr address, byte[] value)
        {
            try
            {
                if (memoryManager.IsAttached())
                {
                    return memoryManager.WriteMemory(address, value);
                }
                else
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao escrever na memória: " + ex.Message, "Erro", MessageDialogIcon.Error);
                return false;
            }
        }

        public void HookInstruction(IntPtr address, int incrementValue, string register, IntPtr returnAddress, int originalInstructionLength)
        {
            try
            {
                if (!memoryManager.IsAttached())
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                if (!RegisterOpcodes.ContainsKey(register.ToLower()))
                {
                    ShowMessage("Registrador inválido.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                if (originalCode == null || originalCode.Length != originalInstructionLength)
                {
                    ShowMessage("Falha ao ler o código original.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                IntPtr hProcess = memoryManager.GetProcessHandle();

                IntPtr allocatedReturnAddress = VirtualAllocEx(hProcess, IntPtr.Zero, 4, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (allocatedReturnAddress == IntPtr.Zero)
                {
                    ShowMessage("Falha ao alocar memória para o endereço de retorno.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                if (!memoryManager.WriteMemory(allocatedReturnAddress, BitConverter.GetBytes(returnAddress.ToInt32())))
                {
                    ShowMessage("Falha ao escrever o endereço de retorno na memória alocada.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, allocatedReturnAddress, 0, MEM_RELEASE);
                    return;
                }

                IntPtr injectedCodeAddress = VirtualAllocEx(hProcess, IntPtr.Zero, 1024, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (injectedCodeAddress == IntPtr.Zero)
                {
                    ShowMessage("Falha ao alocar memória para o código injetado.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, allocatedReturnAddress, 0, MEM_RELEASE);
                    return;
                }

                byte[] injectedCode = GenerateHookCode(register, incrementValue, allocatedReturnAddress);
                if (injectedCode == null || injectedCode.Length == 0)
                {
                    ShowMessage("Falha ao gerar o código injetado.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, injectedCodeAddress, 0, MEM_RELEASE);
                    VirtualFreeEx(hProcess, allocatedReturnAddress, 0, MEM_RELEASE);
                    return;
                }

                if (!memoryManager.WriteMemory(injectedCodeAddress, injectedCode))
                {
                    ShowMessage("Falha ao escrever o código injetado na memória.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, injectedCodeAddress, 0, MEM_RELEASE);
                    VirtualFreeEx(hProcess, allocatedReturnAddress, 0, MEM_RELEASE);
                    return;
                }

                byte[] jumpBytes = CreateJump(address, injectedCodeAddress, originalInstructionLength);
                if (!memoryManager.WriteMemory(address, jumpBytes))
                {
                    ShowMessage("Falha ao escrever o jump na instrução original.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, injectedCodeAddress, 0, MEM_RELEASE);
                    VirtualFreeEx(hProcess, allocatedReturnAddress, 0, MEM_RELEASE);
                    return;
                }

                hooks[address] = Tuple.Create(originalCode, register, incrementValue);
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao criar a modificação: " + ex.Message, "Erro", MessageDialogIcon.Error);
            }
        }

        private byte[] GenerateHookCode(string register, int incrementValue, IntPtr allocatedReturnAddress)
        {
            List<byte> code = new List<byte>();

            switch (register.ToLower())
            {
                case "eax":
                    code.Add(0x05); // add eax, incrementValue
                    break;
                case "ecx":
                    code.Add(0x81);
                    code.Add(0xC1); // add ecx, incrementValue
                    break;
                case "edx":
                    code.Add(0x81);
                    code.Add(0xC2); // add edx, incrementValue
                    break;
                case "ebx":
                    code.Add(0x81);
                    code.Add(0xC3); // add ebx, incrementValue
                    break;
                case "esi":
                    code.Add(0x81);
                    code.Add(0xC6); // add esi, incrementValue
                    break;
                case "edi":
                    code.Add(0x81);
                    code.Add(0xC7); // add edi, incrementValue
                    break;
                default:
                    throw new ArgumentException("Registrador inválido.");
            }

            code.AddRange(BitConverter.GetBytes(incrementValue));

            code.Add(0xFF);
            code.Add(0x25);
            code.AddRange(BitConverter.GetBytes(allocatedReturnAddress.ToInt32()));

            return code.ToArray();
        }

        private byte[] CreateJump(IntPtr fromAddress, IntPtr toAddress, int originalInstructionLength)
        {
            byte[] jumpBytes = new byte[originalInstructionLength];
            jumpBytes[0] = 0xE9;
            int relativeAddress = toAddress.ToInt32() - (fromAddress.ToInt32() + 5);
            byte[] relativeBytes = BitConverter.GetBytes(relativeAddress);
            Array.Copy(relativeBytes, 0, jumpBytes, 1, 4);

            for (int i = 5; i < originalInstructionLength; i++)
            {
                jumpBytes[i] = 0x90; // NOP
            }

            return jumpBytes;
        }

        public void UnhookInstruction(IntPtr address)
        {
            try
            {
                if (!memoryManager.IsAttached())
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                if (!hooks.ContainsKey(address))
                {
                    ShowMessage("Nenhum modificação encontrado neste endereço.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                var hookData = hooks[address];
                byte[] originalCode = hookData.Item1;

                if (!WriteMemory(address, originalCode))
                {
                    ShowMessage("Falha ao restaurar o código original.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                hooks.Remove(address);
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao remover a modificação no endereço: " + ex.Message, "Erro", MessageDialogIcon.Error);
            }
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        public static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll")]
        public static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll")]
        public static extern IntPtr SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        public static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CloseHandle(IntPtr hObject);

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            QUERY_INFORMATION = (0x0040),
            THREAD_ALL_ACCESS = (0x1F03FF)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONTEXT
        {
            public uint ContextFlags;
            public uint Dr0;
            public uint Dr1;
            public uint Dr2;
            public uint Dr3;
            public uint Dr6;
            public uint Dr7;
            public FLOATING_SAVE_AREA FloatSave;
            public uint SegGs;
            public uint SegFs;
            public uint SegEs;
            public uint SegDs;
            public uint Eax;
            public uint Ecx;
            public uint Edx;
            public uint Ebx;
            public uint Esp;
            public uint Ebp;
            public uint Esi;
            public uint Edi;
            public uint Eip;
            public uint SegCs;
            public uint EFlags;
            public uint SegSs;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] DebugControl;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] LastBranchToRip;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] LastBranchFromRip;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] LastExceptionToRip;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] LastExceptionFromRip;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FLOATING_SAVE_AREA
        {
            public uint ControlWord;
            public uint StatusWord;
            public uint TagWord;
            public uint ErrorOffset;
            public uint ErrorSelector;
            public uint DataOffset;
            public uint DataSelector;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
            public byte[] RegisterArea;
            public uint Cr0NpxState;
        }

        [Flags]
        public enum CONTEXT_FLAGS : uint
        {
            CONTEXT_CONTROL = 0x0001,
            CONTEXT_INTEGER = 0x0002,
            CONTEXT_SEGMENTS = 0x0004,
            CONTEXT_FLOATING_POINT = 0x0008,
            CONTEXT_DEBUG_REGISTERS = 0x0010,

            CONTEXT_FULL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS,
            CONTEXT_ALL = CONTEXT_FULL | CONTEXT_FLOATING_POINT | CONTEXT_DEBUG_REGISTERS
        }
    }
}