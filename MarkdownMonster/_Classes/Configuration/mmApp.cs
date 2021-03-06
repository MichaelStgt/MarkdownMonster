﻿using System;
using System.Windows;
using System.Windows.Media;
using MahApps.Metro;
using MahApps.Metro.Controls;
using Westwind.Utilities;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarkdownMonster.Windows;
using Microsoft.Win32;

namespace MarkdownMonster
{

    /// <summary>
    /// Application class for Markdown Monster that provides
    /// a global static placeholder for configuration and some
    /// utility functions
    /// </summary>
    public class mmApp
    {
        private const string mkbase = "a4dx23513TY69dE+533#1Ae@rTo*dO&-002";

        /// <summary>
        /// Holds a static instance of the application's configuration settings
        /// </summary>
        public static ApplicationConfiguration Configuration { get; set;  }


        /// <summary>
        /// The full name of the application displayed on toolbar and dialogs
        /// </summary>
        public static string ApplicationName { get; set; } = "Markdown Monster";

        public static DateTime Started { get; set;  }

        /// <summary>
        /// Returns a machine specific encryption key that can be used for passwords
        /// and other settings. 
        /// 
        /// If the Configuration.UseMachineEcryptionKeyForPasswords flag
        /// is false, no machine specific information is added to the key.
        /// Do this if you want to share your encrypted configuration settings
        /// in cloud based folders like DropBox, OneDrive, etc.
        /// </summary>      
        public static string EncryptionMachineKey
        {
            get
            {                
                if (!mmApp.Configuration.UseMachineEncryptionKeyForPasswords)
                    return mkbase;

                return InternalMachineKey;
            }
        }
        
        /// <summary>
        /// Internal Machine Key which is a registry GUID value
        /// </summary>
        internal static string InternalMachineKey
        {
            get
            {
                if (_internalMachineKey != null)
                    return _internalMachineKey;

                var mmRegKey = @"SOFTWARE\West Wind Technologies\Markdown Monster";

                dynamic data;
                if (!ComputerInfo.TryGetRegistryKey(mmRegKey, "MachineKey", out data, UseCurrentUser: true))
                {
                    data = Guid.NewGuid().ToString();
                    var rk = Registry.CurrentUser.OpenSubKey(mmRegKey, true);

                    if (rk == null)
                        rk = Registry.CurrentUser.CreateSubKey(mmRegKey);

                    dynamic value = rk.GetValue("MachineKey");
                    if (value == null)
                        rk.SetValue("MachineKey", data, RegistryValueKind.String);

                    rk.Dispose();
                }

                if (data != null)
                    _internalMachineKey = data;

                return data as string;
            }
        }
        private static string _internalMachineKey = null;



        internal static string Signature { get; } = "S3VwdWFfMTAw";

        /// <summary>
        /// The URL where new versions are downloaded from
        /// </summary>
        public static string InstallerDownloadUrl { get; internal set; } =
            "https://markdownmonster.west-wind.com/download.aspx";


        internal static string PostFix = "*~~*";

        /// <summary>
        /// Encrypts sensitive user data using an internally generated
        /// encryption key.
        /// </summary>
        /// <param name="value">Value to encrypt</param>
        /// <param name="dontUseMachineKey">
        /// In shared cloud drive situations you might want to not use a machine key
        /// The default uses the UseMachineKeyForPasswords configuration setting.
        /// </param>
        /// <returns></returns>
        public static string EncryptString(string value, bool? dontUseMachineKey = null)
        {
            if (dontUseMachineKey == null)
                dontUseMachineKey = !mmApp.Configuration.UseMachineEncryptionKeyForPasswords;

            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // already encrypted
            if (value.EndsWith(PostFix))
                return value;

            var key = mkbase;
            if (!dontUseMachineKey.Value)
                key = mmApp.EncryptionMachineKey;

            var encrypted = Encryption.EncryptString(value, key) + PostFix;
            return encrypted;
        }
                                                                                                                                              
        public static string DecryptString(string encrypted, bool? dontUseMachineKey = null)
        {
            if (dontUseMachineKey == null)
                dontUseMachineKey = !mmApp.Configuration.UseMachineEncryptionKeyForPasswords;

            if (string.IsNullOrEmpty(encrypted))
                return string.Empty;

            if (!encrypted.EndsWith(PostFix))
                return encrypted;

            encrypted = encrypted.Replace(PostFix, "");

            var key = mkbase;
            if (!dontUseMachineKey.Value)
                key = mmApp.EncryptionMachineKey;

            var decoded = Encryption.DecryptString(encrypted, key);
            return decoded;
        }


