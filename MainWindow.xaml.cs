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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.IO;
using System.Diagnostics;

using System.Runtime.InteropServices;



using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace kinect_cv
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /// stream formats
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Bitmap that will hold opacity mask information
        /// </summary>
        private WriteableBitmap playerOpacityMaskImage = null;

        /// <summary>
        /// Intermediate storage for the depth data received from the sensor
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Intermediate storage for the player opacity mask
        /// </summary>
        private byte[] playerPixelData;
        byte[] justDepthPixels;

        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private ColorImagePoint[] colorCoordinates;

        /// <summary>
        /// Inverse scaling factor between color and depth
        /// </summary>
        private int colorToDepthDivisor;

        /// depth image size
        private int depthWidth;
        private int depthHeight;

        /// Indicates opaque in an opacity mask
        private int opaquePixelValue = -1;


        private InteropBitmapHelper DebugImg1Helper = null;
        private InteropBitmapHelper DebugImg2Helper = null;

        MCvFont debugFont = new MCvFont(FONT.CV_FONT_HERSHEY_PLAIN, 1.0, 1.0); //Create the font

        Image<Bgr, byte> colourImage;
        Image<Gray, Double> depthImage;
        Image<Gray, byte> playerMask;

        //DisplayWindow displayWindow;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            Debug.WriteLine("Window_Loaded");





            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthFormat);

                this.depthWidth = this.sensor.DepthStream.FrameWidth;

                this.depthHeight = this.sensor.DepthStream.FrameHeight;

                this.sensor.ColorStream.Enable(ColorFormat);

                int colorWidth = this.sensor.ColorStream.FrameWidth;
                int colorHeight = this.sensor.ColorStream.FrameHeight;

                this.colorToDepthDivisor = colorWidth / this.depthWidth;

                // Turn on to get player masks
                this.sensor.SkeletonStream.Enable();

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.playerPixelData = new byte[this.sensor.DepthStream.FramePixelDataLength];
                justDepthPixels = new byte[this.sensor.DepthStream.FramePixelDataLength];

                this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                this.MaskedColor.Source = this.colorBitmap;

                // event handler when frames are ready
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();

                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = "Kinect Not Ready";
            }
            else
            {
                statusBarText.Text = String.Format("{0}", sensor.UniqueKinectId);

                // create the display window
                //displayWindow = new DisplayWindow(sensor);
                //displayWindow.Show();

                // Add an event handler to be called whenever there is new depth frame data
                //sensor.AllFramesReady += displayWindow.SensorAllFramesReady;

            }

            // Emgu window setup
            //CvInvoke.cvNamedWindow(win1); //Create the window using the specific name
        }


        String win1 = "Emgu Window"; //The name of the window

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine("Window_Closing");

            //sensor.AllFramesReady -= displayWindow.SensorAllFramesReady;
            // displayWindow.Close();
            //displayWindow = null;

            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }

            // Emgu window shutdown
            //CvInvoke.cvWaitKey(0);  //Wait for the key pressing event
            //CvInvoke.cvDestroyWindow(win1); //Destory the window
        }


        Image<Bgr, Byte> debugImg1 = null;
        Image<Bgr, Byte> debugImg2 = null;

        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {



            #region get image and skeleton data from Kinect

            // if in the middle of shutting down, so nothing to do
            if (null == this.sensor) return;

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

            int colorFrameNum = -1;

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    colorFrameNum = colorFrame.FrameNumber;
                    colorReceived = true;
                }
            }


            Skeleton[] skeletons = new Skeleton[0];
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            // pick which skeleton to track
            int id = TrackClosestSkeleton(skeletons);
            Skeleton skeleton = null;
            if (id > 0) skeleton = skeletons.First(s => s.TrackingId == id);

            #endregion

            #region convert kinect frame to emgu images

            if (colorReceived)
            {
                // get Emgu colour image 
                colourImage = new Image<Bgr, byte>(ToBitmap(colorPixels,
                    sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight));

                String debugMsg = String.Format("{0} Sk: {1}", colorFrameNum, id);
                colourImage.Draw(debugMsg, ref debugFont, new System.Drawing.Point(10, 80), new Bgr(255, 255, 255));
            }

            if (depthReceived)
            {
                Array.Clear(this.playerPixelData, 0, this.playerPixelData.Length);
                short short_id = (short) id;
                byte byte_id = (byte)255;

                Debug.Print("{0} {1} {2}", id, short_id, byte_id);

                // create Emgu depth and playerMask images
                depthImage = new Image<Gray, Double>(sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight);

                // loop over each row and column of the depth
                for (int y = 0; y < this.depthHeight; y++)
                {
                    for (int x = 0; x < this.depthWidth; x++)
                    {
                        // calculate index into depth array
                        int depthIndex = x + (y * this.depthWidth);
                        DepthImagePixel depthPixel = this.depthPixels[depthIndex];
                        //ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];
                        //int v = (int)(depthPixel.Depth - sensor.DepthStream.MinDepth) / (sensor.DepthStream.MaxDepth - sensor.DepthStream.MinDepth);
                        depthImage.Data[y, x, 0] = depthPixel.Depth;
                        
                        //justDepthPixels[depthIndex] = (byte)depthPixel.Depth;
                        if  (depthPixel.PlayerIndex > 0)
                        {
                            playerPixelData[depthIndex] = byte_id;

                        }
                    }
                }



                playerMask = new Image<Gray, byte>(ToBitmap(playerPixelData,
                    sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight,
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed));
            }

            #endregion

            #region use the emgu images

            if (colourImage != null)
            {
                debugImg1 = colourImage.Copy();
            }
            if (skeleton != null)
            {
                SkeletonPoint sleft = skeleton.Joints[JointType.HandLeft].Position;
                ColorImagePoint left = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(sleft, sensor.ColorStream.Format);
                System.Drawing.PointF p = new System.Drawing.PointF(left.X, left.Y);
                debugImg1.Draw(new CircleF(p, 10), new Bgr(255, 0, 0), -1);

                
            }

            if (playerMask != null)
            {
                debugImg2 = depthImage.Convert<Bgr, Byte>();
            }
            sensor.CoordinateMapper.MapDepthFrameToColorFrame(
                DepthFormat,
                this.depthPixels,
                ColorFormat,
                this.colorCoordinates);


            #endregion


            //CvInvoke.cvShowImage(win1, colourImage);

            // display debug images
            if (debugImg1 != null)
            {
                if (DebugImg1Helper == null)
                {
                    DebugImg1Helper = new InteropBitmapHelper(debugImg1.Width, debugImg1.Height, debugImg1.Bytes, PixelFormats.Bgr24);
                    MaskedColor.Source = DebugImg1Helper.InteropBitmap;
                }
                DebugImg1Helper.UpdateBits(debugImg1.Bytes);
            }

            if (debugImg2 != null)
            {
                if (DebugImg2Helper == null)
                {
                    DebugImg2Helper = new InteropBitmapHelper(debugImg2.Width, debugImg2.Height, debugImg2.Bytes, PixelFormats.Bgr24);
                    DepthImage.Source = DebugImg2Helper.InteropBitmap;
                }
                DebugImg2Helper.UpdateBits(debugImg2.Bytes);
            }
        }


        #region helper methods

        private int TrackClosestSkeleton(Skeleton[] skeletons)
        {
            if (this.sensor != null && this.sensor.SkeletonStream != null)
            {
                if (!this.sensor.SkeletonStream.AppChoosesSkeletons)
                {
                    this.sensor.SkeletonStream.AppChoosesSkeletons = true; // Ensure AppChoosesSkeletons is set
                }

                float closestDistance = 10000f; // Start with a far enough distance
                int closestID = 0;

                foreach (Skeleton skeleton in skeletons.Where(s => s.TrackingState != SkeletonTrackingState.NotTracked))
                {
                    if (skeleton.Position.Z < closestDistance)
                    {
                        closestID = skeleton.TrackingId;
                        closestDistance = skeleton.Position.Z;
                    }
                }

                if (closestID > 0)
                {
                    this.sensor.SkeletonStream.ChooseSkeletons(closestID); // Track this skeleton
                }

                return closestID;
            }
            return -1;
        }


        public static System.Drawing.Bitmap ToBitmap(byte[] pixels, int width, int height)
        {
            return ToBitmap(pixels, width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

        public static System.Drawing.Bitmap ToBitmap(byte[] pixels, int width, int height, System.Drawing.Imaging.PixelFormat format)
        {
            if (pixels == null)
                return null;

            var bitmap = new System.Drawing.Bitmap(width, height, format);

            var data = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);

            bitmap.UnlockBits(data);

            return bitmap;
        }

        #endregion
    }
}
