using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;

using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.UI.Core;
using Windows.Storage;
using System.Diagnostics;

namespace SmartAssistant
{
    public sealed partial class MainPage : Page
    {
        
        // Speech events may come in on a thread other than the UI thread, keep track of the UI thread's
        // dispatcher, so we can update the UI in a thread-safe manner.
        private CoreDispatcher dispatcher;

        // The speech recognizer used throughout this sample.
        private SpeechRecognizer speechRecognizer;

        /// <summary>
        /// the HResult 0x8004503a typically represents the case where a recognizer for a particular language cannot
        /// be found. This may occur if the language is installed, but the speech pack for that language is not.
        /// See Settings -> Time & Language -> Region & Language -> *Language* -> Options -> Speech Language Options.
        /// </summary>
        private static uint HResultRecognizerNotFound = 0x8004503a;
        // Webcam Related Variables:
        private WebcamHelper webcam;
        // Speech Related Variables:
        private SpeechHelper speech;
        private ResourceContext speechContext;
        private ResourceMap speechResourceMap;
        private Dictionary<string, string[]> VoiceCommands = null;
        FaceService faceApi = null;
        ComputerVisionService visionApi = null;
        TwitterService twitterApi = null;
        // Keep track of whether the continuous recognizer is currently running, so it can be cleaned up appropriately.
        private bool isListening;

