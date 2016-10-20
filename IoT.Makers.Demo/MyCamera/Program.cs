using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;

namespace MyCamera
{
    public partial class Program
    {
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            //Beri informasi di display
            ClearDisplay();
            button.ButtonPressed += button_ButtonPressed;
        }

        void ClearDisplay()
        {
            displayT35.SimpleGraphics.Clear();
            var font = Resources.GetFont(Resources.FontResources.NinaB);
            displayT35.SimpleGraphics.DisplayText("Silakan tekan tombol untuk mengambil gambar.", font, GT.Color.White, 10, 120);
            
        }

        void button_ButtonPressed(Button sender, Button.ButtonState state)
        {
            serialCameraL1.StartStreaming();
            while (!serialCameraL1.NewImageReady) { Thread.Sleep(10); }
            var bmp = serialCameraL1.GetImage();
            serialCameraL1.StopStreaming();
            displayT35.SimpleGraphics.Clear();
            displayT35.SimpleGraphics.DisplayImage(bmp, 0, 0);
        }
    }
}
