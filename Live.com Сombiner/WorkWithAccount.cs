using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Leaf.xNet;

namespace Live.com_Сombiner
{
    class WorkWithAccount
    {
        #region Свойства класса
        /// <summary>
        /// Паузы, и количество попыток запроса
        /// </summary>
        public static int minPause, maxPause, countRequest, minPauseRegistration, maxPauseRegistration;
        /// <summary>
        /// Режим работы
        /// </summary>
        public static string OperatingMode;
        /// <summary>
        /// Перечисление статусов
        /// </summary>
        public enum Status
        {
            True,
            False,
            UnknownError,
            BlockedAccount
        }
        /// <summary>
        /// Количество аккаунтов для регистрации
        /// </summary>
        public static int CountAccountForRegistration;

        public static Random rand = new Random((int)DateTime.Now.Ticks);
        public static object locker = new object();
        public static object LogOBJ = new object();
        #endregion

        #region Выбор режима работы
        public static void StartWork()
        {
            try
            {
                SaveData.WriteToLog(null, "Начал свою работу");
                if (OperatingMode == "Регистратор")
                    RegistrationAccount();
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        #endregion

        #region Запуск метода регистрации и проверка результата
        public static void RegistrationAccount()
        {
            try
            {
                string Password, UserAgent, NameSurname;
                string proxyLog = "";
                ProxyClient proxyClient;
                while (true)
                {
                    SaveData.WriteToLog($"System", "Попытка получить номер");
                    (string tzid, string number) number = GetSmsReg.GetNumber();
                    #region Выдача аккаунтов
                    lock (locker)
                    {
                        if (SaveData.UsedRegistration < CountAccountForRegistration)
                        {
                            (string NameSurname, string Password) DataForRegistration = GetNameSurnamePassword.Get();

                            if (String.IsNullOrEmpty(DataForRegistration.NameSurname) || String.IsNullOrEmpty(DataForRegistration.Password) || string.IsNullOrEmpty(number.number))
                                continue;

                            NameSurname = DataForRegistration.NameSurname;
                            Password = DataForRegistration.Password;
                            SaveData.UsedRegistration++;
                            SaveData.SaveAccount($"{number.number}:{Password}", SaveData.ProcessedRegistrationList);
                        }
                        else
                        {
                            break;
                        }
                        UserAgent = GetUserAgent.get();
                        proxyClient = GetProxy.get();
                        proxyLog = proxyClient == null ? "" : $";{proxyClient.ToString()}";
                    }
                    #endregion

                    #region Вызов метода регистрации, и проверка результата
                    SaveData.WriteToLog($"{number.number}:{Password}", "Попытка зарегестрировать аккаунт");

                    (Status status, CookieStorage cookie) Data;
                    for (int i = 0; i < countRequest; i++)
                    {
                        Data = GoRegistrationAccount(NameSurname, number, Password, UserAgent, proxyClient);
                        switch (Data.status)
                        {
                            case Status.True:
                                SaveData.GoodRegistration++;
                                SaveData.WriteToLog($"{number.number}:{Password}", "Аккаунт успешно зарегестрирован");
                                SaveData.SaveAccount($"{number.number}:{Password}{proxyLog}|{UserAgent}", SaveData.GoodRegistrationList);
                                Data.cookie.SaveToFile($"out/cookies/{number.number}.jar", true);
                                break;
                            case Status.False:
                                SaveData.InvalidRegistration++;
                                SaveData.WriteToLog($"{number.number}:{Password}", "Аккаунт не зарегестрирован");
                                SaveData.SaveAccount($"{number.number}:{Password}{proxyLog}|{UserAgent}", SaveData.InvalidRegistrationList);
                                break;
                            default:
                                SaveData.WriteToLog($"{number.number}:{Password}", "Неизвестная ошибка, повторяем.");
                                UserAgent = GetUserAgent.get();
                                proxyClient = GetProxy.get();
                                continue;
                        }
                        break;
                    }
                    int sleep = rand.Next(minPauseRegistration, maxPauseRegistration);
                    SaveData.WriteToLog($"System", $"Засыпаем на {sleep/60000} минут");
                    Thread.Sleep(sleep);
                    #endregion
                }
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
        }
        #endregion

        #region Метод регистрации аккаунта
        /// <summary>
        /// Метод регистрации аккаунта
        /// </summary>
        /// <param name="Login">Логин для регистрации</param>
        /// <param name="Password">Пароль для регистрации</param>
        /// <param name="UserAgent">UserAgent</param>
        /// <param name="proxyClient">Прокси</param>
        /// <returns></returns>
        public static (Status status, CookieStorage cookie) GoRegistrationAccount(string nameSurname, (string tzid, string number) number, string password, string userAgent, ProxyClient proxyClient)
        {
            try
            {
                using (HttpRequest request = new HttpRequest())
                {
                    request.Cookies = new CookieStorage();
                    request.UseCookies = true;
                    request.Proxy = proxyClient;
                    request.UserAgent = userAgent;
                    string day = rand.Next(1, 28).ToString();
                    string month = rand.Next(1, 13).ToString();
                    string year = rand.Next(1985, 2003).ToString();

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

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    string Response = request.Get("Https://www.instagram.com/").ToString();

                    string XInstagramAJAX = Response.BetweenOrEmpty("rollout_hash\":\"", "\"");
                    string csrf_token = Response.BetweenOrEmpty("csrf_token\":\"", "\"");

                    string[] librarys = Response.BetweenOrEmpty("<link rel=\"manifest\" href=\"/data/manifest.json\">", "<script type=\"text/javascript\">").Split('>');
                    string ConsumerLibCommons = ParseCurrentLibrary(librarys, "ConsumerLibCommons.js");
                    string ConsumerUICommons = ParseCurrentLibrary(librarys, "ConsumerUICommons.js");
                    string Consumer = ParseCurrentLibrary(librarys, "Consumer.js");
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

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    Response = request.Get(ConsumerLibCommons).ToString();

                    string PWAAppId = Response.BetweenOrEmpty("e.instagramWindowsPWAAppId='", "'");
                    string INTERSTITIAL = Response.BetweenOrEmpty("INTERSTITIAL:'", "'");
                    string PAGE_TOP = Response.BetweenOrEmpty("PAGE_TOP:'", "'");
                    string TOOLTIP = Response.BetweenOrEmpty("TOOLTIP:'", "'");
                    #endregion

                    #region Делаем Get запрос для парсинга Params bloks_versioning_id
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

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    string bloks_versioning_id = request.Get(ConsumerUICommons).ToString().BetweenOrEmpty("e.VERSIONING_ID=\"", "\"");
                    #endregion

                    #region Делаем Get запрос для парсинга viewer
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

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    string viewer = request.Get(Consumer).ToString().BetweenOrEmpty("m.exports=\"", "\"") + "\"";
                    string surfaces_to_queries = $"{{\"{PAGE_TOP}\":\"{viewer},\"{INTERSTITIAL}\":\"{viewer},\"{TOOLTIP}\":\"{viewer}}}";
                    #endregion

                    #region Делаем Post запрос на проверку браузера
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    var UrlParams = new RequestParams();
                    UrlParams["bloks_versioning_id"] = bloks_versioning_id;
                    UrlParams["surfaces_to_queries"] = surfaces_to_queries;
                    UrlParams["vc_policy"] = "default";
                    UrlParams["version"] = "1";

                    request.AddHeadersOrder(new List<string>()
                {
                    "Origin",
                    "Referer",
                    "Accept",
                    "Accept-Language",
                    "Content-Type",
                    "X-CSRFToken",
                    "X-IG-App-ID",
                    "X-IG-WWW-Claim",
                    "X-Instagram-AJAX",
                    "X-Requested-With",
                    "User-Agent",
                    "Host",
                    "Connection",
                    "Cache-Control",
                    "Cookie",
                    "Accept-Encoding",
                    "Content-Length"
                });

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Post("https://www.instagram.com/qp/batch_fetch_web/", UrlParams);
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

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Get("Https://www.instagram.com/accounts/emailsignup/?__a=1");
                    #endregion

                    #region Начинаем отправлять Ajax Запросы на ввод данных для регистрации (Ввели Номер)
                    string client_id = request.Cookies.GetCookie("mid", "Https://www.instagram.com/accounts/emailsignup/?__a=1").Value;

                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");


                    UrlParams.Clear();
                    UrlParams["phone_number"] = number.number;
                    UrlParams["username"] = "";
                    UrlParams["first_name"] = "";
                    UrlParams["client_id"] = client_id;
                    UrlParams["opt_into_one_tap"] = "false";

                    request.HeadersOrder = new List<string>()
                {
                    "Origin",
                    "Referer",
                    "Accept",
                    "Accept-Language",
                    "Content-Type",
                    "X-CSRFToken",
                    "X-IG-App-ID",
                    "X-IG-WWW-Claim",
                    "X-Instagram-AJAX",
                    "X-Requested-With",
                    "User-Agent",
                    "Host",
                    "Connection",
                    "Cache-Control",
                    "Cookie",
                    "Accept-Encoding",
                    "Content-Length"
                };

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Post("Https://www.instagram.com/accounts/web_create_ajax/attempt/", UrlParams);
                    #endregion

                    #region Ajax Запросы на ввод данных для регистрации (Ввели Имя)
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    UrlParams.Clear();
                    UrlParams["phone_number"] = number.number;
                    UrlParams["username"] = "";
                    UrlParams["first_name"] = nameSurname;
                    UrlParams["client_id"] = client_id;
                    UrlParams["opt_into_one_tap"] = "false";

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    Response = request.Post("Https://www.instagram.com/accounts/web_create_ajax/attempt/", UrlParams).ToString();
                    string login = GetLogin(Response.BetweenOrEmpty("username_suggestions\": [", "]"));
                    #endregion

                    #region Ajax Запросы на ввод данных для регистрации (Ввели логин)
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    UrlParams.Clear();
                    UrlParams["phone_number"] = number.number;
                    UrlParams["username"] = login;
                    UrlParams["first_name"] = nameSurname;
                    UrlParams["client_id"] = client_id;
                    UrlParams["opt_into_one_tap"] = "false";

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Post("Https://www.instagram.com/accounts/web_create_ajax/attempt/", UrlParams);
                    #endregion

                    #region Ajax Запросы на ввод данных для регистрации (Ввели пароль)
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    UrlParams.Clear();
                    UrlParams["enc_password"] = $"#PWD_INSTAGRAM_BROWSER:0:{JSTime(true)}:{password}";
                    UrlParams["phone_number"] = number.number;
                    UrlParams["username"] = login;
                    UrlParams["first_name"] = nameSurname;
                    UrlParams["client_id"] = client_id;
                    UrlParams["seamless_login_enabled"] = "1";
                    UrlParams["opt_into_one_tap"] = "false";

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Post("Https://www.instagram.com/accounts/web_create_ajax/attempt/", UrlParams);
                    #endregion

                    #region Делаем Post запрос на проверку даты
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    UrlParams.Clear();
                    UrlParams["day"] = day;
                    UrlParams["month"] = month;
                    UrlParams["year"] = year;

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Post("Https://www.instagram.com/web/consent/check_age_eligibility/", UrlParams);
                    #endregion

                    #region Ajax Запросы на ввод данных для регистрации (Ввели дату рождения)
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    UrlParams.Clear();
                    UrlParams["enc_password"] = $"#PWD_INSTAGRAM_BROWSER:0:{JSTime(true)}:{password}";
                    UrlParams["phone_number"] = number.number;
                    UrlParams["username"] = login;
                    UrlParams["first_name"] = nameSurname;
                    UrlParams["day"] = day;
                    UrlParams["month"] = month;
                    UrlParams["year"] = year;
                    UrlParams["client_id"] = client_id;
                    UrlParams["seamless_login_enabled"] = "1";

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Post("Https://www.instagram.com/accounts/web_create_ajax/attempt/", UrlParams);
                    #endregion

                    #region Отправлям Post запрос на отправку кода верификации на телефон
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    UrlParams.Clear();
                    UrlParams["client_id"] = client_id;
                    UrlParams["phone_number"] = number.number;
                    UrlParams["phone_id"] = "";
                    UrlParams["big_blue_token"] = "";

                    Thread.Sleep(rand.Next(minPause, maxPause));
                    request.Post("Https://www.instagram.com/accounts/send_signup_sms_code_ajax/", UrlParams);
                    #endregion

                    #region Ajax Запросы на ввод данных для регистрации (Ввели код подтверждения)
                    request.AddHeader("Origin", "https://www.instagram.com");
                    request.AddHeader("Referer", "https://www.instagram.com/accounts/emailsignup/");
                    request.AddHeader("Accept", "*/*");
                    request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                    request.AddHeader("X-CSRFToken", csrf_token);
                    request.AddHeader("X-IG-App-ID", PWAAppId);
                    request.AddHeader("X-IG-WWW-Claim", "0");
                    request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                    request.AddHeader("X-Requested-With", "XMLHttpRequest");
                    request.AddHeader("Cache-Control", "no-cache");

                    string sms = GetSmsReg.GetCode(number.tzid);
                    if (string.IsNullOrEmpty(sms))
                        return (Status.False, null);

                    UrlParams.Clear();
                    UrlParams["enc_password"] = $"#PWD_INSTAGRAM_BROWSER:0:{JSTime(true)}:{password}";
                    UrlParams["phone_number"] = number.number;
                    UrlParams["username"] = login;
                    UrlParams["first_name"] = nameSurname;
                    UrlParams["day"] = day;
                    UrlParams["month"] = month;
                    UrlParams["year"] = year;
                    UrlParams["sms_code"] = sms;
                    UrlParams["client_id"] = client_id;
                    UrlParams["seamless_login_enabled"] = "1";
                    UrlParams["tos_version"] = "row";

                    request.IgnoreProtocolErrors = true;
                    Thread.Sleep(rand.Next(minPause, maxPause));
                    Response = request.Post("Https://www.instagram.com/accounts/web_create_ajax/", UrlParams).ToString();
                    #endregion

                    if (Response.Contains("account_created\":true"))
                        return (Status.True, request.Cookies);

                    if (Response.Contains("checkpoint_required"))
                    {
                        #region Отправляем запрос с решенной капчей
                        string checkpoint_url = Response.BetweenOrEmpty("checkpoint_url\":\"", "\"");
                        string urlresult = Response.BetweenOrEmpty("checkpoint_url\":\"", "?challenge");

                        request.AddHeadersOrder(new List<string>()
                        {
                        "Origin",
                        "Referer",
                        "Accept",
                        "Accept-Language",
                        "Content-Type",
                        "X-CSRFToken",
                        "X-IG-App-ID",
                        "X-IG-WWW-Claim",
                        "X-Instagram-AJAX",
                        "X-Requested-With",
                        "User-Agent",
                        "Host",
                        "Connection",
                        "Cache-Control",
                        "Cookie",
                        "Accept-Encoding",
                        "Content-Length"
                        });

                        request.AddHeader("Origin", "https://www.instagram.com");
                        request.AddHeader("Referer", checkpoint_url);
                        request.AddHeader("Accept", "*/*");
                        request.AddHeader("Accept-Language", "ru-UA,ru;q=0.8,en-US;q=0.5,en;q=0.2");
                        request.AddHeader("X-CSRFToken", csrf_token);
                        request.AddHeader("X-IG-App-ID", PWAAppId);
                        request.AddHeader("X-IG-WWW-Claim", "0");
                        request.AddHeader("X-Instagram-AJAX", XInstagramAJAX);
                        request.AddHeader("X-Requested-With", "XMLHttpRequest");
                        request.AddHeader("Cache-Control", "no-cache");
                        request.KeepAlive = false;

                        UrlParams.Clear();
                        UrlParams["g-recaptcha-response"] = GetCaptcha.GetRecaptcha("https://www.instagram.com/", "6LebnxwUAAAAAGm3yH06pfqQtcMH0AYDwlsXnh-u", $"{number.number}:{password}");

                        Thread.Sleep(rand.Next(minPause, maxPause));
                        Response = request.Post(urlresult, UrlParams).ToString();
                        #endregion

                        if (Response.Contains("status\":\"ok"))
                            return (Status.True, request.Cookies);
                        else
                            return (Status.False, null);
                    }
                    else
                        return (Status.False, null);
                }
            }
            catch { };
            return (Status.UnknownError, null);
        }
        #endregion

        #region Парсим Логин
        public static string GetLogin(string logins)
        {
            List<string> Logins = new List<string>();
            try
            {
                while (logins.Contains("\""))
                {
                    Logins.Add(logins.BetweenOrEmpty("\"", "\""));
                    logins = logins.Replace($"\"{logins.BetweenOrEmpty("\"", "\"")}\"", "");
                }
            }
            catch (Exception exception) { MessageBox.Show(exception.Message); }
            return Logins[rand.Next(Logins.Count)];
        }
        #endregion

        #region UnixTime
        public static string JSTime(bool cut = false)
        {
            try
            {
                string t = DateTime.UtcNow
                   .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                   .TotalMilliseconds.ToString();

                if (t.Contains(","))
                    t = t.Substring(0, t.IndexOf(','));

                if (cut && t.Length > 10) t = t.Remove(t.Length - 3, 3);

                return t;
            }
            catch { }

            return "";
        }
        #endregion

        #region Метод парсинга нужной библиотеки JS
        /// <summary>
        /// Метод парсинга нужной библиотеки JS
        /// </summary>
        /// <param name="librarys">Массив строк с библиотеками JS</param>
        /// <param name="currentLibrarys">Какую библиотеку JS будем искать</param>
        /// <returns></returns>
        public static string ParseCurrentLibrary(string[] librarys, string currentLibrarys)
        {
            try
            {
                foreach (var Librarys in librarys)
                    if (Librarys.Contains(currentLibrarys))
                        return "https://www.instagram.com" + Librarys.BetweenOrEmpty("href=\"", "\"");
            }   
            catch(Exception exception) { MessageBox.Show(exception.Message); }
            return null;
        }
        #endregion
    }
}
