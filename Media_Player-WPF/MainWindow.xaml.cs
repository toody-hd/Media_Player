using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Media_Player_WPF
{
    public partial class MainWindow : Window
    {
        #region Private Variables

        private bool mediaPlayerIsStoped = true;
        private bool mediaPlayerIsPaused = false;
        private bool userIsDraggingSlider = false;
        private DispatcherTimer timer = new DispatcherTimer();
        private Stopwatch sw = new Stopwatch();
        private BackgroundWorker leftOverlayBGW = new BackgroundWorker();
        private bool shiftKeyDown = false;
        private float subOffset = 0;
        private List<Subtitle> subtitleList = new List<Subtitle>();
        private TimeSpan _start;
        private TimeSpan _end;
        private string _sub;
        private TimeSpan subtitleOffset = new TimeSpan(0,0,0,0,300);
        private int subPos = 0;
        private bool _updateMoviePosition = true;
        private TimeSpan _moviePosition = new TimeSpan();
        private DispatcherTimer _activityTimer;
        private Point _inactiveMousePosition = new Point(0, 0);
        private bool _idleTimerInizialized = false;
        private Cursor _tempCursor;
        private bool showAnimation = false;
        private bool showSubtitle = true;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            Initializer();
        }

        private void Initializer()
        {
            //LeftInfoOverlay.Visibility = RightInfoOverlay.Visibility = SubtitleOverlay.Visibility = Visibility.Collapsed;
            LeftInfoOverlay.Text = RightInfoOverlay.Text = SubtitleOverlay.Text = SubtitleOverlay1.Text = SubtitleOverlay2.Text = SubtitleOverlay3.Text = TitleOverlay.Text = "";
            MediaPlayer.Volume = Properties.Settings.Default.Volume;
            sliVolume.Value = Properties.Settings.Default.Volume;
            MediaPlayer.IsMuted = Properties.Settings.Default.Mute;

            timer.Interval = TimeSpan.FromMilliseconds(20);
            timer.Tick += Timer_Tick;
            timer.Start();

            _tempCursor = this.Cursor;

            FileExecuted();
        }

        private class Subtitle
        {
            /// <summary>
            /// Gets the Start Time of the subtitle.
            /// </summary>
            public TimeSpan StartTime { get; set; }

            /// <summary>
            /// Gets the End Time of the subtitle.
            /// </summary>
            public TimeSpan EndTime { get; set; }

            /// <summary>
            /// Gets the Text of the subtitle.
            /// </summary>
            public string Text { get; set; }
        }

        private void FileExecuted()
        {
            if (!string.IsNullOrWhiteSpace(App.DirectOpenPath))
            {
                MediaPlayer.Source = new Uri(App.DirectOpenPath);
                Subtitle_Load(App.DirectOpenPath);
                LoadSubtitleMenuItem.IsEnabled = true;
                SubtitleButton.IsEnabled = true;
                Statusbar.IsEnabled = true;
                mediaPlayerIsStoped = false;
                mediaPlayerIsPaused = false;
                cbPlay.IsChecked = false;
                MediaPlayer.Play();
                SetDimension();
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                // hide the window before changing window style
                this.Visibility = Visibility.Collapsed;
                this.Topmost = true;
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                // re-show the window after changing style
                this.Visibility = Visibility.Visible;
                this.Topmost = false;
                Toolbar.Visibility = Visibility.Collapsed;
                InitializingActivity();
            }
            else
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ResizeMode = ResizeMode.CanResize;
                Toolbar.Visibility = Visibility.Visible;
                DeinitializingActivity();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if ((MediaPlayer.Source != null) && (MediaPlayer.NaturalDuration.HasTimeSpan))
            {
                if (!userIsDraggingSlider && !(mediaPlayerIsStoped))
                {
                    sliProgress.Minimum = 0;
                    sliProgress.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    sliProgress.Value = MediaPlayer.Position.TotalSeconds;
                }
                UpdateSubToShow();
            }
        }

        private void Open_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            LoadMovie();
        }

        private void Play_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (MediaPlayer != null) && (MediaPlayer.Source != null) && (mediaPlayerIsStoped || mediaPlayerIsPaused);
        }

        private void Play_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            LoadSubtitleMenuItem.IsEnabled = true;
            SubtitleButton.IsEnabled = true;
            Statusbar.IsEnabled = true;
            mediaPlayerIsStoped = false;
            mediaPlayerIsPaused = false;
            cbPlay.IsChecked = false;
            MediaPlayer.Play();
            SetDimension();
        }

        private void Pause_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !mediaPlayerIsStoped && !mediaPlayerIsPaused;
        }

        private void Pause_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            mediaPlayerIsPaused = true;
            cbPlay.IsChecked = true;
            MediaPlayer.Pause();
        }

        private void Stop_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !mediaPlayerIsStoped;
        }

        private void Stop_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MediaPlayer.Stop();
            sliProgress.Value = 0;
            lblProgressStatus.Text = "--:--:-- / --:--:--";
            SubtitleOverlay.Text = SubtitleOverlay1.Text = SubtitleOverlay2.Text = SubtitleOverlay3.Text = "";
            mediaPlayerIsStoped = true;
            SubtitleButton.IsEnabled = false;
            LoadSubtitleMenuItem.IsEnabled = false;
            Statusbar.IsEnabled = false;
            cbPlay.IsChecked = false;
            var _tempUri = MediaPlayer.Source;
            MediaPlayer.Source = null;
            MediaPlayer.Source = _tempUri;
        }

        private void SubtitleLoad_Click(object sender, RoutedEventArgs e)
        {
            Subtitle_Load(null);
        }

        private void Subtitle_Load(string subFound)
        {
            if (subFound == null)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog()
                {
                    //Filter = "Subtitle files (*.sub;*.srt)|*.sub;*.srt|All files (*.*)|*.*"
                    Filter = "Subtitle files (*.srt)|*.srt"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    subtitleOffset = new TimeSpan(0, 0, 0, 0, 0);
                    subOffset = 0;
                    LoadSub(openFileDialog.FileName);
                }
            }
            else
            {
                if (File.Exists(Path.ChangeExtension(subFound, "srt")))
                    LoadSub((Path.ChangeExtension(subFound, "srt")));
                //else if (File.Exists(Path.ChangeExtension(subFound, "sub")))
                //    LoadSub((Path.ChangeExtension(subFound, "sub")));
                else if (Directory.GetFiles(Path.GetDirectoryName(subFound), "*.srt").Length != 0)
                    LoadSub((Directory.GetFiles(Path.GetDirectoryName(subFound), "*.srt")[0]));
                //else if (File.Exists(Directory.GetFiles(Path.GetDirectoryName(subFound), "*.sub")[0]))
                //    LoadSub((Directory.GetFiles(Path.GetDirectoryName(subFound), "*.sub")[0]));
            }
        }

        private void LoadSub(string file)
        {
            subtitleList = new List<Subtitle>();
            bool _lineFound = false;
            foreach (var line in File.ReadAllLines(file))
            {
                if (line.Contains(" --> ") && line.Length == 29)
                {
                    _lineFound = true;
                    _start = TimeSpan.ParseExact(line.Substring(0, 12), @"hh\:mm\:ss\,fff", System.Globalization.CultureInfo.InvariantCulture);
                    _end = TimeSpan.ParseExact(line.Substring(17, 12), @"hh\:mm\:ss\,fff", System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (_lineFound && line.Length != 0)
                {
                    if (_sub == null)
                        _sub = line; //.Replace("<i>", "<Italic>").Replace("</i>", "</Italic>");
                    else
                        _sub = _sub + "<br>" + line;
                    //_sub = _sub + "\n" + line; //.Replace("<i>", "<Italic>").Replace("</i>", "</Italic>");
                }
                else if (line.Length == 0)
                {
                    subtitleList.Add(new Subtitle() { Text = _sub, StartTime = _start, EndTime = _end });
                    _sub = null;
                    _lineFound = false;
                }
            }
        }

        private void SliProgress_DragStarted(object sender, DragStartedEventArgs e)
        {
            userIsDraggingSlider = true;
        }

        private void SliProgress_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            userIsDraggingSlider = false;
            MediaPlayer.Position = TimeSpan.FromSeconds(sliProgress.Value);
            SubtitleOverlay.Text = SubtitleOverlay1.Text = SubtitleOverlay2.Text = SubtitleOverlay3.Text = "";
        }

        private void SliProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!userIsDraggingSlider && (TimeSpan.FromSeconds(sliProgress.Value) >= MediaPlayer.Position.Add(new TimeSpan(0, 0, 1)) || TimeSpan.FromSeconds(sliProgress.Value).Add(new TimeSpan(0, 0, 1)) <= MediaPlayer.Position))
            {
                MediaPlayer.Position = TimeSpan.FromSeconds(sliProgress.Value);
                SubtitleOverlay.Text = SubtitleOverlay1.Text = SubtitleOverlay2.Text = SubtitleOverlay3.Text = "";
            }
            lblProgressStatus.Text = TimeSpan.FromSeconds(sliProgress.Value).ToString(@"hh\:mm\:ss") + " / " + MediaPlayer.NaturalDuration;
        }

        private void UpdateSubToShow()
        {
            if (_updateMoviePosition)
            {
                _moviePosition = MediaPlayer.Position;
            }

            if (showSubtitle && subtitleList.Exists(x => (x.StartTime <= (_moviePosition + subtitleOffset) && x.EndTime >= (_moviePosition + subtitleOffset))))
            {
                _updateMoviePosition = false;
                //SubtitleOverlay.Text = subtitleList.Find(x => (x.StartTime <= (_moviePosition + subtitleOffset) && x.EndTime >= (_moviePosition + subtitleOffset))).Text;
                HtmlToXamlConvert.HtmlTextBoxProperties.SetHtmlText(SubtitleOverlay, subtitleList.Find(x => (x.StartTime <= (_moviePosition + subtitleOffset) && x.EndTime >= (_moviePosition + subtitleOffset))).Text);
                HtmlToXamlConvert.HtmlTextBoxProperties.SetHtmlText(SubtitleOverlay1, subtitleList.Find(x => (x.StartTime <= (_moviePosition + subtitleOffset) && x.EndTime >= (_moviePosition + subtitleOffset))).Text);
                HtmlToXamlConvert.HtmlTextBoxProperties.SetHtmlText(SubtitleOverlay2, subtitleList.Find(x => (x.StartTime <= (_moviePosition + subtitleOffset) && x.EndTime >= (_moviePosition + subtitleOffset))).Text);
                HtmlToXamlConvert.HtmlTextBoxProperties.SetHtmlText(SubtitleOverlay3, subtitleList.Find(x => (x.StartTime <= (_moviePosition + subtitleOffset) && x.EndTime >= (_moviePosition + subtitleOffset))).Text);
                _updateMoviePosition = true;
            }
            else
            {
                SubtitleOverlay.Text = "";
                SubtitleOverlay1.Text = "";
                SubtitleOverlay2.Text = "";
                SubtitleOverlay3.Text = "";
            }
        }

        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!mediaPlayerIsStoped)
            {
                if (shiftKeyDown)
                {
                    if (e.Delta > 0 && SubtitleOverlay.FontSize < 60)
                        SubtitleOverlay.FontSize += 1;
                    else if (e.Delta < 0 && SubtitleOverlay.FontSize > 25)
                        SubtitleOverlay.FontSize -= 1;
                    LeftInfoOverlay.Text = "Subtitle size : " + SubtitleOverlay.FontSize;
                }
                else if (sliVolume.IsEnabled)
                {
                    if (e.Delta > 0 && MediaPlayer.Volume < 1)
                        MediaPlayer.Volume += 0.01;
                    else if (e.Delta < 0 && MediaPlayer.Volume > 0)
                        MediaPlayer.Volume += -0.01;
                    LeftInfoOverlay.Text = "Volume : " + Convert.ToInt32(MediaPlayer.Volume * 100) + "%";
                    Properties.Settings.Default.Volume = MediaPlayer.Volume;
                    Properties.Settings.Default.Save();

                    sw.Restart();
                    leftOverlayBGW = null;
                    leftOverlayBGW = new BackgroundWorker();
                    leftOverlayBGW.DoWork += (ss, ee) =>
                    {
                        Thread.Sleep(1300);
                    };

                    leftOverlayBGW.RunWorkerCompleted += (ss, ee) =>
                    {
                        if (sw.Elapsed >= TimeSpan.FromMilliseconds(1300))
                            LeftInfoOverlay.Visibility = Visibility.Collapsed;
                    };

                    leftOverlayBGW.RunWorkerAsync();
                    LeftInfoOverlay.Visibility = Visibility.Visible;
                    (FindResource("DelayedFade") as Storyboard).Begin(LeftInfoOverlay);
                }
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.WindowState ^= WindowState.Maximized;
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (!mediaPlayerIsStoped)
            {
                if (shiftKeyDown && (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down))
                {
                    switch (e.Key)
                    {
                        case Key.Up:
                            if (subPos < 40)
                            {
                                subGrid.Margin = new Thickness(0, 0, 0, subGrid.Margin.Bottom + 1);
                                LeftInfoOverlay.Text = string.Format("Subtitle position : " + "{0:+0;-0;0}", (subPos += 1));
                            }
                            break;
                        case Key.Down:
                            if (subPos > -20)
                            {
                                subGrid.Margin = new Thickness(0, 0, 0, subGrid.Margin.Bottom - 1);
                                LeftInfoOverlay.Text = string.Format("Subtitle position : " + "{0:+0;-0;0}", (subPos -= 1));
                            }
                            break;
                        case Key.Left:
                            {
                                subtitleOffset -= new TimeSpan(0, 0, 0, 0, 500);
                                LeftInfoOverlay.Text = string.Format("Subtitle offset by : " + "{0:+0.0;-0.0;0}", (subOffset -= 0.5f));
                            }
                            break;
                        case Key.Right:
                            {
                                subtitleOffset += new TimeSpan(0, 0, 0, 0, 500);
                                LeftInfoOverlay.Text = string.Format("Subtitle offset by : " + "{0:+0.0;-0.0;0}", (subOffset += 0.5f));
                            }
                            break;
                    }
                    sw.Restart();
                    leftOverlayBGW = null;
                    leftOverlayBGW = new BackgroundWorker();
                    leftOverlayBGW.DoWork += (ss, ee) =>
                    {
                        Thread.Sleep(1300);
                    };

                    leftOverlayBGW.RunWorkerCompleted += (ss, ee) =>
                    {
                        if (sw.Elapsed >= TimeSpan.FromMilliseconds(1300))
                            LeftInfoOverlay.Visibility = Visibility.Collapsed;
                    };

                    leftOverlayBGW.RunWorkerAsync();

                    LeftInfoOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.LeftShift:
                            shiftKeyDown = false;
                            break;
                        case Key.Left:
                            MediaPlayer.Position -= TimeSpan.FromSeconds(10);
                            break;
                        case Key.Right:
                            MediaPlayer.Position += TimeSpan.FromSeconds(10);
                            break;
                        case Key.Space:
                            if (!mediaPlayerIsPaused)
                            {
                                mediaPlayerIsPaused = true;
                                cbPlay.IsChecked = true;
                                MediaPlayer.Pause();
                            }
                            else
                            {
                                SubtitleButton.IsEnabled = true;
                                mediaPlayerIsPaused = false;
                                cbPlay.IsChecked = false;
                                MediaPlayer.Play();
                            }
                            break;
                        case Key.Escape:
                            if (this.WindowState == WindowState.Maximized)
                                this.WindowState = WindowState.Normal;
                            break;
                        case Key.S:
                            {
                                showSubtitle = !showSubtitle;
                            }
                            break;
                    }
                }
            (FindResource("DelayedFade") as Storyboard).Begin(LeftInfoOverlay);


                /*
                if(e.Key == Key.Left)
                    MediaPlayer.Position -= TimeSpan.FromSeconds(10);
                else if(e.Key == Key.Right)
                    MediaPlayer.Position += TimeSpan.FromSeconds(10);
                if(e.Key == Key.Space)
                {
                    if (mediaPlayerIsPlaying)
                    {
                        mediaPlayerIsPlaying = false;
                        MediaPlayer.Pause();
                    }
                    else
                    {
                        mediaPlayerIsPlaying = true;
                        MediaPlayer.Play();
                    }

                }

                if (e.Key == Key.LeftShift)
                    shiftKeyDown = false;
                if (shiftKeyDown && (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down))
                {
                    sw.Restart();
                    leftOverlayBGW = null;
                    leftOverlayBGW = new BackgroundWorker();
                    leftOverlayBGW.DoWork += (ss, ee) =>
                    {
                        Thread.Sleep(1300);
                    };

                    leftOverlayBGW.RunWorkerCompleted += (ss, ee) =>
                    {
                        if (sw.Elapsed >= TimeSpan.FromMilliseconds(1300))
                            LeftInfoOverlay.Visibility = Visibility.Collapsed;
                    };

                    leftOverlayBGW.RunWorkerAsync();

                    LeftInfoOverlay.Visibility = Visibility.Visible;

                    if (e.Key == Key.Right)
                    {
                        subtitleOffset += new TimeSpan(0, 0, 0, 0, 500);
                        LeftInfoOverlay.Text = string.Format("Subtitle offset by : " + "{0:+0.0;-0.0;0}", (subOffset += 0.5f));
                    }
                    else if (e.Key == Key.Left)
                    {
                        subtitleOffset -= new TimeSpan(0, 0, 0, 0, 500);
                        LeftInfoOverlay.Text = string.Format("Subtitle offset by : " + "{0:+0.0;-0.0;0}", (subOffset -= 0.5f));
                    }
                    else if (e.Key == Key.Up && subPos < 40)
                    {
                        SubtitleOverlay.Margin = new Thickness(0, 0, 0, SubtitleOverlay.Margin.Bottom + 1);
                        LeftInfoOverlay.Text = string.Format("Subtitle position : " + "{0:+0;-0;0}", (subPos += 1));
                    }
                    else if(e.Key == Key.Down && subPos > -20)
                    {
                        SubtitleOverlay.Margin = new Thickness(0, 0, 0, SubtitleOverlay.Margin.Bottom - 1);
                        LeftInfoOverlay.Text = string.Format("Subtitle position : " + "{0:+0;-0;0}", (subPos -= 1));
                    }
                    (FindResource("DelayedFade") as Storyboard).Begin(LeftInfoOverlay);
                    */
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!mediaPlayerIsStoped)
            {
                if (e.Key == Key.LeftShift)
                    shiftKeyDown = true;
                if (e.Key == Key.LeftShift && (e.Key == Key.Left || e.Key == Key.Right))
                    if(e.Key == Key.Left)
                    {
                        subtitleOffset -= new TimeSpan(0, 0, 0, 0, 500);
                        LeftInfoOverlay.Text = string.Format("Subtitle offset by : " + "{0:+0.0;-0.0;0}", (subOffset -= 0.5f));
                    }
                    else
                    {
                        subtitleOffset += new TimeSpan(0, 0, 0, 0, 500);
                        LeftInfoOverlay.Text = string.Format("Subtitle offset by : " + "{0:+0.0;-0.0;0}", (subOffset += 0.5f));
                    }
            }
        }

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (!mediaPlayerIsStoped)
            {
                this.Title = Path.GetFileNameWithoutExtension(MediaPlayer.Source.LocalPath);
                SetDimension();
                TitleOverlay.Text = Path.GetFileNameWithoutExtension(MediaPlayer.Source.LocalPath);

                leftOverlayBGW = new BackgroundWorker();
                leftOverlayBGW.DoWork += (ss, ee) =>
                {
                    Thread.Sleep(1300);
                };

                leftOverlayBGW.RunWorkerCompleted += (ss, ee) =>
                {
                    TitleOverlay.Visibility = Visibility.Collapsed;
                };

                leftOverlayBGW.RunWorkerAsync();
                (FindResource("DelayedFade") as Storyboard).Begin(TitleOverlay);
            }
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Stop();
            sliProgress.Value = 0;
            lblProgressStatus.Text = "--:--:-- / --:--:--";
            SubtitleOverlay.Text = SubtitleOverlay1.Text = SubtitleOverlay2.Text = SubtitleOverlay3.Text = "";
            LoadSubtitleMenuItem.IsEnabled = false;
            SubtitleButton.IsEnabled = false;
            mediaPlayerIsStoped = true;
            Statusbar.IsEnabled = false;
            cbPlay.IsChecked = false;
            var _tempUri = MediaPlayer.Source;
            MediaPlayer.Source = null;
            MediaPlayer.Source = _tempUri;
            MediaPlayer.Position = new TimeSpan();
        }

        private void LoadMovie()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "Media files (*.mkv,*.mp4,*.avi)|*.mkv;*.mp4;*.avi"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                MediaPlayer.Source = new Uri(openFileDialog.FileName);
                Subtitle_Load(openFileDialog.FileName);
                mediaPlayerIsStoped = false;
                SubtitleButton.IsEnabled = true;
                LoadSubtitleMenuItem.IsEnabled = true;
                Statusbar.IsEnabled = true;
                MediaPlayer.Play();
                SetDimension();
            }
        }

        private void LoadMovie_Click(object sender, RoutedEventArgs e)
        {
            LoadMovie();
        }

        private void CloseApplication_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void InitializingActivity()
        {
            InputManager.Current.PreProcessInput += OnActivity;
            _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.8), IsEnabled = true };
            _activityTimer.Tick += OnInactivity;
            _idleTimerInizialized = true;
        }

        private void DeinitializingActivity()
        {
            if (_idleTimerInizialized)
            {
                InputManager.Current.PreProcessInput -= OnActivity;
                _activityTimer.Tick -= OnInactivity;
                _idleTimerInizialized = false;
            }
        }

        private void OnInactivity(object sender, EventArgs e)
        {
            _activityTimer.Stop();
            // remember mouse position
            _inactiveMousePosition = Mouse.GetPosition(MediaPlayerGrid);
            
            // set UI on inactivity
            Cursor = Cursors.None;

            /*
            BackgroundWorker bg = new BackgroundWorker();
            bg.DoWork += (ss, ee) =>
            {
                Thread.Sleep(300);
            };

            bg.RunWorkerCompleted += (ss, ee) =>
            {
                Statusbar.Visibility = Visibility.Collapsed;
            };

            bg.RunWorkerAsync();
            */

            (FindResource("Fade") as Storyboard).Begin(Statusbar);
            showAnimation = true;
        }

        private void OnActivity(object sender, PreProcessInputEventArgs e)
        {
            InputEventArgs inputEventArgs = e.StagingItem.Input;

            if (inputEventArgs is MouseEventArgs || inputEventArgs is KeyboardEventArgs)
            {
                if (e.StagingItem.Input is MouseEventArgs mouseEventArgs)
                {
                    // no button is pressed and the position is still the same as the application became inactive
                    if (mouseEventArgs.LeftButton == MouseButtonState.Released &&
                        mouseEventArgs.RightButton == MouseButtonState.Released &&
                        mouseEventArgs.MiddleButton == MouseButtonState.Released &&
                        mouseEventArgs.XButton1 == MouseButtonState.Released &&
                        mouseEventArgs.XButton2 == MouseButtonState.Released &&
                        _inactiveMousePosition == mouseEventArgs.GetPosition(MediaPlayerGrid))
                        return;
                }

                // set UI on activity
                Cursor = _tempCursor;
                if (showAnimation)
                {
                    (FindResource("Show") as Storyboard).Begin(Statusbar);
                    showAnimation = false;
                }
                //Statusbar.Visibility = Visibility.Visible;
                _activityTimer.Stop();
                _activityTimer.Start();
            }
        }

        private void SetDimension()
        {
            if (MediaPlayer.NaturalVideoHeight < MediaPlayer.NaturalVideoWidth)
            {
                this.Width = (SystemParameters.WorkArea.Width * 0.85f);
                this.Height = (MediaPlayer.NaturalVideoHeight * (this.Width / MediaPlayer.NaturalVideoWidth)) + 56;
                this.Left = (SystemParameters.WorkArea.Width - this.Width) / 2 + SystemParameters.WorkArea.Left;
                this.Top = (SystemParameters.WorkArea.Height - this.Height) / 2 + SystemParameters.WorkArea.Top;
            }
        }

        private void SliVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!mediaPlayerIsStoped)
            {
                LeftInfoOverlay.Text = "Volume : " + Convert.ToInt32(MediaPlayer.Volume * 100) + "%";
                Properties.Settings.Default.Volume = MediaPlayer.Volume;
                Properties.Settings.Default.Save();

                sw.Restart();
                leftOverlayBGW = null;
                leftOverlayBGW = new BackgroundWorker();
                leftOverlayBGW.DoWork += (ss, ee) =>
                {
                    Thread.Sleep(1300);
                };

                leftOverlayBGW.RunWorkerCompleted += (ss, ee) =>
                {
                    if (sw.Elapsed >= TimeSpan.FromMilliseconds(1300))
                        LeftInfoOverlay.Visibility = Visibility.Collapsed;
                };

                leftOverlayBGW.RunWorkerAsync();
                LeftInfoOverlay.Visibility = Visibility.Visible;
                (FindResource("DelayedFade") as Storyboard).Begin(LeftInfoOverlay);
            }
        }
        
        private void CbPlay_Click(object sender, RoutedEventArgs e)
        {
            if (cbPlay.IsChecked == true)
            {
                mediaPlayerIsPaused = true;
                MediaPlayer.Pause();
            }
            else
            {
                mediaPlayerIsPaused = false;
                MediaPlayer.Play();
            }
        }

        private void MediaPlayerGrid_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            string[] link = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (link.Length == 1 && (link[0].EndsWith(".mkv") || link[0].EndsWith(".mp4") || link[0].EndsWith(".avi")))
                e.Effects = DragDropEffects.Copy;
            else if(link.Length == 1 && !mediaPlayerIsStoped && link[0].EndsWith(".srt"))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void MediaPlayerGrid_Drop(object sender, DragEventArgs e)
        {
            string[] link = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (link[0].EndsWith(".srt"))
            {
                subtitleOffset = new TimeSpan(0, 0, 0, 0, 0);
                subOffset = 0;
                Subtitle_Load(link[0]);
            }
            else
            {
                MediaPlayer.Source = new Uri(link[0]);
                Subtitle_Load(link[0]);
                mediaPlayerIsStoped = false;
                SubtitleButton.IsEnabled = true;
                LoadSubtitleMenuItem.IsEnabled = true;
                Statusbar.IsEnabled = true;
                MediaPlayer.Play();
                SetDimension();
            }
        }

        private void CbVolume_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Mute = CbVolume.IsChecked ?? false;
            Properties.Settings.Default.Save();
        }
    }
}
