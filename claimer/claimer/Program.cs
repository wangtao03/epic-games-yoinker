using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace epic_claimer
{
    internal class Program
    {
        private static IWebDriver _driver;

        private static WebDriverWait _wait;

        private static string _username;
        private static string _password;

        private static string _captcha;

        private static bool telegram = false;
        private static string _chatId;
        private static string _botToken;
        private static string _pushplus;

        private static void Main(string[] args)
        {
            _username = Environment.GetEnvironmentVariable("epicname");
            _password = Environment.GetEnvironmentVariable("epicpass");
            _captcha = Environment.GetEnvironmentVariable("captcha");
            _chatId = Environment.GetEnvironmentVariable("chatid");
            _botToken = Environment.GetEnvironmentVariable("bottoken");
            _pushplus = Environment.GetEnvironmentVariable("pushplus");

            telegram = ValidateTelegram();
            Console.WriteLine($"Telegram: {telegram}");

            if (ValidateArguments() == false)
            {
                return;
            }

            // create an instance of the webdriver
            _driver = new ChromeDriver();
            // create an instance of the webdriver waiter.
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));
            // maximize the window.
            _driver.Manage().Window.Maximize();

            //if the cookie was not retrieved successfully.
            if (GetCookie(_captcha) == false)
            {
                Console.WriteLine("Failed to retrieve authentication cookie.");
                return;
            }

            if (Login(_username, _password) == false)
            {
                return;
            }

            Thread.Sleep(10000);

            foreach (var url in GetFreeGamesUrls())
            {
                ClaimGame(url);
            }

            Console.WriteLine("Process finished");
        }

        private static bool ValidateTelegram()
        {
            if (string.IsNullOrEmpty(_chatId) || string.IsNullOrEmpty(_botToken))
            {
                return false;
            }
            else
            {
                var isChatID = int.TryParse(_chatId, out _);
                var isToken = new Regex(@"\d{9,10}:[a-zA-Z0-9_-]{35}").IsMatch(_botToken);

                return isChatID && isToken;
            }
        }

        private static bool ValidateArguments()
        {
            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password) || string.IsNullOrEmpty(_captcha))
            {
                Console.WriteLine("Missing arguments");

                return false;
            }
            try
            {
                var mailAddress = new MailAddress(_username);
            }
            catch (FormatException)
            {
                Console.WriteLine("Email address is not valid");

                return false;
            }
            if (new Regex("^https:\\/\\/accounts.hcaptcha.com\\/verify_email\\/[0-9a-z-]+$").IsMatch(_captcha) == false)
            {
                Console.WriteLine("Captcha url is not valid");

                return false;
            }
            return true;
        }

        private static bool GetCookie(string url)
        {
            const int maxTries = 5;

            _driver.Navigate().GoToUrl(url);

            Thread.Sleep(10000);

            for (var i = 0; i < maxTries; i++)
            {
                Console.Write($"{i + 1}/{maxTries} Retrieving cookie: ");

                //Other languages can be used
                GetElement("//button[@data-cy=\"setAccessibilityCookie\"]").Click();
                Thread.Sleep(10000);
                //Other languages can be used
                if (GetElement("//span[@data-cy=\"fetchStatus\"]").Text.StartsWith("Cookie"))
                {
                    Console.WriteLine("Success");

                    return true;
                }

                Console.WriteLine("Failed");
            }

            return false;
        }

        private static void AddEpicCookies()
        {
            _driver.Manage().Cookies.AddCookie(new Cookie(
                name: "HAS_ACCEPTED_AGE_GATE_ONCE",
                value: "true",
                domain: "www.epicgames.com",
                path: "/",
                expiry: DateTime.Now.AddHours(1)
            ));
            _driver.Manage().Cookies.AddCookie(new Cookie(
                name: "OptanonAlertBoxClosed",
                value: "en-US",
                domain: ".epicgames.com",
                path: "/",
                expiry: DateTime.Now.AddHours(1)
            ));
        }

        private static bool Login(string user, string pass)
        {
            const int maxTries = 5;

            //force the use of en-US
            _driver.Navigate().GoToUrl("https://www.epicgames.com/id/login?lang=en-US");
            Thread.Sleep(10000);

            GetElement("//div[@aria-label=\"Sign in with Epic Games\"]").Click();
            AddEpicCookies();

            var loginUrl = _driver.Url;
            for (var i = 0; i < maxTries; i++)
            {
                Console.Write($"{i + 1}/{maxTries} Logging in: ");
                Thread.Sleep(5000);
                var nameField = GetElement("//input[@name=\"email\"]");
                var passField = GetElement("//input[@name=\"password\"]");

                nameField.Clear();
                passField.Clear();

                nameField.SendKeys(user);
                passField.SendKeys(pass);
                Thread.Sleep(5000);

                GetElement("//span[text()=\"Log in now\"]").Click();

                Thread.Sleep(10000);

                if (_driver.Url != loginUrl)
                {
                    Console.WriteLine("Success");

                    return true;
                }
                Console.WriteLine("Failed");
            }
            return false;
        }

        private static IEnumerable<string> GetFreeGamesUrls()
        {
            _driver.Navigate().GoToUrl("https://www.epicgames.com/store/en-US/free-games");

            _wait.Until(x => x.FindElement(By.XPath("//div[@data-component=\"CardGridDesktopBase\"]")).Displayed);

            Thread.Sleep(10000);

            var freeGameSection = new Regex("<h1 class=\"css-.+?\">Free Games<\\/h1>(.+?)Sales and Specials").Match(_driver.PageSource);

            var freeGameUrls = new Regex("href=\"(/store/en-US/product.+?)\">").Matches(freeGameSection.Groups[0].ToString());

            var urls = freeGameUrls.ToList().Select(url => $"https://www.epicgames.com{url.Groups[1]}");

            return urls;
        }

        private static void ClaimGame(string url)
        {
            Console.WriteLine($"Claiming {url}");

            _driver.Navigate().GoToUrl(url);

            Thread.Sleep(10000);

            var title = _driver.Title;

            if (_driver.PageSource.Contains("Owned</span>"))
            {
                Console.WriteLine($"Already owned: {title}");
                return;
            }

            // Click the get button.
            GetElement("//button[@data-testid=\"purchase-cta-button\"]").Click();
            Thread.Sleep(10000);
            // Click place order button
            GetElement("//button[@class=\"btn btn-primary\"]").Click();
            Thread.Sleep(10000);
            // click the agree button
            GetElements("//button[@class=\"btn btn-primary\"]")[1].Click();
            Thread.Sleep(10000);

            Console.WriteLine($"Claimed {title}");

            if (telegram)
            {
                try
                {
                    var bot = new TelegramBotClient(_botToken);
                    url += url.ToLower().EndsWith("/home") ? "" : "/home";
                    bot.SendTextMessageAsync(new ChatId(int.Parse(_chatId)), $"{title}\r\n{url}").Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Telegram ERROR: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(_pushplus))
            {
                _driver.Navigate().GoToUrl($"http://pushplus.hxtrip.com/send?token={_pushplus}&title={title}&content={url}&template=html");
                Thread.Sleep(10000);
            }
        }

        private static List<IWebElement> GetElements(string xPath)
        {
            try
            {
                _wait.Until(x => x.FindElements(By.XPath(xPath)));

                return _driver.FindElements(By.XPath(xPath)).ToList();
            }
            catch (Exception)
            {
                Console.WriteLine($"Elements not found: {xPath}");
                throw;
            }
        }

        private static IWebElement GetElement(string xPath)
        {
            try
            {
                _wait.Until(x => x.FindElement(By.XPath(xPath)));

                return _driver.FindElement(By.XPath(xPath));
            }
            catch (Exception)
            {
                Console.WriteLine($"Element not found: {xPath}");
                return null;
            }
        }
    }
}