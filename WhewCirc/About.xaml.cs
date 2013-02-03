using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using System.Windows.Media.Imaging;
using System.IO.IsolatedStorage;

namespace WhewMap
{
    public partial class About : PhoneApplicationPage
    {
        bool inEmail;
        double appversion = 1.3;

        IsolatedStorageSettings settings;

        public About()
        {
            InitializeComponent();

            settings = IsolatedStorageSettings.ApplicationSettings;

            // Load initial settings
            if (settings.Contains("UseLocation"))
            {
                locationtoggle.IsChecked = settings["UseLocation"].ToString() == "true";
            }
            else
            {
                settings["UseLocation"] = "false";
                locationtoggle.IsChecked = false;
            }

            textBlock1.Text = "WHEWCIRC\nVersion "+ appversion +"\n\nCoded lovingly by\nJeff Stephens in St. Louis\n@jefftheman45";
            textBlock1.Text += "\n\nFor support, please contact\nwhewapps@gmail.com";
            textBlock1.Text += "\n\nThanks to Colin E. for his great article on\nanimation!";

            // Change logo if light theme
            Visibility isLight = (Visibility)Resources["PhoneLightThemeVisibility"]; // for light theme

            if (isLight == System.Windows.Visibility.Visible)
            {
                image1.Source = new BitmapImage(new Uri("/WhewCirc;component/icons/Marketplace_Device_173x173.png", UriKind.Relative));
            }
        }
        
        public void SendEmail(object sender, EventArgs e)
        {
            EmailComposeTask emailComposeTask = new EmailComposeTask();

            emailComposeTask.Subject = "WHEWCIRC Feedback (Version " + appversion +")";
            emailComposeTask.Body = "";
            emailComposeTask.To = "whewapps@gmail.com";

            if (!inEmail)
            {
                inEmail = true;
                emailComposeTask.Show();
            }
        }

        // Allow navigation to individual Pivot pages
        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            inEmail = false;
            string pagechoicestring;

            if (NavigationContext.QueryString.TryGetValue("page", out pagechoicestring))
            {
                int pagechoiceint = int.Parse(pagechoicestring);
                if (pagechoiceint >= AboutPivot.Items.Count)
                {
                    MessageBox.Show("Invalid page selection. Here's the About page.");
                }
                else
                {
                    AboutPivot.SelectedIndex = pagechoiceint;
                }
            }

            if (State.ContainsKey("AboutPivot"))
            {
                AboutPivot.SelectedIndex = Convert.ToInt32(State["AboutPivot"]);
            }
        }

        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            // if the user is navigating backwards, there's no way to return to this page instance
            if (e.NavigationMode != System.Windows.Navigation.NavigationMode.Back)
            {
                State["AboutPivot"] = AboutPivot.SelectedIndex.ToString();
            }
        }

        private void LocationOn(object sender, RoutedEventArgs e)
        {
            settings["UseLocation"] = "true";
        }

        private void LocationOff(object sender, RoutedEventArgs e)
        {
            settings["UseLocation"] = "false";
        }
    }
}