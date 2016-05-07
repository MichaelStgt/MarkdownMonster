﻿using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Media;
using MahApps.Metro.Controls;
using MarkdownMonster;
using Westwind.Utilities;

namespace WebLogAddin
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class WebLogStart : MetroWindow
    {
        public WeblogAddinModel Model { get; set;  }


        #region Startup and Shutdown

        public WebLogStart()
        {
            Model = new WeblogAddinModel();
            
            Model.ActivePostMetadata = new WeblogPostMetadata();
            mmApp.SetTheme(mmApp.Configuration.ApplicationTheme);

            Model.Configuration = WeblogApp.Configuration;
            
            InitializeComponent();
            //mmApp.SetThemeWindowOverride(this);         

            DataContext = Model;

            Loaded += WebLogStart_Loaded;
            Closing += WebLogStart_Closing;
        }

        private void WebLogStart_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Model.LoadWebLognames();

            var markdown = Model.AppModel.ActiveEditor.GetMarkdown();
            Model.ActivePostMetadata = Model.Addin.GetPostConfigFromMarkdown(markdown);
        }


        private void WebLogStart_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // save settings
            WeblogApp.Configuration.Write();
        }

        #endregion

        #region Button Handlers
        private void ButtonPostBlog_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            
            // Update the Markdown document first
            Model.Addin.SetConfigInMarkdown(Model.ActivePostMetadata);

            WeblogApp.Configuration.LastWeblogAccessed = Model.ActivePostMetadata.WeblogName;

            var window = Model.AppModel.Window;

            window.ShowStatus("Uploading Blog post...");
            window.SetStatusIcon(FontAwesome.WPF.FontAwesomeIcon.Upload, Colors.Orange, true);
            try
            {                
                // Then send the post - it will re-read the new values
                if (Model.Addin.SendPost())
                    this.Close();

                Thread.Sleep(4000);
            }
            finally
            {
                window.ShowStatus("Blog post uploaded successfully.",5000);
                window.SetStatusIcon();
            }
        }

        private void ButtonNewPost_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string title = Model.NewTitle;
            string weblogName = Model.Configuration.LastWeblogAccessed;

            if (string.IsNullOrEmpty(title))
                return;

            // strip path of invalid characters
            var invalids = Path.GetInvalidFileNameChars();
            string filename = null;
            foreach (char c in invalids)
                filename = title.Replace(c, '-');

            var folder = Path.Combine(WeblogApp.Configuration.PostsFolder, filename);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            var outputFile = Path.Combine(folder, filename + ".md");

            // Create the new post by creating a file with title preset
            string newPostMarkdown = Model.Addin.NewWeblogPost(new WeblogPostMetadata()
            {
                Title = title,
                WeblogName = weblogName
            });
            File.WriteAllText(outputFile, newPostMarkdown);
            Model.AppModel.Window.OpenTab(outputFile);

            this.Close();
        }
        #endregion
    }
}