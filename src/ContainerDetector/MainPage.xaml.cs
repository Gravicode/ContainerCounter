using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage;
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

        public MainPage()
        {
            this.InitializeComponent();
            m_bboxRenderer = new BoundingBoxRenderer(UIOverlayCanvas1);
            // Event handlers
            //Application.Current.Suspending += ApplicationSuspending;
            this.Loaded += OnLoaded;
        }

        private BoundingBoxRenderer m_bboxRenderer = null;
    

        // Model
        private ObjectDetection model = null;
        // File name of the ONNX model. This must be in the Assets folder for the project
        private string modelFileName = "mask.onnx";

    
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
            

            // Load the model
            await this.LoadModelAsync();

            StartCapture();
        }

      

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
                model = new ObjectDetection(new string[] { "mask", "nomask" });//await ONNXModel.CreateONNXModel(file);
                await model.Init(modelFile);
            }

        }
    
        /// <summary>
        /// Process the output returned by the model
        /// </summary>
        /// <param name="evalOutput"></param>
        /// <returns></returns>
        async Task ProcessOutputAsync(IList<PredictionModel> predictions)//ONNXModelOutput evalOutput
        {
            if (predictions != null && predictions.Count > 0)
            {
                m_bboxRenderer.Render(predictions);
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
      
       


      

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            float previewAspectRatio = (float)(PreviewCanvas.ActualWidth / PreviewCanvas.ActualHeight);
            var cameraAspectRatio = previewAspectRatio;
            UIOverlayCanvas1.Width = cameraAspectRatio >= previewAspectRatio ? PreviewCanvas.ActualWidth : PreviewCanvas.ActualHeight * cameraAspectRatio;
            UIOverlayCanvas1.Height = cameraAspectRatio >= previewAspectRatio ? PreviewCanvas.ActualWidth / cameraAspectRatio : PreviewCanvas.ActualHeight;

            m_bboxRenderer.ResizeContent(e);
        }
  
        //camera
        private MediaCapture mediaCapture;
        private MediaFrameReader mediaFrameReader;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private readonly string VIDEO_FILE_NAME = "video.mp4";
        private bool isPreviewing;
        private SoftwareBitmap backBuffer;
        private bool taskRunning = false;
        private Thread decodingThread;


      
        VideoFrame previewFrame;
        VideoFrame videoFrame;
       
        async void StartCapture()
        {

            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        //captureImage.Source = null;
                        //playbackElement.Source = null;
                        isPreviewing = false;
                    }

                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                StatusBlock.Text = "Initializing camera to capture audio and video...";
                // Use default initialization
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                // Set callbacks for failure and recording limit exceeded
                StatusBlock.Text = "Device successfully initialized for video recording!";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
                //mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
                StatusBlock.Text = "Camera preview succeeded";
                // Get information about the preview
                var previewProperties = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

                // Create a video frame in the desired format for the preview frame
                videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)previewProperties.Width, (int)previewProperties.Height);

               StartEngine();
            }
            catch (Exception ex)
            {
                StatusBlock.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;
            }
        }
        private async void mediaCapture_Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    StatusBlock.Text = "MediaCaptureFailed: " + currentFailure.Message;


                }
                catch (Exception)
                {
                }
                finally
                {

                    StatusBlock.Text += "\nCheck if camera is diconnected. Try re-launching the app";
                }
            });
        }
        #region Cam
        private async void Cleanup()
        {
            if (mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                    //captureImage.Source = null;
                    //playbackElement.Source = null;
                    isPreviewing = false;
                }

                mediaCapture.Dispose();
                mediaCapture = null;
            }

        }
        #endregion
        #region QR

        SoftwareBitmap currentBitmapForDecoding;

        private Object thisLock = new Object();


        private async void InferenceImage()
        {


            while (true)
            {
                if (videoFrame == null) continue;
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async() =>
                {
                   
                    previewFrame = await mediaCapture.GetPreviewFrameAsync(videoFrame);
                    if (previewFrame.SoftwareBitmap != null)
                    {
                        currentBitmapForDecoding = previewFrame.SoftwareBitmap;

                    }
                    if (currentBitmapForDecoding != null)
                    {
                        DoRecognize(currentBitmapForDecoding);

                    }
                    //currentBitmapForDecoding.Dispose();
                    currentBitmapForDecoding = null;
                });
                Thread.Sleep(500);

            }

        }





        async void StartEngine()
        {
           
            //start decoding
            decodingThread = new Thread(InferenceImage);
            decodingThread.Start();

        }

        // Do this when you start your application
        static int mainThreadId;

        // If called in the non main thread, will return false;
        public static bool IsMainThread
        {
            get { return System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId; }
        }
        #endregion


        /// <summary>
        /// Trigger file picker and image evaluation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DoRecognize(SoftwareBitmap softwareBitmap)
        {

            try
            {
                // Load the model
                //await Task.Run(async () => await LoadModelAsync());


                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                
                // Display the image
                //SoftwareBitmapSource imageSource = new SoftwareBitmapSource();
                //await imageSource.SetBitmapAsync(softwareBitmap);
                //UIPreviewImage.Source = imageSource;

                // Encapsulate the image within a VideoFrame to be bound and evaluated
                VideoFrame videoFrame = VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);

                await Task.Run(async () =>
                {
                    if (videoFrame != null)
                    {
                        // If there is a frame, set it as input to the model
                        //ONNXModelInput input = new ONNXModelInput();
                        //input.data = videoFrame;
                        // Evaluate the input data
                        var evalOutput = await model.PredictImageAsync(videoFrame);
                        //await model.EvaluateAsync(input);
                        // Do something with the model output
                        await this.ProcessOutputAsync(evalOutput);
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"error: {ex.Message}");

            }
        }

       

    }
}
