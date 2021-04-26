using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Leaf.xNet;

namespace Live.com_Сombiner
{
    public partial class Form1 : Form
    {
        #region Свойства класа
        /// <summary>
        /// List с Аккаунтами
        /// </summary>
        List<string> Account = new List<string>();
        public static Stopwatch stopwatch = new Stopwatch();
        #endregion

        public Form1()
        {
            InitializeComponent();
            #region Загрузка настроек перед запуском
            UserAgentFileBox.Text = Properties.Settings.Default.UserAgentFileBox;
            ProxyFilePathBox.Text = Properties.Settings.Default.ProxyFilePathBox;
            ProxyCheckLinkBox.Text = Properties.Settings.Default.ProxyCheckLinkBox;
            OperatingModeBox.SelectedIndex = Properties.Settings.Default.OperatingModeBox;
            ProxySourceBox.SelectedIndex = Properties.Settings.Default.ProxySourceBox;
            ProxyModeBox.SelectedIndex = Properties.Settings.Default.ProxyModeBox;
            TypeOfProxyBox.SelectedIndex = Properties.Settings.Default.TypeOfProxyBox;
            MinPauseNumeric.Value = Properties.Settings.Default.MinPauseNumeric;
            MaxPauseNumeric.Value = Properties.Settings.Default.MaxPauseNumeric;
            CountRequestNumeric.Value = Properties.Settings.Default.CountRequestNumeric;
            CountThreadNumeric.Value = Properties.Settings.Default.CountThreadNumeric;
            DisableLogBox.Checked = Properties.Settings.Default.DisableLogBox;
            ClientKeyBox.Text = Properties.Settings.Default.ClientKeyBox;
            NameSurnameBox.Text = Properties.Settings.Default.NameSurnameBox;
            PasswordFileBox.Text = Properties.Settings.Default.PasswordFileBox;
            PasswordGenerateCheckBox.Checked = Properties.Settings.Default.PasswordGenerateCheckBox;
            CountAccountNumeric.Value = Properties.Settings.Default.CountAccountNumeric;
            #endregion
        }
        #region Сохранение настроек перед закрытием формы
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Properties.Settings.Default.UserAgentFileBox = UserAgentFileBox.Text;
                Properties.Settings.Default.ProxyFilePathBox = ProxyFilePathBox.Text;
                Properties.Settings.Default.ProxyCheckLinkBox = ProxyCheckLinkBox.Text;
                Properties.Settings.Default.OperatingModeBox = OperatingModeBox.SelectedIndex;
                Properties.Settings.Default.ProxySourceBox = ProxySourceBox.SelectedIndex;
                Properties.Settings.Default.ProxyModeBox = ProxyModeBox.SelectedIndex;
                Properties.Settings.Default.TypeOfProxyBox = TypeOfProxyBox.SelectedIndex;
                Properties.Settings.Default.MinPauseNumeric = (int)MinPauseNumeric.Value;
                Properties.Settings.Default.MaxPauseNumeric = (int)MaxPauseNumeric.Value;
                Properties.Settings.Default.CountRequestNumeric = (int)CountRequestNumeric.Value;
                Properties.Settings.Default.CountThreadNumeric = (int)CountThreadNumeric.Value;
                Properties.Settings.Default.DisableLogBox = DisableLogBox.Checked;
                Properties.Settings.Default.ClientKeyBox = ClientKeyBox.Text;
                Properties.Settings.Default.NameSurnameBox = NameSurnameBox.Text;
                Properties.Settings.Default.PasswordFileBox = PasswordFileBox.Text;
                Properties.Settings.Default.PasswordGenerateCheckBox = PasswordGenerateCheckBox.Checked;
                Properties.Settings.Default.CountAccountNumeric = (int)CountAccountNumeric.Value;
                Properties.Settings.Default.Save();
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (Controller.IsAlive())
                {
                    MessageBox.Show("В работе");
                    return;
                }
                if (!DataValidation())
                    return;
                new Thread(() => Controller.StartThread()) { IsBackground = true }.Start();
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }

