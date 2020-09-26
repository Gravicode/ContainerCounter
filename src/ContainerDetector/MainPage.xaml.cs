using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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

            // Start the camera video preview
            var availableFrameSourceGroups = await CameraHelper.GetFrameSourceGroupsAsync();
            if (availableFrameSourceGroups != null)
            {
                CameraHelper cameraHelper = new CameraHelper() { FrameSourceGroup = availableFrameSourceGroups.FirstOrDefault() };
                CamPreview.PreviewFailed += CamPreview_PreviewFailed;
                await CamPreview.StartAsync(cameraHelper);
                CamPreview.CameraHelper.FrameArrived += CameraHelper_FrameArrived;
            }

            //await StartPreviewAsync();
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
      
       

        private async void CameraHelper_FrameArrived(object sender, Microsoft.Toolkit.Uwp.Helpers.FrameEventArgs e)
        {
            //if (Interlocked.CompareExchange(ref this.processingFlag, 1, 0) == 0)
            {
                try
                {
                    var videoFrame = e.VideoFrame;

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

                }
                finally
                {
                    //Interlocked.Exchange(ref this.processingFlag, 0);
                }
            }
        }

      

        private void CamPreview_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            float previewAspectRatio = (float)(CamPreview.ActualWidth / CamPreview.ActualHeight);
            var cameraAspectRatio = previewAspectRatio;
            UIOverlayCanvas1.Width = cameraAspectRatio >= previewAspectRatio ? CamPreview.ActualWidth : CamPreview.ActualHeight * cameraAspectRatio;
            UIOverlayCanvas1.Height = cameraAspectRatio >= previewAspectRatio ? CamPreview.ActualWidth / cameraAspectRatio : CamPreview.ActualHeight;

            m_bboxRenderer.ResizeContent(e);
        }
  

      


      

        private void CamPreview_PreviewFailed(object sender, Microsoft.Toolkit.Uwp.UI.Controls.PreviewFailedEventArgs e)
        {
            var errorMessage = e.Error;
        }

    }
}