        /// <summary>
    /// Url that is used to check for new version information
    /// </summary>
    public static string UpdateCheckUrl { get; internal set; }
        
        /// <summary>
        /// Static constructor to initialize configuration
        /// </summary>
        static mmApp()
        {
            Configuration = new ApplicationConfiguration();                
            Configuration.Initialize();            
        }


        /// <summary>
        /// Logs exceptions in the applications
        /// </summary>
        /// <param name="ex"></param>
        public static void Log(Exception ex)
        {
            ex = ex.GetBaseException();            
            Log(ex.Message,ex);
        }

        /// <summary>
        /// Logs messages to the log file
        /// </summary>
        /// <param name="msg"></param>
        public static void Log(string msg, Exception ex = null)
        {
            string exMsg = string.Empty;
            if (ex != null)
            {
                var version = mmApp.GetVersion();
                var winVersion = ComputerInfo.WinMajorVersion + "." + ComputerInfo.WinMinorVersion + "." +
                                 ComputerInfo.WinBuildLabVersion + " - " + CultureInfo.CurrentUICulture.IetfLanguageTag +
                                 " - " +
                                 ".NET " + ComputerInfo.GetDotnetVersion() + " - " +
                                 (Environment.Is64BitProcess ? "64 bit" : "32 bit");

                ex = ex.GetBaseException();
                exMsg =$@"
Markdown Monster v{version}
{winVersion}
---
{ex.Source}
{ex.StackTrace}
---------------------------


";
                SendBugReport(ex,msg);
            }

            var text = msg + exMsg;
            StringUtils.LogString(text, Path.Combine( Configuration.CommonFolder ,                               
                "MarkdownMonsterErrors.txt"), Encoding.UTF8);
        }

        public static void SetWorkingSet(int lnMaxSize, int lnMinSize)
        {
            try
            {
                Process loProcess = Process.GetCurrentProcess();
                loProcess.MaxWorkingSet = (IntPtr)lnMaxSize;
                loProcess.MinWorkingSet = (IntPtr)lnMinSize;
            }
            catch {}
        }


        /// <summary>
        /// Handles an Application level exception by logging the error
        /// to log, and displaying an error message to the user.
        /// Also sends the error to server if enabled.
        /// 
        /// Returns true if application should continue, false to exit.        
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static bool HandleApplicationException(Exception ex)
        {

            mmApp.Log("Last Resort Handler", ex);

            var msg = string.Format("Yikes! Something went wrong...\r\n\r\n{0}\r\n\r\n" +
                "The error has been recorded and written to a log file and you can\r\n" +
                "review the details or report the error via Help | Show Error Log\r\n\r\n" +
                "Do you want to continue?", ex.Message);

            var res = MessageBox.Show(msg, mmApp.ApplicationName + " Error",
                                                MessageBoxButton.YesNo,
                                                MessageBoxImage.Error);


            if (res.HasFlag(MessageBoxResult.No))
                return false;
            return true;
        }


        public static void SendBugReport(Exception ex, string msg = null)
        {                        
            var bug = new BugReport()
            {
                TimeStamp = DateTime.UtcNow,
                Message = ex.Message,                
                Product = "Markdown Monster",
                Version = mmApp.GetVersion(),      
                WinVersion = ComputerInfo.WinMajorVersion + "." + ComputerInfo.WinMinorVersion + "." + ComputerInfo.WinBuildLabVersion + " - " + CultureInfo.CurrentUICulture.IetfLanguageTag,
                StackTrace = (ex.Source + "\r\n\r\n" + ex.StackTrace).Trim()               
            };
            if (!string.IsNullOrEmpty(msg))
                bug.Message = msg + "\r\n" + bug.Message;
            
            new TaskFactory().StartNew(
                (bg) =>
                {                    
                    try
                    {
                        var temp = HttpUtils.JsonRequest<BugReport>(new HttpRequestSettings()
                        {
                            Url = mmApp.Configuration.BugReportUrl,
                            HttpVerb = "POST",
                            Content = bg,
                            Timeout = 3000
                        });
                    }
                    catch (Exception ex2)
                    {
                        // don't log with exception otherwise we get an endless loop
                        Log("Unable to report bug: " + ex2.Message);
                    }
                },bug);            
        }



