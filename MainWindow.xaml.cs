using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TCPchat
{
    public partial class MainWindow : Window
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private bool isConnected = false;
        private string currentUser;
        private Dictionary<char, int> encode;
        private string logFilePath;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEncodingDictionary();
            CreateLogFile();
        }

        private void CreateLogFile()
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy_HH-mm-ss");
            logFilePath = $"{timestamp}.txt";
            File.WriteAllText(logFilePath, $"Лог файл создан: {timestamp}\n\n");
        }

        private void InitializeEncodingDictionary()
        {
            encode = new Dictionary<char, int>();
            string chars = "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            int[] codes = Enumerable.Range(192, 64).Concat(Enumerable.Range(224, 64)).ToArray();

            for (int i = 0; i < chars.Length; i++)
            {
                encode[chars[i]] = codes[i];
            }
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
                string ipAddress = config["TcpSettings:IpAddress"];
                int port = int.Parse(config["TcpSettings:Port"]);

                currentUser = NickBox.Text.Trim();
                if (string.IsNullOrEmpty(currentUser))
                {
                    MessageBox.Show("Введите никнейм.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);

                NetworkStream stream = client.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                await writer.WriteLineAsync(currentUser);

                isConnected = true;
                UpdateConnectionStatus();

                ServerSettings.Text = $"Сервер: {ipAddress}\nПорт: {port}";
                StartListening();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (client != null)
            {
                await writer.WriteLineAsync("DISCONNECT");
                client.Close();
                client = null;
            }
            isConnected = false;
            UpdateConnectionStatus();
            ServerSettings.Text = "";
            Users.Items.Clear();
        }

        private async void StartListening()
        {
            try
            {
                while (isConnected)
                {
                    string receivedMessage = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(receivedMessage)) break;

                    Dispatcher.Invoke(() =>
                    {
                        ChatHistory.Text += receivedMessage + "\n";
                        File.AppendAllText(logFilePath, $"{receivedMessage}\n");
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении сообщения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsCyrillic(string text)
        {
            foreach (char c in text)
            {
                if (!((c >= 'А' && c <= 'я') || c == 'ё' || c == 'Ё' || c == ' '))
                {
                    return false;
                }
            }
            return true;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("Не подключено к серверу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string message = SendBox.Text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                MessageBox.Show("Введите сообщение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsCyrillic(message))
            {
                MessageBox.Show("Сообщение содержит символы, не входящие в кириллицу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string encodedMessage = EncodeToWindows1251(message);
                string binaryMessage = EncodeToBinary(encodedMessage);

                string formattedMessage = $"{message}\n" +
                                         $"{encodedMessage} (Кодировка windows 1251)\n" +
                                         $"{binaryMessage} (Двоичное)";
                await writer.WriteLineAsync(formattedMessage);

                SendBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string EncodeToWindows1251(string text)
        {
            List<int> encodedValues = new List<int>();

            foreach (char c in text)
            {
                if (encode.TryGetValue(c, out int code))
                {
                    encodedValues.Add(code);
                }
                else
                {
                    encodedValues.Add((int)c);
                }
            }

            return string.Join(" ", encodedValues);
        }

        private string EncodeToBinary(string encodedText)
        {
            string[] codes = encodedText.Split(' ');
            List<string> binaryValues = new List<string>();

            foreach (string code in codes)
            {
                if (int.TryParse(code, out int num))
                {
                    binaryValues.Add(Convert.ToString(num, 2).PadLeft(8, '0'));
                }
            }

            return string.Join(" ", binaryValues);
        }

        private void UpdateConnectionStatus()
        {
            Connect.IsEnabled = !isConnected;
            Disconnect.IsEnabled = isConnected;
            Send.IsEnabled = isConnected;
            NickBox.IsEnabled = !isConnected;
        }
    }
}