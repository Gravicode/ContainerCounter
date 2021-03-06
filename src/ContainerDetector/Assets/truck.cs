// This file was automatically generated by VS extension Windows Machine Learning Code Generator v3
// from model file truck.onnx
// Warning: This file may get overwritten if you add add an onnx file with the same name
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.AI.MachineLearning;
namespace ContainerDetector
{
    
    public sealed class truckInput
    {
        public ImageFeatureValue data; // BitmapPixelFormat: Bgra8, BitmapAlphaMode: Premultiplied, width: 416, height: 416
    }
    
    public sealed class truckOutput
    {
        public TensorFloat model_outputs0; // shape(-1,-1,13,13)
    }
    
    public sealed class truckModel
    {
        private LearningModel model;
        private LearningModelSession session;
        private LearningModelBinding binding;
        public static async Task<truckModel> CreateFromStreamAsync(IRandomAccessStreamReference stream)
        {
            truckModel learningModel = new truckModel();
            learningModel.model = await LearningModel.LoadFromStreamAsync(stream);
            learningModel.session = new LearningModelSession(learningModel.model);
            learningModel.binding = new LearningModelBinding(learningModel.session);
            return learningModel;
        }
        public async Task<truckOutput> EvaluateAsync(truckInput input)
        {
            binding.Bind("data", input.data);
            var result = await session.EvaluateAsync(binding, "0");
            var output = new truckOutput();
            output.model_outputs0 = result.Outputs["model_outputs0"] as TensorFloat;
            return output;
        }
    }
}