        /// <summary>
        /// Sends usage information to server
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="data"></param>
        public static void SendTelemetry(string operation, string data = null)
        {
            bool isRegistered = UnlockKey.IsRegistered();
            int accessCount = mmApp.Configuration.ApplicationUpdates.AccessCount;

            if (!Configuration.SendTelemetry ||  (isRegistered && accessCount > 350))
                return;

            string version = GetVersion();
            
            var t = new Telemetry
            {
                Version = version,
                Registered = UnlockKey.IsRegistered(),
                Access = accessCount,
                Operation = operation,
                Time = Convert.ToInt32((DateTime.UtcNow - Started).TotalSeconds),
                Data = data
            };

            try
            {
                HttpUtils.JsonRequest<string>(new HttpRequestSettings()
                {
                    Url = mmApp.Configuration.TelemetryUrl,
                    HttpVerb = "POST",
                    Content = t,
                    Timeout = 1000
                });
            }
            catch (Exception ex2)
            {
                // don't log with exception otherwise we get an endless loop
                Log("Unable to send telemetry: " + ex2.Message);
            }
        }


        /// <summary>
        /// Gets the Markdown Monster Version as a string
        /// </summary>
        /// <returns></returns>
        public static string GetVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v.ToString();            
        }

        public static string GetVersionForDisplay()
        {
            return GetVersion().Replace(".0", "");
        }

        public static string GetVersionDate()
        {
            var fi = new FileInfo(Assembly.GetExecutingAssembly().Location);
            return fi.LastWriteTime.ToString("MMM d, yyyy");
        }


        /// <summary>
        /// Sets the light or dark theme for a form. Call before
        /// InitializeComponents().
        /// 
        /// We only support the dark theme now so this no longer relevant
        /// but left in place in case we decide to support other themes.
        /// </summary>
        /// <param name="theme"></param>
        /// <param name="window"></param>
        public static void SetTheme(Themes theme = Themes.Default,MetroWindow window = null)
        {
            if (theme == Themes.Default)
                theme = mmApp.Configuration.ApplicationTheme;

            //if (theme == Themes.Light)
            //{
            //    // get the current app style (theme and accent) from the application
            //    // you can then use the current theme and custom accent instead set a new theme
            //    Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);

            //    // now set the Green accent and dark theme
            //    ThemeManager.ChangeAppStyle(Application.Current,
            //        ThemeManager.GetAccent("Steel"),
            //        ThemeManager.GetAppTheme("BaseLight")); // or appStyle.Item1                
            //}
            //else
            //{
            //    // get the current app style (theme and accent) from the application
            //    // you can then use the current theme and custom accent instead set a new theme
            //    Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);

            //    // now set the highlight accent and dark theme
            //    ThemeManager.ChangeAppStyle(Application.Current,
            //        ThemeManager.GetAccent("Blue"),
            //        ThemeManager.GetAppTheme("BaseDark")); // or appStyle.Item1      
            //}

            if (window != null)
                SetThemeWindowOverride(window);            

        }

        /// <summary>
        /// Overrides specific theme colors in the window header
        /// </summary>
        /// <param name="window"></param>
        public static void SetThemeWindowOverride(MetroWindow window)
        {
            if (mmApp.Configuration.ApplicationTheme == Themes.Dark)
            {
                if (window != null)
                {
                    window.WindowTitleBrush = (SolidColorBrush) (new BrushConverter().ConvertFrom("#333333"));
                    window.NonActiveWindowTitleBrush = (Brush) window.FindResource("WhiteBrush");

                    var brush = App.Current.Resources["MenuSeparatorBorderBrush"] as SolidColorBrush;
                    App.Current.Resources["MenuSeparatorBorderBrush"] = (SolidColorBrush) new BrushConverter().ConvertFrom("#333333");
                    brush = App.Current.Resources["MenuSeparatorBorderBrush"] as SolidColorBrush;
                }
            }
            //else
            //{
            //    if (window != null)
            //    {
            //        // Need to fix this to show the accent color when switching
            //        //window.WindowTitleBrush = (Brush)window.FindResource("WhiteBrush");
            //        //window.NonActiveWindowTitleBrush = (Brush)window.FindResource("WhiteBrush");
            //    }
            //}
        }
    }


    /// <summary>
    /// Supported themes (not used any more)
    /// </summary>
    public enum Themes
    {
        Dark,
        Light,
        Default
    }


    public class BugReport
    {
        public DateTime TimeStamp { get; set; }
        public string Message { get; set; }
        public string Product { get; set; }
        public string Version { get; set; }
        public string WinVersion { get; set; }
        public string StackTrace { get; set; }
        
    }

    public class Telemetry
    {
        public string Version { get; set; }
        public bool Registered { get; set; }
        public string Operation { get; set; }
        public string Data { get; set; }
        public int Access { get; set; }
        public int Time { get; set; }
    }
}