        public MainPage()
        {
            PopulateCommands();
            //init intelligent service
            faceApi = new SmartAssistant.FaceService();
            visionApi = new SmartAssistant.ComputerVisionService();
            twitterApi = new SmartAssistant.TwitterService();
            this.InitializeComponent();
            Player1.MediaFailed += Player1_MediaFailed;
            isListening = false;
            //mqtt
            if (client == null)
            {
                // create client instance 
                MQTT_BROKER_ADDRESS = "cloud.makestro.com";
                client = new MqttClient(MQTT_BROKER_ADDRESS);
                string clientId = Guid.NewGuid().ToString();
                client.Connect(clientId, "mifmasterz", "123qweasd");
                SubscribeMessage();

            }
        }
        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);
                await speech.Read("I am your smart assistant");
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }

        void PopulateCommands()
        {
            VoiceCommands = new Dictionary<string, string[]>();
            VoiceCommands.Add(TagCommands.Calling, new string[] { "Hello John", "Hello" });
            VoiceCommands.Add(TagCommands.TurnOnLamp, new string[] { "Please turn on the light" });
            VoiceCommands.Add(TagCommands.TurnOffLamp, new string[] { "Please turn off the light" });
            VoiceCommands.Add(TagCommands.TakePhoto, new string[] { "Take my picture", "Take a picture" });
            VoiceCommands.Add(TagCommands.SeeMe, new string[] { "What do you see" });
            VoiceCommands.Add(TagCommands.SeeMyEmo, new string[] { "Play some music" });
            VoiceCommands.Add(TagCommands.Thanks, new string[] { "Thank you", "Thanks" });
            VoiceCommands.Add(TagCommands.ReadText, new string[] { "Read this", "Please read" });
            VoiceCommands.Add(TagCommands.Twitter, new string[] { "Get last tweet" });
            VoiceCommands.Add(TagCommands.Stop, new string[] { "Stop music" });
        }

        /// <summary>
        /// Upon entering the scenario, ensure that we have permissions to use the Microphone. This may entail popping up
        /// a dialog to the user on Desktop systems. Only enable functionality once we've gained that permission in order to 
        /// prevent errors from occurring when using the SpeechRecognizer. If speech is not a primary input mechanism, developers
        /// should consider disabling appropriate parts of the UI if the user does not have a recording device, or does not allow 
        /// audio input.
        /// </summary>
        /// <param name="e">Unused navigation parameters</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {

            // Keep track of the UI thread dispatcher, as speech events will come in on a separate thread.
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {
                btnContinuousRecognize.IsEnabled = true;

                // Initialize resource map to retrieve localized speech strings.
                Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
                string langTag = speechLanguage.LanguageTag;
                speechContext = ResourceContext.GetForCurrentView();
                speechContext.Languages = new string[] { langTag };

                speechResourceMap = ResourceManager.Current.MainResourceMap.GetSubtree("LocalizationSpeechResources");

                PopulateLanguageDropdown();
                await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            }
            else
            {
                this.resultTextBlock.Visibility = Visibility.Visible;
                this.resultTextBlock.Text = "Permission to access capture resources was not given by the user, reset the application setting in Settings->Privacy->Microphone.";
                btnContinuousRecognize.IsEnabled = false;
                cbLanguageSelection.IsEnabled = false;
            }
        }

        /// <summary>
        /// Look up the supported languages for this speech recognition scenario, 
        /// that are installed on this machine, and populate a dropdown with a list.
        /// </summary>
        private void PopulateLanguageDropdown()
        {
            // disable the callback so we don't accidentally trigger initialization of the recognizer
            // while initialization is already in progress.
            cbLanguageSelection.SelectionChanged -= cbLanguageSelection_SelectionChanged;
            Language defaultLanguage = SpeechRecognizer.SystemSpeechLanguage;
            IEnumerable<Language> supportedLanguages = SpeechRecognizer.SupportedGrammarLanguages;
            foreach (Language lang in supportedLanguages)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Tag = lang;
                item.Content = lang.DisplayName;

                cbLanguageSelection.Items.Add(item);
                if (lang.LanguageTag == defaultLanguage.LanguageTag)
                {
                    item.IsSelected = true;
                    cbLanguageSelection.SelectedItem = item;
                }
            }

            cbLanguageSelection.SelectionChanged += cbLanguageSelection_SelectionChanged;
        }

        /// <summary>
        /// When a user changes the speech recognition language, trigger re-initialization of the 
        /// speech engine with that language, and change any speech-specific UI assets.
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private async void cbLanguageSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem item = (ComboBoxItem)(cbLanguageSelection.SelectedItem);
            Language newLanguage = (Language)item.Tag;
            if (speechRecognizer != null)
            {
                if (speechRecognizer.CurrentLanguage == newLanguage)
                {
                    return;
                }
            }

            // trigger cleanup and re-initialization of speech.
            try
            {
                // update the context for resource lookup
                speechContext.Languages = new string[] { newLanguage.LanguageTag };

                await InitializeRecognizer(newLanguage);
            }
            catch (Exception exception)
            {
                var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                await messageDialog.ShowAsync();
            }
        }

        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            try
            {
                this.speechRecognizer = new SpeechRecognizer(recognizerLanguage);

                // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
                // of an audio indicator to help the user understand whether they're being heard.
                speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

                // Build a command-list grammar. Commands should ideally be drawn from a resource file for localization, and 
                // be grouped into tags for alternate forms of the same command.
                foreach(var item in VoiceCommands)
                {
                    speechRecognizer.Constraints.Add(
                   new SpeechRecognitionListConstraint(
                      item.Value, item.Key));
                }
               

                // Update the help text in the UI to show localized examples
                string uiOptionsText = string.Format("Try saying '{0}', '{1}' or '{2}'",
                    VoiceCommands[TagCommands.Calling][0],
                    VoiceCommands[TagCommands.TakePhoto][0],
                    VoiceCommands[TagCommands.TurnOnLamp][0]);
                /*
                listGrammarHelpText.Text = string.Format("{0}\n{1}",
                    speechResourceMap.GetValue("ListGrammarHelpText", speechContext).ValueAsString,
                    uiOptionsText);
                    */
                SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
                if (result.Status != SpeechRecognitionResultStatus.Success)
                {
                    // Disable the recognition buttons.
                    btnContinuousRecognize.IsEnabled = false;

                    // Let the user know that the grammar didn't compile properly.
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = "Unable to compile grammar.";
                }
                else
                {
                    btnContinuousRecognize.IsEnabled = true;

                    resultTextBlock.Visibility = Visibility.Collapsed;


                    // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
                    // some recognized phrases occur, or the garbage rule is hit.
                    speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
                    speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
                }
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == HResultRecognizerNotFound)
                {
                    btnContinuousRecognize.IsEnabled = false;

                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = "Speech Language pack for selected language not installed.";
                }
                else
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                    await messageDialog.ShowAsync();
                }
            }

        }

        /// <summary>
        /// Upon leaving, clean up the speech recognizer. Ensure we aren't still listening, and disable the event 
        /// handlers to prevent leaks.
        /// </summary>
        /// <param name="e">Unused navigation parameters.</param>
        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (this.speechRecognizer != null)
            {
                if (isListening)
                {
                    await this.speechRecognizer.ContinuousRecognitionSession.CancelAsync();
                    isListening = false;
                }
                heardYouSayTextBlock.Visibility = resultTextBlock.Visibility = Visibility.Collapsed;

                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }
        }

        /// <summary>
        /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
        /// some transient issues occur.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.NotifyUser("Continuous Recognition Completed: " + args.Status.ToString(), NotifyType.StatusMessage);
                    ContinuousRecoButtonText.Text = " Continuous Recognition";
                    cbLanguageSelection.IsEnabled = true;
                    isListening = false;
                });
            }
        }

        /// <summary>
        /// Handle events fired when a result is generated. This may include a garbage rule that fires when general room noise
        /// or side-talk is captured (this will have a confidence of Rejected typically, but may occasionally match a rule with
        /// low confidence).
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // The garbage rule will not have a tag associated with it, the other rules will return a string matching the tag provided
            // when generating the grammar.
            string tag = "unknown";
            if (args.Result.Constraint != null)
            {
                tag = args.Result.Constraint.Tag;
            }

            // Developers may decide to use per-phrase confidence levels in order to tune the behavior of their 
            // grammar based on testing.
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal,async () =>

                {
                    heardYouSayTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = string.Format("Heard: '{0}', (Tag: '{1}', Confidence: {2})", args.Result.Text, tag, args.Result.Confidence.ToString());
                    switch (tag)
                    {
                        case TagCommands.Calling:
                            await speech.Read("Yes, What Can I Do Boss?");
                            break;
                        case TagCommands.SeeMe:
                            {
                                var photo = await TakePhoto();
                                //call computer vision
                                var res = await visionApi.RecognizeImage(photo);
                                if (!string.IsNullOrEmpty(res))
                                {
                                    await speech.Read(res);
                                    resultTextBlock.Text = "I see " + res;
                                }
                            }
                            break;
                        case TagCommands.ReadText:
                            {
                                var photo = await TakePhoto();
                                //call computer vision
                                var res = await visionApi.RecognizeText(photo);
                                if (!string.IsNullOrEmpty(res))
                                {
                                    await speech.Read(res);
                                    resultTextBlock.Text = "read: " + res;
                                }
                            }
                            break;
                        case TagCommands.Stop:
                            Player1.Stop();
                            break;
                        case TagCommands.SeeMyEmo:
                            {
                                var photo = await TakePhoto();
                                var faces = await faceApi.UploadAndDetectFaces(photo);
                                if (faces == null)
                                {
                                    await speech.Read("I don't see any faces");
                                }
                                else if (faces.Count() > 1)
                                {
                                    await speech.Read("I see more than one face");
                                }
                                else if (faces.Count() == 1)
                                {
                                    //call computer vision
                                    var res = await faceApi.DetectEmotion(photo);
                                    if (!string.IsNullOrEmpty(res))
                                    {
                                        //await speech.Read(res);
                                        resultTextBlock.Text = "I think you are in " + res;

                                        Player1.Volume = 80;
                                        Player1.AutoPlay = false;

                                        switch (res.ToLower())
                                        {
                                            case "anger":
                                                await speech.Read("don't be angry boss");
                                                Player1.Source = new Uri(@"ms-appx:///lagu/metal.mp3");
                                                Player1.Play();
                                                break;
                                            case "happiness":
                                                await speech.Read("happy song for you");

                                                Player1.Source = new Uri(@"ms-appx:///lagu/lucu.mp3");
                                                Player1.Play();

                                                break;
                                            
                                            default:
                                                Player1.Source = new Uri(@"ms-appx:///lagu/santai.mp3");
                                                Player1.Play();

                                                break;

                                        }

                                    }

                                }
                            }

                            break;
                        case TagCommands.Twitter:
                            {
                                var tweets = await twitterApi.GetTwittByHashtag("#trump", 1);
                                foreach (LinqToTwitter.Status x in tweets)
                                {
                                    var y = await SentimentService.GetSentiment(x.Text);
                                    if (y != null)
                                    {
                                        var speaknow = x.Text + ". This is a " + (y.documents[0].score < 0.5 ? "negative message" : "positive message");
                                        await speech.Read(speaknow);
                                    }
                                }
                            }
                            break;
                        case TagCommands.TakePhoto:
                            await speech.Read("I will take your picture boss");
                            break;
                        case TagCommands.Thanks:
                            await speech.Read("My pleasure boss");
                            break;
                        case TagCommands.TurnOnLamp:
                            {
                                await speech.Read("Turn on the light");
                                var Pesan = Encoding.UTF8.GetBytes("LIGHT_ON");
                                client.Publish("mifmasterz/assistant/control", Pesan, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
                            }
                            break;
                        case TagCommands.TurnOffLamp:
                            {
                                await speech.Read("Turn off the light");
                                var Pesan = Encoding.UTF8.GetBytes("LIGHT_OFF");
                                client.Publish("mifmasterz/assistant/control", Pesan, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
                            }
                            break;

                    }
                });
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    heardYouSayTextBlock.Visibility = Visibility.Collapsed;
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = string.Format("Sorry, I didn't catch that. (Heard: '{0}', Tag: {1}, Confidence: {2})", args.Result.Text, tag, args.Result.Confidence.ToString());
                });
            }
        }

        private void Player1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.WriteLine(e.ErrorMessage);
        }

        private async Task<StorageFile> TakePhoto()
        {
          
            // Confirms that webcam has been properly initialized and oxford is ready to go
            if (webcam.IsInitialized())
            {

                try
                {
                    // Stores current frame from webcam feed in a temporary folder
                    StorageFile image = await webcam.CapturePhoto();
                    return image;
                }
                catch
                {
                    // General error. This can happen if there are no visitors authorized in the whitelist
                    Debug.WriteLine("WARNING: Oxford just threw a general expception.");
                }

            }
            else
            {
                if (!webcam.IsInitialized())
                {
                    // The webcam has not been fully initialized for whatever reason:
                    Debug.WriteLine("Unable to analyze visitor at door as the camera failed to initlialize properly.");
                    await speech.Read("No camera available");
                }
                /*
                if (!initializedOxford)
                {
                    // Oxford is still initializing:
                    Debug.WriteLine("Unable to analyze visitor at door as Oxford Facial Recogntion is still initializing.");
                }*/
            }
            return null;
        }

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                this.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
            });
        }

        /// <summary>
        /// Begin recognition, or finish the recognition session. 
        /// </summary>
        /// <param name="sender">The button that generated this event</param>
        /// <param name="e">Unused event details</param>
        public async void ContinuousRecognize_Click(object sender, RoutedEventArgs e)
        {
            btnContinuousRecognize.IsEnabled = false;
            if (isListening == false)
            {
                // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
                // This prevents an exception from occurring.
                if (speechRecognizer.State == SpeechRecognizerState.Idle)
                {
                    try
                    {
                        await speechRecognizer.ContinuousRecognitionSession.StartAsync();
                        ContinuousRecoButtonText.Text = " Stop Continuous Recognition";
                        cbLanguageSelection.IsEnabled = false;
                        isListening = true;
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
            else
            {
                isListening = false;
                ContinuousRecoButtonText.Text = " Continuous Recognition";
                cbLanguageSelection.IsEnabled = true;

                heardYouSayTextBlock.Visibility = Visibility.Collapsed;
                resultTextBlock.Visibility = Visibility.Collapsed;
                if (speechRecognizer.State != SpeechRecognizerState.Idle)
                {
                    try
                    {
                        // Cancelling recognition prevents any currently recognized speech from
                        // generating a ResultGenerated event. StopAsync() will allow the final session to 
                        // complete.
                        await speechRecognizer.ContinuousRecognitionSession.CancelAsync();
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }

            btnContinuousRecognize.IsEnabled = true;
        }

        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusBlock2.Text = strMessage;

            // Collapse the StatusBlock2 if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock2.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock2.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
        }
        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e)
        {
            if (webcam == null || !webcam.IsInitialized())
            {
                // Initialize Webcam Helper
                webcam = new WebcamHelper();
                await webcam.InitializeCameraAsync();

                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    // Start the live feed
                    await webcam.StartCameraPreview();
                }
            }
            else if (webcam.IsInitialized())
            {
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    await webcam.StartCameraPreview();
                }
            }
        }
        public MqttClient client { set; get; }

        public string MQTT_BROKER_ADDRESS
        {
            set; get;
        }

        void SubscribeMessage()
        {

            // register to message received 

            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

            client.Subscribe(new string[] { "mifmasterz/assistant/data", "mifmasterz/assistant/control" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

        }





        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)

        {

            string Pesan = Encoding.UTF8.GetString(e.Message);

            switch (e.Topic)

            {

                case "mifmasterz/assistant/data":


                    break;

                case "mifmasterz/assistant/control":

                    switch (Pesan)
                    {
                        case "LIGHT_ON":

                            break;
                        case "LIGHT_OFF":

                            break;
                    }
                    break;


            }
            Debug.WriteLine(e.Topic+":"+Pesan);
        }

        

    }
}
