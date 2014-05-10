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


// Emgu library
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

using KinectCV.Helpers;

// default to using System.Drawing graphics to make using EMGU easier 
using Point = System.Drawing.Point;
using PointF = System.Drawing.PointF;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace KinectCV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// stream formats
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        // Active Kinect sensor
        private KinectSensor sensor;

        // Intermediate storage for data received from the sensor
        private DepthImagePixel[] depthPixels;
        private byte[] colorPixels;
        private byte[] playerPixelData;
        private byte[] justDepthPixels;

        // debug images
        Bgr brushColour = new Bgr(0, 0, 255);

        Image<Bgr, Byte> debugImg1 = null;
        Image<Bgr, Byte> debugImg2 = null;

        // processing images
        Image<Bgr, byte> colourImage;
        Image<Gray, float> depthImage;
        Image<Gray, byte> playerMasks;

        // paint image
        Image<Bgr, byte> brushImage;
        Image<Gray, float> brushMask;
        Image<Bgr, byte> paintingImage;

        // the actual image to display
        Image<Bgr, byte> displayImage;

        // homography matrix to register depth to RGB using Emgu
        Matrix<double> depthToRGBHomography;

        // helper to convert EMGU image to WPF Image Source
        private InteropBitmapHelper DisplayImageHelper = null;

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public MainWindow()
        {
            InitializeComponent();
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

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

                this.sensor.ColorStream.Enable(ColorFormat);

                // Turn on to get player masks
                this.sensor.SkeletonStream.Enable();
                this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.playerPixelData = new byte[this.sensor.DepthStream.FramePixelDataLength];
                justDepthPixels = new byte[this.sensor.DepthStream.FramePixelDataLength];

                // output image
                displayImage = new Image<Bgr, byte>(sensor.ColorStream.FrameWidth, sensor.ColorStream.FrameHeight);

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

                // tilt if necessary
                this.sensor.ElevationAngle = 10;
            }
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine("Window_Closing");

            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

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

            #endregion

            #region convert kinect frame to emgu images

            if (colorReceived)
            {
                // get Emgu colour image 
                colourImage = new Image<Bgr, byte>(ToBitmap(colorPixels,
                    sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight));
            }

            int playerIndex2 = 0;

            short maxplayerindex = -1;

            if (depthReceived)
            {
                Array.Clear(this.playerPixelData, 0, this.playerPixelData.Length);
                byte byte_id = (byte)255;

                // create Emgu depth and playerMask images
                depthImage = new Image<Gray, float>(sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight);
                playerMasks = new Image<Gray, Byte>(sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight);

                int maxdepth = sensor.DepthStream.MaxDepth;

                // loop over each row and column of the depth 
                // this can be slow, but only way to preserve full depth information
                int depthW = sensor.DepthStream.FrameWidth;
                int depthH = sensor.DepthStream.FrameHeight;
                int i;
                for (int y = 0; y < depthH; y++)
                {
                    for (int x = 0; x < depthW; x++)
                    {
                        // calculate index into depth array
                        i = x + (y * depthW);
                        DepthImagePixel depthPixel = this.depthPixels[i];

                        // save pixel to images
                        depthImage.Data[y, x, 0] = (depthPixel.Depth < maxdepth) ? depthPixel.Depth : maxdepth;
                        playerMasks.Data[y, x, 0] = (byte)depthPixel.PlayerIndex;

                        //playerPixelData[i] = (byte)depthPixel.PlayerIndex;

                    }
                }

                // HACK set one pixel to max value to stop flickering when 
                // converting float image to byte or Bgr
                depthImage.Data[0, 0, 0] = sensor.DepthStream.MaxDepth;

                // should work, but causes grey values to be pseudo randomly transposed
                //playerMasks = new Image<Gray, byte>(ToBitmap(playerPixelData,
                //    sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight,
                //    System.Drawing.Imaging.PixelFormat.Format8bppIndexed));
            }



            #endregion

            // pick which skeleton to track
            int playerId = TrackClosestSkeleton(sensor, skeletons);
            Skeleton skeleton = null;
            if (playerId > 0) skeleton = skeletons.First(s => s.TrackingId == playerId);


            #region use the emgu images to do something interesting

            // if we have a skeleton
            if (skeleton != null && colourImage != null && depthImage != null)
            {
                // create a player mask for player we want
                byte playerIndex = (byte)(Array.IndexOf(skeletons, skeleton) + 1);
 
                //playerMask.SetValue(new Gray(255), playerMasks.And(new Gray(10 * playerIndex)));


                //playerMasks.Draw(new CircleF(new PointF(160, 120), 30), new Gray(10), -1);
                //playerMask.SetValue(new Gray(255), playerMasks);

                //playerMask = playerMasks.ThresholdToZero(new Gray(9));

                //double[] min, max;
                //Point[] pmin, pmax;
                //playerMasks.MinMax(out min, out max, out pmin, out pmax);


                Image<Gray, Byte> playerMask = playerMasks.Convert(delegate(Byte b) { return (Byte)(b == playerIndex ? 255 : 0); });

                CvInvoke.cvShowImage("playerMask", playerMask);
                CvInvoke.cvShowImage("playerMasks", playerMasks);

                // register depth to Rgb using Emgu
                // compute homography if first frame
                if (depthToRGBHomography == null)
                    depthToRGBHomography = ComputeDepthToRGBHomography(
                        depthImage.Convert<Gray, byte>(), sensor);
                // do the registration warp
                Image<Gray, byte> registeredplayerMask = playerMask.WarpPerspective(
                    depthToRGBHomography, INTER.CV_INTER_CUBIC, WARP.CV_WARP_DEFAULT,new Gray(0));

                // create the display image background
                // blended RGB and player mask
                displayImage = colourImage.AddWeighted(
                    registeredplayerMask.Resize(2, INTER.CV_INTER_NN).Convert<Bgr, byte>(), 
                    0.7, 0.3, 0);

                // blur it out
                displayImage = displayImage.SmoothBlur(5, 5);

                // get body depth (in m)
                SkeletonPoint sposition = skeleton.Position;
                // get the left and right hand  positions (m)
                SkeletonPoint sleft = skeleton.Joints[JointType.HandLeft].Position;
                SkeletonPoint sright = skeleton.Joints[JointType.HandRight].Position;
                // head position
                SkeletonPoint shead = skeleton.Joints[JointType.Head].Position;

                // mask out depth except playermask
                Image<Gray, float> playerDepth = depthImage.Copy();
                playerDepth.SetValue(new Gray(0), playerMask.Not());

                // paint with hands
                // - - - - - - - - - - - - -

                // hands become a brush or eraser when more that 250 mm closer to kinect
                float brushThresh = (sposition.Z * 1000) - 250;

                brushMask = playerDepth.Copy();
                brushMask = brushMask.ThresholdToZeroInv(new Gray(brushThresh));

                // update the brush image
                if (brushImage == null) brushImage = 
                    new Image<Bgr, byte>(displayImage.Width, displayImage.Height);
                brushImage.SetZero();
                brushImage.SetValue(brushColour, brushMask.Convert<Gray, byte>().Resize(2, INTER.CV_INTER_NN));
                brushImage = brushImage.SmoothBlur(10, 10);

                if (paintingImage == null) 
                    paintingImage = new Image<Bgr, byte>(displayImage.Width, displayImage.Height);

                paintingImage = paintingImage.Add(brushImage * 0.05);

                debugImg2 = depthImage.Convert<Bgr, Byte>();
                {
                    DepthImagePoint dp;
                    dp = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(sleft, sensor.DepthStream.Format);
                    debugImg2.Draw(new CircleF(dp.ToPointF(), 20), new Bgr(Color.Coral), 1);
                    dp = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(sright, sensor.DepthStream.Format);
                    debugImg2.Draw(new CircleF(dp.ToPointF(), 20), new Bgr(Color.LightGreen), 1);
                    dp = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(shead, sensor.DepthStream.Format);
                    debugImg2.Draw(new CircleF(dp.ToPointF(), 20), new Bgr(Color.Cyan), 1);
                    Utilities.WriteDebugText(debugImg2, 10, 40, "Player: {0} {1}", playerId, playerIndex);
                }


                displayImage = displayImage.AddWeighted(paintingImage, 0.5, 0.5, 0.0);

                // erase all 
                // - - - - - - - - - - - - -

                // raising both hands over head erases
                double hand_distance = Utilities.Distance(sleft, sright);
                if (hand_distance < 0.3 && sleft.Y > shead.Y && sright.Y > shead.Y)
                {
                    paintingImage.SetZero();
                }

                // colour picker
                // - - - - - - - - - - - - -

                // hands can form a picker hole when more than 150 mm closer to kinect
                float pickerThresh = (sposition.Z * 1000) - 150;

                Image<Gray, float> pickerImage = playerDepth.ThresholdToZeroInv(new Gray(pickerThresh));
                pickerImage = pickerImage.Dilate(2).Erode(2);

                debugImg1 = new Image<Bgr, byte>(pickerImage.Width, pickerImage.Height);
                debugImg1.SetValue(new Bgr(Color.Yellow), pickerImage.Convert<Gray, Byte>());
                debugImg1.SetValue(new Bgr(Color.OrangeRed), brushMask.Convert<Gray, Byte>());

                // get the contours
                Contour<Point> contours = null;

                Image<Gray, byte> pickerImageByte = pickerImage.Convert<Gray, byte>();

                PointF pickerPoint = PointF.Empty;

                for (contours = pickerImageByte.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                                                RETR_TYPE.CV_RETR_CCOMP);
                        contours != null;
                        contours = contours.HNext)
                {

                    // take the first hole contour
                    Contour<Point> hole = contours.VNext;
                    if (hole != null && hole.Area > 50)
                    {
                        debugImg1.Draw(hole, new Bgr(0, 0, 255), 2);
                        MCvMoments moments = hole.GetMoments();
                        MCvPoint2D64f p = moments.GravityCenter;

                        pickerPoint = new PointF((float)p.x, (float)p.y);
                    }
                    else
                    {

                    }
                }

                if (pickerPoint != PointF.Empty)
                {
                    DepthImagePoint dp = new DepthImagePoint();
                    dp.X = (int)pickerPoint.X;
                    dp.Y = (int)pickerPoint.Y;
                    dp.Depth = (int)depthImage[dp.Y, dp.X].Intensity;
                    ColorImagePoint cp = sensor.CoordinateMapper.MapDepthPointToColorPoint(sensor.DepthStream.Format, dp, sensor.ColorStream.Format);

                    if (cp.X > 0 && cp.X < colourImage.Width && cp.Y > 0 && cp.Y < colourImage.Height)
                    {
                        Bgr c = colourImage[cp.Y, cp.X];
                        double hue, sat, val;
                        Color cc = Color.FromArgb((int)c.Red, (int)c.Green, (int)c.Blue);
                        Utilities.ColorToHSV(cc, out hue, out sat, out val);
                        sat = Math.Min(sat * 2, 1);
                        val = Math.Min(val * 2, 1);
                        cc = Utilities.HSVToColor(hue, sat, val);
                        CircleF circle = new CircleF(new PointF(cp.X, cp.Y), 10);
                        displayImage.Draw(circle, new Bgr(cc), -1);
                        displayImage.Draw(circle, new Bgr(255,255,255), 1);
                        //WriteDebugText(debugImg1, (int)pickerPoint.X, (int)pickerPoint.Y, String.Format("{0},{1},{2}", hue, sat, val));
                        brushColour = new Bgr(cc);
                    }
                }

            }
            // waiting for skeleton
            else if (colourImage != null && depthImage != null)
            {
                displayImage = colourImage.Copy();
                debugImg1 = displayImage.Copy();
                debugImg2 = depthImage.Convert<Bgr, Byte>();
                Utilities.WriteDebugText(debugImg2, 10, 40, "No Skeleton");

            }
            // something's wrong
            else
            {
                displayImage.SetValue(new Bgr(Color.CadetBlue));
            }

            #endregion

            // display image
            if (displayImage != null)
            {
                if (DisplayImageHelper == null)
                {
                    DisplayImageHelper = new InteropBitmapHelper(displayImage.Width, displayImage.Height, displayImage.Bytes, PixelFormats.Bgr24);
                    DisplayImage.Source = DisplayImageHelper.InteropBitmap;
                }
                DisplayImageHelper.UpdateBits(displayImage.Bytes);
            }

            // display Emgu debug images
            CvInvoke.cvShowImage("debugImg1", debugImg1);
            CvInvoke.cvShowImage("debugImg2", debugImg2);

        }


        #region helper methods

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        public static Matrix<double> ComputeDepthToRGBHomography(Image<Gray, byte> depth, KinectSensor sensor)
        {
            // grid size
            int width = depth.Width;
            int height = depth.Height;

            // grid size to sample from
            int numX = 4;
            int numY = 4;

            // margin on outside to skip
            int margin = 20;

            int strideX = (width - 2 * margin) / numX;
            int strideY = (height - 2 * margin) / numY;

            double[,] sourcePts = new double[numX * numY, 2];

            for (int i = 0; i < numX; i++)
                for (int j = 0; j < numY; j++)
                {
                    sourcePts[(i * numX) + j, 0] = margin + (i * strideX);
                    sourcePts[(i * numX) + j, 1] = margin + (j * strideY);
                }

            double[,] destPts = new double[sourcePts.GetLength(0), 2];

            double ratio = sensor.ColorStream.FrameWidth / (double)sensor.DepthStream.FrameWidth;

            for (int i = 0; i < sourcePts.GetLength(0); i++)
            {
                DepthImagePoint dp = new DepthImagePoint();
                dp.X = (int)sourcePts[i, 0];
                dp.Y = (int)sourcePts[i, 1];
                dp.Depth = 2000;
                // you would think this would work ...
                //dp.Depth = (int)depth[dp.Y, dp.X].Intensity;

                ColorImagePoint cp = sensor.CoordinateMapper.MapDepthPointToColorPoint(sensor.DepthStream.Format, dp, sensor.ColorStream.Format);

                destPts[i, 0] = cp.X / ratio;
                destPts[i, 1] = cp.Y / ratio;

            }

            Matrix<double> srcpm = new Matrix<double>(sourcePts);
            Matrix<double> dstpm = new Matrix<double>(destPts);
            Matrix<double> homogm = new Matrix<double>(3, 3, 1);

            CvInvoke.cvFindHomography(srcpm.Ptr, dstpm.Ptr, homogm, HOMOGRAPHY_METHOD.DEFAULT, 3, IntPtr.Zero);

            return homogm;
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        int lastClosestId;

        private static int TrackClosestSkeleton(KinectSensor sensor, Skeleton[] skeletons)
        {
            if (sensor != null && sensor.SkeletonStream != null)
            {
                if (!sensor.SkeletonStream.AppChoosesSkeletons)
                {
                    sensor.SkeletonStream.AppChoosesSkeletons = true; // Ensure AppChoosesSkeletons is set
                }



                SkeletonPoint camera = new SkeletonPoint();
                camera.X = camera.Y = camera.Z = 0;

                double closestDistance = 10000f; // Start with a far enough distance

                int closestID = 0;
                
                foreach (Skeleton skeleton in skeletons.Where(s => s.TrackingState != SkeletonTrackingState.NotTracked))
                {
                    double dist = Utilities.Distance(camera, skeleton.Position);
                    if (dist < closestDistance)
                    {
                        closestID = skeleton.TrackingId;
                        closestDistance = dist;
                    }
                }

                if (closestID > 0)
                {
                    sensor.SkeletonStream.ChooseSkeletons(closestID); // Track this skeleton
                }

                return closestID;
            }
            return -1;
        }

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

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

        // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

        #endregion
    }
}
