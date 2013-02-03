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
using System.IO.IsolatedStorage;
using System.Device.Location;
using Microsoft.Phone.Controls.Maps;

namespace WhewCirc
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Lists of stop times
        List<DateTime> s40;
        List<DateTime> ARmal;
        List<DateTime> skinker;
        List<DateTime> millbrook;
        List<DateTime> brookings;
        List<DateTime> mallinckrodt;

        IsolatedStorageSettings settings;

        GeoCoordinateWatcher loc;
        Pushpin curpin;

        List<Pushpin> pins;

        List<FrameworkElement> textBlocks;
        
        bool ready;
        int currentStop;
        List<Stops> source;
        bool useLoc = false;

        // The maximum proximity to a Circulator Stop that should be considered when auto-selecting
        double proximityRadius = 0.00517252;

        public class Stops
        {
            public string Name
            {
                get;
                set;
            }

            public string ShortName
            {
                get;
                set;
            }

            public GeoCoordinate Location
            {
                get;
                set;
            }
        }

        // Constructor
        public MainPage()
        {
            ready = false;
            
            settings = IsolatedStorageSettings.ApplicationSettings;
            
            InitializeComponent();

            // Set maps API key
            map.CredentialsProvider = new ApplicationIdCredentialsProvider("AnZIQpxMd4BEmvCR7_nBhqr2ZncAvMC3MAWyackuGfVag58r39tDhW_y23lumgQJ");

            // Populate location list
            source = new List<Stops>();
            source.Add(new Stops() { Name = "south forty", ShortName="south 40", Location = new GeoCoordinate(38.645327, -90.312952) });
            source.Add(new Stops() { Name = "mallinckrodt (to skinker)", ShortName = "mallinckrodt", Location = new GeoCoordinate(38.647021, -90.309522) });
            source.Add(new Stops() { Name = "skinker", ShortName = "skinker", Location = new GeoCoordinate(38.647654, -90.30133) });
            source.Add(new Stops() { Name = "millbrook", ShortName = "millbrook", Location = new GeoCoordinate(38.650172, -90.311331) });
            source.Add(new Stops() { Name = "brookings", ShortName = "brookings", Location = new GeoCoordinate(38.647923, -90.304025) });
            source.Add(new Stops() { Name = "mallinckrodt (to south forty)", ShortName = "mallinckrodt", Location = new GeoCoordinate(38.647021, -90.309522) });
            this.stopPicker.ItemsSource = source;

            // Add all text blocks to a List for animating
            textBlocks = new List<FrameworkElement>();
            textBlocks.Add(nextStopTime);
            textBlocks.Add(nextStopTime2);
            textBlocks.Add(nextStopTime3);

            // Create pins for each location
            pins = new List<Pushpin>();

            for (int i = 0; i < source.Count; i++)
            {
                Pushpin p = new Pushpin();
                p.Location = new GeoCoordinate(0, 0);
                pins.Add(p);
                map.Children.Add(p);
            }

            // Set up location services
            if (!settings.Contains("UseLocation"))
            {
                MessageBoxResult m = MessageBox.Show("Automatically select a circulator stop based on your physical location? You can change this later in Settings.",
                    "Location Service Request", MessageBoxButton.OKCancel);

                if (m == MessageBoxResult.OK)
                {
                    useLoc = true;
                    settings["UseLocation"] = "true";
                }
                else
                {
                    useLoc = false;
                    settings["UseLocation"] = "false";
                }
            }
            else if (settings["UseLocation"].ToString() == "true")
            {
                useLoc = true;
            }

            // Initialize current location pin and location watcher
            if (useLoc)
            {
                curpin = new Pushpin();
                curpin.Content = "current location";
                curpin.Background = new SolidColorBrush(Colors.Blue);
                curpin.Location = new GeoCoordinate(0, 0);
                map.Children.Add(curpin);

                loc = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
                updateLocation();
            }

            DateTime now = DateTime.Now;
            DateTime firstStop;
            int numOfStops;

            if (!settings.Contains("chosenStop"))
            {
                settings.Add("chosenStop", 0);
                currentStop = 0;
            }

            else
            {
                currentStop = Convert.ToInt32(settings["chosenStop"]);
            }

            // Determine whether it's a weekend or weekday and generate schedule accordingly
            if (now.DayOfWeek == DayOfWeek.Sunday || now.DayOfWeek == DayOfWeek.Saturday)
            {
                firstStop = new DateTime(now.Year, now.Month, now.Day, 12, 0, 0);
                numOfStops = 41;
            }

            else
            {
                firstStop = new DateTime(now.Year, now.Month, now.Day, 7, 40, 0);
                numOfStops = 54;
            }

            // South 40
            s40 = new List<DateTime>();

            s40.Add(firstStop);

            for (int i = 0; i < numOfStops; i++)
            {
                s40.Add(s40.Last().AddMinutes(20));
            }

            // AR Mallinckrodt
            ARmal = new List<DateTime>();

            foreach(DateTime d in s40) {
                ARmal.Add(d.AddMinutes(3));
            }

            // Skinker
            skinker = new List<DateTime>();

            foreach (DateTime d in ARmal)
            {
                skinker.Add(d.AddMinutes(3));
            }

            // Millbrook
            millbrook = new List<DateTime>();

            foreach (DateTime d in skinker)
            {
                millbrook.Add(d.AddMinutes(4));
            }

            // Brookings
            brookings = new List<DateTime>();

            foreach (DateTime d in millbrook)
            {
                brookings.Add(d.AddMinutes(4));
            }

            // Mallinckrodt
            mallinckrodt = new List<DateTime>();

            foreach (DateTime d in brookings)
            {
                mallinckrodt.Add(d.AddMinutes(3));
            }

            // Change colors if light theme
            Visibility isLight = (Visibility)Resources["PhoneLightThemeVisibility"]; // for light theme

            if (isLight == System.Windows.Visibility.Visible)
            {
                nextStopTime2.Foreground = new SolidColorBrush(Colors.Gray);
                nextStopTime3.Foreground = new SolidColorBrush(Colors.LightGray);
            }

            stopPicker.SelectedIndex = currentStop;
            ready = true;
            buildMap();
            updateTime();
        }

        private void buildMap()
        {
            // Center map
            GeoCoordinate center = new GeoCoordinate(38.6485, -90.307896);
            var bounds = new LocationRect(
                center.Latitude + proximityRadius / 2,
                center.Longitude - proximityRadius / 2,
                center.Latitude - proximityRadius / 2,
                center.Longitude + proximityRadius * .75);
            map.SetView(bounds);

            int loopcount = 0;

            // Drop pins
            foreach (Stops stop in source)
            {
                if (loopcount != 5)
                {
                    List<DateTime> times = nextTimes(loopcount);
                    pins[loopcount].Location = stop.Location;
                    pins[loopcount].Content = times[0].ToShortTimeString();

                    if (loopcount == 1)
                    {
                        List<DateTime> futureTimes = nextTimes(5);
                        pins[loopcount].Content = times[0].ToShortTimeString() + "\n" + futureTimes[0].ToShortTimeString();
                    }

                    pins[loopcount].MouseLeftButtonUp += new MouseButtonEventHandler(pushpin_click);
                }

                loopcount++;
            }

            map.Children.Remove(pins[2]);
            map.Children.Add(pins[2]);
        }

        void pushpin_click(object sender, MouseButtonEventArgs e)
        {
            Pushpin tappedPin = (Pushpin)sender;
            Dictionary<Pushpin,int> indexLookup = new Dictionary<Pushpin,int>();

            for (int i = 0; i < source.Count; i++)
            {
                indexLookup[pins[i]] = i;
            }

            if (indexLookup[tappedPin] == 1 || indexLookup[tappedPin] == 5) // special case for shared Mallinckrodt stop; choose stop with closest time
            {
                List<DateTime> stop1 = nextTimes(1);
                List<DateTime> stop5 = nextTimes(5);

                if (DateTime.Compare(stop1[0], stop5[0]) < 0)
                {
                    stopPicker.SelectedIndex = 1;
                }
                else
                {
                    stopPicker.SelectedIndex = 5;
                }
            }

            else
            {
                stopPicker.SelectedIndex = indexLookup[tappedPin];
            }
            circpivot.SelectedIndex = 0;
        } 

        private void ListPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ready)
            {
                // De-color previously selected pushpin
                pins[currentStop].Background = new SolidColorBrush(Colors.Black);
                pins[1].Background = new SolidColorBrush(Colors.Black);
                pins[5].Background = new SolidColorBrush(Colors.Black);

                currentStop = stopPicker.SelectedIndex;
                nextStopLabel.Visibility = System.Windows.Visibility.Visible;
                nextStopTime.Visibility = System.Windows.Visibility.Visible;
                updateTime();

                // Color selected pushpin
                if (currentStop == 5) // Special case for shared Mallinckrodt pin
                {
                    pins[1].Background = new SolidColorBrush((Color)Application.Current.Resources["PhoneAccentColor"]);
                }
                else // All other cases
                {
                    pins[currentStop].Background = new SolidColorBrush((Color)Application.Current.Resources["PhoneAccentColor"]);
                }

                if (!settings.Contains("chosenStop"))
                {
                    settings.Add("chosenStop", currentStop);
                }
                else
                {
                    settings["chosenStop"] = currentStop;
                }

                Peel(textBlocks, 170);
            }
        }

        public static void Peel(List<FrameworkElement> elementList, double initDelay)
        {
            var lastElement = elementList.Last();

            // iterate over all the elements, animating each of them
            double delay = initDelay;
            foreach (FrameworkElement element in elementList)
            {
                var sb = GetPeelAnimation(element, delay);

                // add a Completed event handler to the last element
                if (element.Equals(lastElement))
                {
                    sb.Completed += (s, e) =>
                    {
                        ;
                    };
                }

                sb.Begin();
                delay += 50;
            }
        }

        private static Storyboard GetPeelAnimation(FrameworkElement element, double delay)
        {
            Storyboard sb;

            var projection = new PlaneProjection()
            {
                CenterOfRotationX = -0.1
            };
            element.Projection = projection;

            // compute the angle of rotation required to make this element appear
            // at a 90 degree angle at the edge of the screen.
            var width = element.ActualWidth;
            var targetAngle = Math.Atan(1000 / (width / 2));
            targetAngle = targetAngle * 180 / Math.PI;

            // animate the projection
            sb = new Storyboard();
            sb.BeginTime = TimeSpan.FromMilliseconds(delay);
            sb.Children.Add(CreateAnimation(-(180 - targetAngle), 0, 0.3, "RotationY", projection));
            sb.Children.Add(CreateAnimation(23, 0, 0.3, "RotationZ", projection));
            sb.Children.Add(CreateAnimation(-23, 0, 0.3, "GlobalOffsetZ", projection));
            return sb;
        }

        private static DoubleAnimation CreateAnimation(double from, double to, double duration,
          string targetProperty, DependencyObject target)
        {
            var db = new DoubleAnimation();
            db.To = to;
            db.From = from;
            db.EasingFunction = new SineEase();
            db.Duration = TimeSpan.FromSeconds(duration);
            Storyboard.SetTarget(db, target);
            Storyboard.SetTargetProperty(db, new PropertyPath(targetProperty));
            return db;
        }

        public void updateTime()
        {
            if (ready)
            {
                // Update to next available stop at given location
                List<DateTime> times = nextTimes(currentStop);

                List<TextBlock> outputs = new List<TextBlock>();
                outputs.Add(nextStopTime);
                outputs.Add(nextStopTime2);
                outputs.Add(nextStopTime3);

                int loopcount = 0;

                foreach (DateTime t in times)
                {
                    if (t != DateTime.MinValue)
                    {
                        outputs[loopcount].Text = t.ToShortTimeString();
                    }
                    else
                    {
                        outputs[loopcount].Text = "";
                    }
                    loopcount++;
                }
            }
        }

        public List<DateTime> nextTimes(int stopIndex)
        {
            List<DateTime> nextTimes = new List<DateTime>();

            Dictionary<int, List<DateTime>> locationLookup = new Dictionary<int, List<DateTime>>();
            locationLookup[0] = s40;
            locationLookup[1] = ARmal;
            locationLookup[2] = skinker;
            locationLookup[3] = millbrook;
            locationLookup[4] = brookings;
            locationLookup[5] = mallinckrodt;

            List<DateTime> lookupList = locationLookup[stopIndex];

            bool done = false;
            int loopcount = 0;
            DateTime now = DateTime.Now;

            while (!done && loopcount < lookupList.Count)
            {
                if (now.CompareTo(lookupList[loopcount]) < 0)
                {
                    nextTimes.Add(lookupList[loopcount]);

                    if (loopcount + 1 < lookupList.Count)
                    {
                        nextTimes.Add(lookupList[loopcount + 1]);

                        if (loopcount + 2 < lookupList.Count)
                        {
                            nextTimes.Add(lookupList[loopcount + 2]);
                        }

                        else
                        {
                            nextTimes.Add(DateTime.MinValue);
                        }
                    }
                    else
                    {
                        nextTimes.Add(DateTime.MinValue);
                    }
                    done = true;
                }
                loopcount++;
            }
            
            return nextTimes;
        }

        void loc_statusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            if (useLoc)
            {
                if (loc.Permission == GeoPositionPermission.Denied)
                {
                    MessageBox.Show("You've disabled Location Services for your device, so automatic selection won't work.");
                    settings["UseLocation"] = false;
                    useLoc = false;
                }
                else if (e.Status == GeoPositionStatus.Ready)
                {
                    // Determine closest stop
                    GeoCoordinate curLoc = loc.Position.Location;
                    double minDist = proximityRadius + 1;
                    int stopIndex = -1;
                    int loopcount = 0;

                    foreach (Stops s in source)
                    {
                        if (Distance(s.Location, curLoc) < minDist)
                        {
                            minDist = Distance(s.Location, curLoc);
                            stopIndex = loopcount;
                        }
                        loopcount++;
                    }

                    if (minDist < proximityRadius)
                    {
                        stopPicker.SelectedIndex = stopIndex;
                    }

                    curpin.Location = loc.Position.Location;

                    loc.Stop();
                    locateProgress.Visibility = System.Windows.Visibility.Collapsed;
                    locateProgress.IsIndeterminate = false;
                }
            }
        }

        public void updateLocation()
        {
            // Update useLoc
            useLoc = settings["UseLocation"] == "true";
            if (useLoc)
            {
                locateProgress.IsIndeterminate = true;
                locateProgress.Visibility = System.Windows.Visibility.Visible;

                loc.StatusChanged -= loc_statusChanged;
                loc.StatusChanged += loc_statusChanged;
                loc.Start();
            }

            updateTime();
        }

        // Handle tombstoning
        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            // if the user is navigating backwards, there's no way to return to this page instance
            if (e.NavigationMode != System.Windows.Navigation.NavigationMode.Back)
            {
                State["CircPivot"] = circpivot.SelectedIndex.ToString();
                State["CircMapZoom"] = map.ZoomLevel.ToString();
                State["CircMapCenterLat"] = map.Center.Latitude.ToString();
                State["CircMapCenterLong"] = map.Center.Longitude.ToString();
            }
        }

        // Allow navigation to individual Pivot pages
        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Automatically select closest stop
            updateLocation();            
            string pagechoicestring;

            if (NavigationContext.QueryString.TryGetValue("page", out pagechoicestring))
            {
                int pagechoiceint = int.Parse(pagechoicestring);
                if (pagechoiceint >= circpivot.Items.Count)
                {
                    MessageBox.Show("Invalid navigation request. Here's the next circulator time.");
                }
                else
                {
                    circpivot.SelectedIndex = pagechoiceint;
                }
            }

            if (State.ContainsKey("CircPivot"))
            {
                circpivot.SelectedIndex = Convert.ToInt32(State["CircPivot"]);
            }

            if (State.ContainsKey("CircMapZoom"))
            {
                map.ZoomLevel = Convert.ToInt32(State["CircMapZoom"]);
            }

            if (State.ContainsKey("CircMapCenterLat") && State.ContainsKey("CircMapCenterLong"))
            {
                map.Center = new GeoCoordinate(Convert.ToDouble(State["CircMapCenterLat"]), Convert.ToDouble(State["CircMapCenterLong"]));
            }
        }

        // Handle menu buttons
        private void aboutHandler(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/About.xaml", UriKind.Relative));
        }

        private void settingsHandler(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/About.xaml?page=1", UriKind.Relative));
        }

        private double Distance(GeoCoordinate one, GeoCoordinate two)
        {
            double leg1 = Math.Pow((one.Latitude) - (two.Latitude), 2);
            double leg2 = Math.Pow((one.Longitude) - (two.Longitude), 2);
            return Math.Sqrt(leg1 + leg2);
        }

        private void nextStopLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            tempStopTime_sb.Begin();
            nextStopTime_sb.Begin();
        }
    }
}