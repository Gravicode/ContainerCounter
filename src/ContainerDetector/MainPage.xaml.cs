using ContainerDetector.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ContainerDetector
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {

        static int TotalCount = 0;
        static Tracker tracker;
        private BoundingBoxRenderer m_bboxRenderer = null;
        truckModel model1;

        // Model
        private ObjectDetection model = null;
        // File name of the ONNX model. This must be in the Assets folder for the project
        private string modelFileName = "truck.onnx";

        #region framecapture

        private MediaFrameSource _source;
        private FrameRenderer _frameRenderer;
        private bool _streaming = false;

        #endregion



        /// <summary>
        /// Load and create the model from the .onnx file
        /// </summary>
        /// <returns></returns>
        private async Task LoadModelAsync()
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/{modelFileName}"));
            // Create the model from the file
            // IMPORTANT: Change `Model.CreateModel` to match the class and methods in the
            //   .cs file generated from the ONNX model
            string fname = $@"Assets\{modelFileName}";
            StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFile modelFile = await InstallationFolder.GetFileAsync(fname);
            if (File.Exists(file.Path))
            {
                //"mask", "nomask"
                model = new ObjectDetection(new string[] { "Container", "Truck Container" });//await ONNXModel.CreateONNXModel(file);
                await model.Init(modelFile);
                model1 = await truckModel.CreateFromStreamAsync(modelFile);
            }

        }

        /// <summary>
        /// Process the output returned by the model
        /// </summary>
        /// <param name="evalOutput"></param>
        /// <returns></returns>
        async Task ProcessOutputAsync(IList<PredictionModel> predictions)//ONNXModelOutput evalOutput
        {
            //filter only truck
            var onlyTrucks = predictions.Where(x => x.TagName == "Truck Container").ToList();
            if (onlyTrucks != null && onlyTrucks.Count > 0)
            {
                m_bboxRenderer.Render(onlyTrucks);

                var res = tracker.DoTracking(onlyTrucks);
                m_bboxRenderer.RenderTrail(ref tracker);
                TotalCount += res.counter;
                TxtCounter.Text = $"counter : {TotalCount}";
            }
            /*
            // Get the label and loss from the output
            string label = evalOutput.classLabel.GetAsVectorView()[0];
            string loss = (evalOutput.loss[0][label] * 100.0f).ToString("#0.00");
            // Format the output string
            string score = $"{label} - {loss}";

            // Display the score
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    this.Score = score;
                }
            );*/
        }

        private void VideoPreview_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            float previewAspectRatio = (float)(VideoPreview.ActualWidth / VideoPreview.ActualHeight);
            var cameraAspectRatio = previewAspectRatio;
            UIOverlayCanvas1.Width = cameraAspectRatio >= previewAspectRatio ? VideoPreview.ActualWidth : VideoPreview.ActualHeight * cameraAspectRatio;
            UIOverlayCanvas1.Height = cameraAspectRatio >= previewAspectRatio ? VideoPreview.ActualWidth / cameraAspectRatio : VideoPreview.ActualHeight;
           
            m_bboxRenderer.ResizeContent(e);
        }


        // For capturing video from the camera and displaying a preview
        MediaCapture mediaCapture;
        bool isPreviewing;
        DisplayRequest displayRequest = new DisplayRequest();



        // Frame reader for extracting frames from the video
        MediaFrameReader frameReader;
        int processingFlag;

        // Handle property changes
        public event PropertyChangedEventHandler PropertyChanged;

        string score;
        public string Score
        {
            get => this.score;
            set => this.SetProperty(ref this.score, value);
        }

        /// <summary>
        /// Sets <paramref name="propertyName"/> to a <paramref name="value"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storage"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            storage = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handle OnLoad event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitAll();
        }

       
        public float xWidth { get; set; } = 1200f;
        public float xHeight { get; set; } = 900f;
        async Task InitAll()
        {
            if (tracker == null)
            {
                //this.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                //this.Arrange(new Windows.Foundation.Rect(0, 0, this.DesiredSize.Width, this.DesiredSize.Height));
                //ResizeScreen(null);
                tracker = new Tracker(new BoundingBox(0, 0, 1200f, 900f));
                //add sample region (top and bottom)
                tracker.AddRegion(new BoundingBox(0, 0, xWidth, xHeight / 2));
                tracker.AddRegion(new BoundingBox(0, xHeight / 2, xWidth, xHeight / 2));
            }
            // Load the model
            await this.LoadModelAsync();

            _frameRenderer = new FrameRenderer(VideoPreview);


            // Start the camera video preview
            await StartPreviewAsync();
        }

        /// <summary>
        /// Try to retrieve the information for the camera by panel
        /// </summary>
        /// <param name="desiredPanel"></param>
        /// <returns></returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(
                x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }




        /// <summary>
        /// Start the video preview
        /// </summary>
        /// <returns></returns>
        private async Task StartPreviewAsync()
        {
            try
            {
                // Try to get the rear camera
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);
                var settings = new MediaCaptureInitializationSettings { 
                    VideoDeviceId = cameraDevice.Id,

                    // This media capture has exclusive control of the source.
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,

                    // Set to CPU to ensure frames always contain CPU SoftwareBitmap images,
                    // instead of preferring GPU D3DSurface images.
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,

                    // Capture only video. Audio device will not be initialized.
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                };
                // Setup video capture from the camera
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync(settings);
                displayRequest.RequestActive();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;

                // Set up the FrameReader to capture frames from the camera video
                var frameSource = this.mediaCapture.FrameSources.Where(
                    source => source.Value.Info.SourceKind == MediaFrameSourceKind.Color)
                    .First();
                //this.frameReader = await this.mediaCapture.CreateFrameReaderAsync(frameSource.Value);

                _source = frameSource.Value;

                if (_source != null)
                {
                    // Ask the MediaFrameReader to use a subtype that we can render.
                    string requestedSubtype = FrameRenderer.GetSubtypeForFrameReader(_source.Info.SourceKind, _source.CurrentFormat);
                    if (requestedSubtype != null)
                    {
                        this.frameReader = await mediaCapture.CreateFrameReaderAsync(_source, requestedSubtype);

                        // Set up handler for frames
                        this.frameReader.FrameArrived += OnFrameArrived;
                        // Start the FrameReader
                       
                        MediaFrameReaderStartStatus result = await this.frameReader.StartAsync();
                        //_logger.Log($"Start reader with result: {result}");

                        if (result == MediaFrameReaderStartStatus.Success)
                        {
                            _streaming = true;
                           
                        }
                        //_logger.Log($"Reader created on source: {_source.Info.Id}");
                    }
                    else
                    {
                        //_logger.Log($"Cannot render current format on source: {_source.Info.Id}");
                    }
                }

               
            }
            catch (UnauthorizedAccessException)
            {
                // Display an error if the user denied access to the camera in privacy settings
                ContentDialog unauthorizedMsg = new ContentDialog()
                {
                    Title = "No access",
                    Content = "The app was denied access to the camera",
                    CloseButtonText = "OK"
                };
                await unauthorizedMsg.ShowAsync();
                return;
            }

            try
            {
                // Wire up the video capture to the CaptureElement to display the video preview
                //VideoPreview.Source = mediaCapture;
                //await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (System.IO.FileLoadException)
            {
                mediaCapture.CaptureDeviceExclusiveControlStatusChanged += MediaCaptureCaptureDeviceExclusiveControlStatusChanged;
            }

        }

        /// <summary>
        /// Handler for exclusive control events for the camera
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void MediaCaptureCaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
        {
            if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
            {
                ContentDialog accessMsg = new ContentDialog()
                {
                    Title = "No access",
                    Content = "Another app has exclusive access",
                    CloseButtonText = "OK"
                };
            }
            else if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !isPreviewing)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await StartPreviewAsync();
                });
            }
        }

        /// <summary>
        /// Clean up the camera access/preview
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync()
        {
            if (mediaCapture != null)
            {
                if (isPreviewing)
                {
                    //await mediaCapture.StopPreviewAsync();
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    VideoPreview.Source = null;
                    if (displayRequest != null)
                    {
                        displayRequest.RequestRelease();
                    }

                    mediaCapture.Dispose();
                    mediaCapture = null;
                });
            }

        }

        /// <summary>
        /// Handler for application suspend
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ApplicationSuspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                await CleanupCameraAsync();
                deferral.Complete();
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            m_bboxRenderer = new BoundingBoxRenderer(UIOverlayCanvas1);
            // Event handlers
            Application.Current.Suspending += ApplicationSuspending;
            this.Loaded += OnLoaded;
        }
        /// <summary>
        /// Processes media frames as they arrive
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        async void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            if (Interlocked.CompareExchange(ref this.processingFlag, 1, 0) == 0)
            {
                try
                {
                    using (var frame = sender.TryAcquireLatestFrame())
                    {
                        var bmp = _frameRenderer.ProcessFrame(frame);
                        var videoFrame = VideoFrame.CreateWithSoftwareBitmap(bmp);
                        if (videoFrame != null)
                        {
                            // If there is a frame, set it as input to the model
                            truckInput input = new truckInput();
                            input.data = ImageFeatureValue.CreateFromVideoFrame(videoFrame);

                            // Evaluate the input data
                            var outData = await model1.EvaluateAsync(input);
                            var evalOutput = model.PredictImageAsync2(outData.model_outputs0);
                            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                               async () =>
                               {
                                   await this.ProcessOutputAsync(evalOutput);
                               });


                            /*
                            // Evaluate the input data
                            var evalOutput = await model.PredictImageAsync(videoFrame);
                            //await model.EvaluateAsync(input);
                            // Do something with the model output
                            await this.ProcessOutputAsync(evalOutput);*/

                        }
                        /*
                        using (var videoFrame = frame.VideoMediaFrame?.GetVideoFrame())
                        {  
                        }
                        */
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref this.processingFlag, 0);
                }
            }
        }
    }
}
