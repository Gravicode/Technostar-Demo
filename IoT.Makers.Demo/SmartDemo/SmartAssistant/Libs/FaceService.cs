using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;
using Windows.Storage;
using System.IO;
using System.Diagnostics;

namespace SmartAssistant
{
    public class FaceService
    {
        const string EmoKey = "356c0ac7e05c43e2a755b46627d4501d";
        const string FaceKey = "f485c0148bcc484ba6537a209d154b77";
        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient(FaceKey);
        private readonly EmotionServiceClient emotionServiceClient = new EmotionServiceClient(EmoKey);

        public async Task<FaceRectangle[]> UploadAndDetectFaces(StorageFile imageFile)
        {
            try
            {
                var stream = await imageFile.OpenStreamForReadAsync();
                var faces = await faceServiceClient.DetectAsync(stream);
                if (faces == null || faces.Length < 1)
                {
                    return null;
                }
                var faceRects = faces.Select(face => face.FaceRectangle);
                return faceRects.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }



        #region Detect Emotion
        public async Task<string> DetectEmotion(StorageFile imageFile)
        {
           var emotions =  await DetectEmotionFromFile(imageFile);
            foreach (var item in emotions)
            {
                var bestEmotion = item.Scores.ToRankedList().FirstOrDefault().Key;
                if (bestEmotion != nameof(Scores.Neutral))
                    return bestEmotion;
            }
            return null;
        }

        async Task<Emotion[]> DetectEmotionFromFile(StorageFile imageFile)
        {
            WriteLog("Calling EmotionServiceClient.RecognizeAsync()...");
            try
            {
                var stream = await imageFile.OpenStreamForReadAsync();
                Emotion[] emotionResult;

                emotionResult = await emotionServiceClient.RecognizeAsync(stream);
                return emotionResult;

            }
            catch (Exception exception)
            {
                WriteLog(exception.ToString());
                return null;
            }
        }
        async Task<Emotion[]> DetectEmotionUrl(string Url)
        {

            WriteLog("Calling EmotionServiceClient.RecognizeAsync()...");
            try
            {
                //
                // Detect the emotions in the URL
                //
                Emotion[] emotionResult = await emotionServiceClient.RecognizeAsync(Url);
                return emotionResult;
            }
            catch (Exception exception)
            {
                WriteLog("Detection failed. Please make sure that you have the right subscription key and proper URL to detect.");
                WriteLog(exception.ToString());
                return null;
            }
        }

        void WriteLog(string Msg)
        {
            Debug.WriteLine(Msg);
        }
        #endregion

    }
}
