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
        private Dictionary<IntPtr, Tuple<byte[], string, int, int, string>> hooks = new Dictionary<IntPtr, Tuple<byte[], string, int, int, string>>(); // Adiciona baseRegister
        private Form parentForm;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        public const uint MEM_COMMIT = 0x00001000;
        public const uint MEM_RESERVE = 0x00002000;
        public const uint MEM_RELEASE = 0x00008000;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;

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
                MessageBox.Show(message, caption);
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

        public void HookInstruction(IntPtr address, string sourceRegister, int offset, int newValue, int originalInstructionLength, string baseRegister)
        {
            try
            {
                if (!memoryManager.IsAttached())
                {
                    ShowMessage("Processo não anexado.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                string[] validRegisters = { "eax", "ecx", "edx", "ebx", "esi", "edi" };
                if (!Array.Exists(validRegisters, r => r == sourceRegister.ToLower()) || !Array.Exists(validRegisters, r => r == baseRegister.ToLower()))
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
                IntPtr injectedCodeAddress = VirtualAllocEx(hProcess, IntPtr.Zero, 1024, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (injectedCodeAddress == IntPtr.Zero)
                {
                    ShowMessage("Falha ao alocar memória para o código injetado.", "Erro", MessageDialogIcon.Error);
                    return;
                }

                byte[] injectedCode = GenerateHookCode(sourceRegister, offset, newValue, baseRegister);
                if (injectedCode == null || injectedCode.Length == 0)
                {
                    ShowMessage("Falha ao gerar o código injetado.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, injectedCodeAddress, 0, MEM_RELEASE);
                    return;
                }

                if (!memoryManager.WriteMemory(injectedCodeAddress, injectedCode))
                {
                    ShowMessage("Falha ao escrever o código injetado na memória.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, injectedCodeAddress, 0, MEM_RELEASE);
                    return;
                }

                byte[] callBytes = CreateJump(address, injectedCodeAddress, originalInstructionLength);
                if (!memoryManager.WriteMemory(address, callBytes))
                {
                    ShowMessage("Falha ao escrever o call na instrução original.", "Erro", MessageDialogIcon.Error);
                    VirtualFreeEx(hProcess, injectedCodeAddress, 0, MEM_RELEASE);
                    return;
                }

                hooks[address] = Tuple.Create(originalCode, sourceRegister, offset, newValue, baseRegister);
            }
            catch (Exception ex)
            {
                ShowMessage("Erro ao criar a modificação: " + ex.Message, "Erro", MessageDialogIcon.Error);
            }
        }

        private byte[] GenerateHookCode(string sourceRegister, int offset, int newValue, string baseRegister)
        {
            List<byte> code = new List<byte>();
            byte pushOp, popOp, modRm;

            // Define os opcodes de push/pop e ModR/M com base no registrador base
            switch (baseRegister.ToLower())
            {
                case "eax":
                    pushOp = 0x50; popOp = 0x58; modRm = 0x80; break;
                case "ecx":
                    pushOp = 0x51; popOp = 0x59; modRm = 0x81; break;
                case "edx":
                    pushOp = 0x52; popOp = 0x5A; modRm = 0x82; break;
                case "ebx":
                    pushOp = 0x53; popOp = 0x5B; modRm = 0x83; break;
                case "esi":
                    pushOp = 0x56; popOp = 0x5E; modRm = 0x86; break;
                case "edi":
                    pushOp = 0x57; popOp = 0x5F; modRm = 0x87; break;
                default:
                    throw new ArgumentException("Registrador base inválido.");
            }

            // Preserva o registrador base
            code.Add(pushOp); // push <baseRegister>

            // Escreve o novo valor no endereço [baseRegister + offset]
            code.Add(0xC7); // mov dword ptr [baseRegister + offset], ...
            code.Add(modRm); // ModR/M para o registrador base
            code.AddRange(BitConverter.GetBytes(offset)); // Deslocamento
            code.AddRange(BitConverter.GetBytes(newValue)); // Novo valor

            // Restaura o registrador base
            code.Add(popOp); // pop <baseRegister>

            code.Add(0xC3); // ret

            return code.ToArray();
        }

        private byte[] CreateJump(IntPtr fromAddress, IntPtr toAddress, int originalInstructionLength)
        {
            if (originalInstructionLength < 5)
            {
                throw new ArgumentException("O comprimento da instrução original deve ser pelo menos 5 bytes para um call.");
            }

            byte[] jumpBytes = new byte[originalInstructionLength];
            jumpBytes[0] = 0xE8; // call
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

        // Outros métodos (CONTEXT, ThreadAccess, etc.) permanecem iguais
    }
}