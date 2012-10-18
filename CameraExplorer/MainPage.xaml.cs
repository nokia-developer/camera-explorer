﻿using Microsoft.Devices;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Navigation;
using Windows.Phone.Media.Capture;

namespace CameraExplorer
{
    public partial class MainPage : PhoneApplicationPage
    {
        private CameraExplorer.DataContext _dataContext = CameraExplorer.DataContext.Singleton;
        private ProgressIndicator _progressIndicator = new ProgressIndicator();
        private bool _capturing = false;

        public MainPage()
        {
            InitializeComponent();

            ApplicationBarMenuItem menuItem = new ApplicationBarMenuItem();
            menuItem.Text = "about";
            menuItem.IsEnabled = false;
            ApplicationBar.MenuItems.Add(menuItem);
            menuItem.Click += new EventHandler(aboutMenuItem_Click);

            DataContext = _dataContext;

            _progressIndicator.IsIndeterminate = true;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (_dataContext.Device == null)
            {
                ShowProgress("Initializing camera...");

                await InitializeCamera(CameraSensorLocation.Back);

                HideProgress();
            }

            videoBrush.RelativeTransform = new CompositeTransform()
            {
                CenterX = 0.5,
                CenterY = 0.5,
                Rotation = _dataContext.Device.SensorLocation == CameraSensorLocation.Back ?
                    _dataContext.Device.SensorRotationInDegrees : - _dataContext.Device.SensorRotationInDegrees
            };

            videoBrush.SetSource(_dataContext.Device);

            overlayComboBox.Opacity = 1;

            SetScreenButtonsEnabled(true);
            SetCameraButtonsEnabled(true);

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            overlayComboBox.Opacity = 0;

            SetScreenButtonsEnabled(false);
            SetCameraButtonsEnabled(false);

            base.OnNavigatingFrom(e);
        }

        private void SetScreenButtonsEnabled(bool enabled)
        {
            foreach (ApplicationBarIconButton b in ApplicationBar.Buttons)
            {
                b.IsEnabled = enabled;
            }

            foreach (ApplicationBarMenuItem m in ApplicationBar.MenuItems)
            {
                m.IsEnabled = enabled;
            }
        }

        private void SetCameraButtonsEnabled(bool enabled)
        {
            if (enabled)
            {
                Microsoft.Devices.CameraButtons.ShutterKeyHalfPressed += ShutterKeyHalfPressed;
                Microsoft.Devices.CameraButtons.ShutterKeyPressed += ShutterKeyPressed;
            }
            else
            {
                Microsoft.Devices.CameraButtons.ShutterKeyHalfPressed -= ShutterKeyHalfPressed;
                Microsoft.Devices.CameraButtons.ShutterKeyPressed -= ShutterKeyPressed;
            }
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private async void sensorButton_Click(object sender, EventArgs e)
        {
            SetScreenButtonsEnabled(false);
            SetCameraButtonsEnabled(false);

            ShowProgress("Initializing camera...");

            videoBrush.Opacity = 0.25;

            overlayComboBox.Opacity = 0;

            CameraSensorLocation currentSensorLocation = _dataContext.Device.SensorLocation;

            _dataContext.Device.Dispose();
            _dataContext.Device = null;

            IReadOnlyList<CameraSensorLocation> sensorLocations = PhotoCaptureDevice.AvailableSensorLocations;

            if (currentSensorLocation == sensorLocations[1])
            {
                await InitializeCamera(sensorLocations[0]);
            }
            else
            {
                await InitializeCamera(sensorLocations[1]);
            }

            videoBrush.RelativeTransform = new CompositeTransform()
            {
                CenterX = 0.5,
                CenterY = 0.5,
                Rotation = _dataContext.Device.SensorLocation == CameraSensorLocation.Back ?
                           _dataContext.Device.SensorRotationInDegrees : _dataContext.Device.SensorRotationInDegrees + 180
            };

            videoBrush.SetSource(_dataContext.Device);
            videoBrush.Opacity = 1;

            overlayComboBox.Opacity = 1;

            HideProgress();

            SetScreenButtonsEnabled(true);
            SetCameraButtonsEnabled(true);
        }

        private async void captureButton_Click(object sender, EventArgs e)
        {
            await AutoFocus();

            await Capture();
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.Relative));
        }

        private void ShowProgress(String msg)
        {
            _progressIndicator.Text = msg;
            _progressIndicator.IsVisible = true;

            SystemTray.SetProgressIndicator(this, _progressIndicator);
        }

        private void HideProgress()
        {
            _progressIndicator.IsVisible = false;

            SystemTray.SetProgressIndicator(this, _progressIndicator);
        }

        private async Task InitializeCamera(CameraSensorLocation sensorLocation)
        {
            Windows.Foundation.Size initialResolution = new Windows.Foundation.Size(640, 480);
            Windows.Foundation.Size previewResolution = new Windows.Foundation.Size(640, 480);
            Windows.Foundation.Size captureResolution = new Windows.Foundation.Size(640, 480);

            PhotoCaptureDevice d = await PhotoCaptureDevice.OpenAsync(sensorLocation, initialResolution);

            await d.SetPreviewResolutionAsync(previewResolution);
            await d.SetCaptureResolutionAsync(captureResolution);

            d.SetProperty(KnownCameraGeneralProperties.EncodeWithOrientation, d.SensorLocation == CameraSensorLocation.Back ? d.SensorRotationInDegrees : -d.SensorRotationInDegrees);

            _dataContext.Device = d;
        }

        private async Task AutoFocus()
        {
            if (!_capturing && PhotoCaptureDevice.IsFocusSupported(_dataContext.Device.SensorLocation))
            {
                SetScreenButtonsEnabled(false);
                SetCameraButtonsEnabled(false);

                await _dataContext.Device.FocusAsync();

                SetScreenButtonsEnabled(true);
                SetCameraButtonsEnabled(true);

                _capturing = false;
            }
        }

        private async Task Capture()
        {
            if (!_capturing)
            {
                _capturing = true;

                MemoryStream stream = new MemoryStream();

                CameraCaptureSequence sequence = _dataContext.Device.CreateCaptureSequence(1);
                sequence.Frames[0].CaptureStream = stream.AsOutputStream();

                await _dataContext.Device.PrepareCaptureSequenceAsync(sequence);
                await sequence.StartCaptureAsync();

                _dataContext.ImageStream = stream;

                _capturing = false;

                NavigationService.Navigate(new Uri("/PreviewPage.xaml", UriKind.Relative));
            }
        }

        private async void ShutterKeyHalfPressed(object sender, EventArgs e)
        {
            await AutoFocus();
        }

        private async void ShutterKeyPressed(object sender, EventArgs e)
        {
            await Capture();
        }
    }
}