using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Emgu.CV;
using Emgu.CV.Structure;

using Microsoft.Kinect;

namespace kinect_cv
{
    /// <summary>
    /// Interaction logic for DisplayWindow.xaml
    /// </summary>
    public partial class DisplayWindow : Window
    {


        private byte[] colorPixels;
        private DepthImagePixel[] depthPixels;

        Emgu.CV.Image<Bgr, byte> emguColour;

        public DisplayWindow(KinectSensor sensor)
        {
            InitializeComponent();

            // Allocate space to put the depth pixels we'll receive
            depthPixels = new DepthImagePixel[sensor.DepthStream.FramePixelDataLength];

            // Allocate space to put the color pixels we'll create
            colorPixels = new byte[sensor.ColorStream.FramePixelDataLength];



        }

        public void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            bool depthReceived = false;
            bool colorReceived = false;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    
                    colorReceived = true;
                }
            }


            if (colorReceived != null)
            {
                //emguColour = new Image<Bgr, byte>(ToBitmap(colorPixels, 640, 480));

            }

        }

    }
}