        #region Проверка переменных и запуск таймера
        public bool DataValidation()
        {
            try
            {
                if (!Directory.Exists("out"))
                    Directory.CreateDirectory("out");
                if (!Directory.Exists("in"))
                    Directory.CreateDirectory("in");
                if (!Directory.Exists("out/regger"))
                    Directory.CreateDirectory("out/regger");
                if (!Directory.Exists("out/accounts"))
                    Directory.CreateDirectory("out/accounts");
                if (!Directory.Exists("out/spamer"))
                    Directory.CreateDirectory("out/spamer");
                if (!Directory.Exists("out/cookies"))
                    Directory.CreateDirectory("out/cookies");

                if (MinPauseNumeric.Value >= MaxPauseNumeric.Value)
                {
                    MessageBox.Show("Максимальная пауза между запросами не может быть больше либо равняться Минимальной!");
                    return false;
                }
                if (MinPauseRegistrationNumeric.Value >= MaxPauseRegistrationNumeric.Value)
                {
                    MessageBox.Show("Максимальная пауза между регистрациями не может быть больше либо равняться Минимальной!");
                    return false;
                }
                if (CountThreadNumeric.Value <= 0)
                {
                    MessageBox.Show("Количество потоков не может быть меньше либо равнятся нулю!");
                    return false;
                }
                if (OperatingModeBox.Text == "Регистратор")
                {
                    if (CountThreadNumeric.Value > CountAccountNumeric.Value)
                    {
                        MessageBox.Show("Количество потоков не может быть больше количества регистрируемых аккаунтов!");
                        return false;
                    }
                }
                else
                {
                    if (CountThreadNumeric.Value > Account.Count)
                    {
                        MessageBox.Show("Количество потоков не может быть больше количества аккаунтов!");
                        return false;
                    }
                }
                if (!GetUserAgent.FillInUserAgents(UserAgentFileBox.Text, BuiltInUserAgents.Checked))
                    return false;
                if (!GetProxy.FillInProxy(ProxyFilePathBox.Text, ProxySourceBox.Text, ProxyModeBox.Text, TypeOfProxyBox.Text, ProxyCheckLinkBox.Text))
                    return false;
                if (!CheckBalanceCaptcha())
                    return false;
                if (!GetNameSurnamePassword.FillInData(NameSurnameBox.Text, PasswordGenerateCheckBox.Checked, PasswordFileBox.Text))
                    return false;
                if (!GetSmsReg.FillInSettings(CountrySmsRegBox.Text, ServiceSmsRegBox.Text, APIKeySmsRegBox.Text))
                    return false;

                // Очистки лога, и запуск таймера.
                LogBox.Text = "";
                Controller.isAliveTimer = true;
                stopwatch.Restart();
                timer1.Enabled = true;

                // Обнуление статистики регистрации
                SaveData.UsedRegistration = 0;
                SaveData.GoodRegistration = 0;
                SaveData.InvalidRegistration = 0;

                WorkWithAccount.minPause = (int)MinPauseNumeric.Value;
                WorkWithAccount.maxPause = (int)MaxPauseNumeric.Value;
                WorkWithAccount.minPauseRegistration = (int)MinPauseRegistrationNumeric.Value * 60000;
                WorkWithAccount.maxPauseRegistration = (int)MaxPauseRegistrationNumeric.Value * 60000;
                WorkWithAccount.countRequest = (int)CountRequestNumeric.Value;
                WorkWithAccount.OperatingMode = OperatingModeBox.Text;
                Controller.countThread = (int)CountThreadNumeric.Value;
                WorkWithAccount.CountAccountForRegistration = (int)CountAccountNumeric.Value;
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
            return true;
        }
        #endregion

        #region Загрузка пути к файлу
        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                LoadFromFile(NameSurnameBox);
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                LoadFromFile(PasswordFileBox);
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        private void UploadProxyButton_Click(object sender, EventArgs e)
        {
            try
            {
                LoadFromFile(ProxyFilePathBox);
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        private void UploadUserAgentButton_Click(object sender, EventArgs e)
        {
            try
            {
                LoadFromFile(UserAgentFileBox);
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        public void LoadFromFile(Guna2TextBox textBox = null)
        {
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "txt files (*.txt)|*.txt";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        textBox.Text = openFileDialog.FileName;
                    }
                }
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        #endregion

        #region Метод для работы таймера и загрузка данных на UI
        /// <summary>
        /// Метод для работы таймера и загрузка данных на UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                StopWatchLabel.Text = $"Время работы программы {stopwatch.Elapsed.ToString("hh\\:mm\\:ss")}";

                // Статистика регистрации
                UsedRegistrationLabel.Text = $"Отработано: {SaveData.UsedRegistration}";
                GoodRegistrationLabel.Text = $"Удачно: {SaveData.GoodRegistration}";
                InvalidRegistrationLabel.Text = $"Не удачно: {SaveData.InvalidRegistration}";

                // Запись аккаунтов.
                lock (WorkWithAccount.LogOBJ)
                {
                    // Запись аккаунтов с регистрации
                    File.AppendAllLines("out/regger/good.txt", SaveData.GoodRegistrationList);
                    SaveData.GoodRegistrationList.Clear();
                    File.AppendAllLines("out/regger/bad.txt", SaveData.InvalidRegistrationList);
                    SaveData.InvalidRegistrationList.Clear();
                    File.AppendAllLines("out/regger/processed.txt", SaveData.ProcessedRegistrationList);
                    SaveData.ProcessedRegistrationList.Clear();
                }

                // Запись данных в Лог.
                lock (WorkWithAccount.LogOBJ)
                {
                    if (!DisableLogBox.Checked)
                    {
                        File.AppendAllLines("out/log.txt", SaveData.Log);
                        while (SaveData.Log.Count != 0)
                        {
                            LogBox.Text += SaveData.Log[0] + Environment.NewLine;
                            SaveData.Log.RemoveAt(0);
                        }
                    }
                    else
                    {
                        File.AppendAllLines("out/log.txt", SaveData.Log);
                        SaveData.Log.Clear();
                    }
                }
                // Когда программа закончила свою работу, останавливаем таймер.
                if (!Controller.isAliveTimer)
                {
                    stopwatch.Stop();
                    timer1.Enabled = false;
                }
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        #endregion

        #region Стоп
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < Controller.Threads.Count; i++)
                {
                    Controller.Threads[i].Abort();
                }
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        #endregion

        #region Методы проверки баланса капчи
        private void CheckBalanceCaptchaButton_Click(object sender, EventArgs e)
        {
            try
            {
                CheckBalanceCaptcha();
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        public bool CheckBalanceCaptcha()
        {
            try
            {
                string result = GetCaptcha.CheckBalance(ClientKeyBox.Text);
                if (result != "")
                {
                    AntiCaptchaBalanceBox.Text = $"Текущий баланс: {(Convert.ToDouble(result.Replace(".", ","))).ToString("#.##")} $";
                    return true;
                }
                else
                {
                    MessageBox.Show("Проверьте Client Key");
                    return false;
                }
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
            return false;
        }
        #endregion

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 200; i++)
            {
                using (HttpRequest request = new HttpRequest())
                {
                    request.Cookies = new CookieStorage();
                    request.UseCookies = true;
                    request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; MSAppHost/3.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.102 Safari/537.36 Edge/18.19042";
                    int min = 200;
                    int max = 8000;
                    Random rand = new Random((int)DateTime.Now.Ticks);

                    #region Делаем Get запрос на главную страницу сайта. Парсим: Headers XInstagramAJAX, Headers csrf_token, Библиотека ConsumerLibCommons.
                    request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("Upgrade-Insecure-Requests", "1");
                    request.AddHeadersOrder(new List<string>()
                    {
                    "Accept",
                    "Accept-Language",
                    "Upgrade-Insecure-Requests",
                    "User-Agent",
                    "Host",
                    "Connection",
                    "Accept-Encoding"
                    });

                    Thread.Sleep(rand.Next(min, max));
                    string Response = request.Get("Https://www.instagram.com/").ToString();

                    string XInstagramAJAX = Response.BetweenOrEmpty("rollout_hash\":\"", "\"");
                    string csrf_token = Response.BetweenOrEmpty("csrf_token\":\"", "\"");

                    string[] librarys = Response.BetweenOrEmpty("<link rel=\"manifest\" href=\"/data/manifest.json\">", "<script type=\"text/javascript\">").Split('>');
                    string ConsumerLibCommons = WorkWithAccount.ParseCurrentLibrary(librarys, "ConsumerLibCommons.js");
                    #endregion

                    #region Делаем Get запрос для парсинга Headers FbAppID. Post INTERSTITIAL, PAGE_TOP, TOOLTIP
                    request.AddHeader("Referer", "https://www.instagram.com/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeadersOrder(new List<string>()
                    {
                        "Referer",
                        "Accept",
                        "Accept-Language",
                        "User-Agent",
                        "Host",
                        "Connection",
                        "Cookie",
                        "Accept-Encoding"
                    });

                    Thread.Sleep(rand.Next(min, max));
                    Response = request.Get(ConsumerLibCommons).ToString();

                    string PWAAppId = Response.BetweenOrEmpty("e.instagramWindowsPWAAppId='", "'");
                    #endregion

                    #region Делаем Get запрос на страницу регистрации
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");

                    request.AddHeadersOrder(new List<string>()
                    {
                    "Referer",
                    "Accept",
                    "Accept-Language",
                    "X-IG-App-ID",
                    "X-IG-WWW-Claim",
                    "X-Requested-With",
                    "User-Agent",
                    "Host",
                    "Connection",
                    "Cookie",
                    "Accept-Encoding"
                    });

                    Thread.Sleep(rand.Next(min, max));
                    request.Get("Https://www.instagram.com/accounts/emailsignup/?__a=1");
                    #endregion
                }
            }
        }
    }
}
