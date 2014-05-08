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
        private InteropBitmapHelper DisplayImageHelper = null;

        MCvFont debugFont = new MCvFont(FONT.CV_FONT_HERSHEY_PLAIN, 1.5, 1.5);

        // processing images
        Image<Bgr, byte> colourImage;
        Image<Gray, float> depthImage;
        Image<Gray, byte> playerMask;

        // paint image
        Image<Bgr, byte> brushImage;
        Image<Gray, float> brushMask;
        Image<Bgr, byte> paintingImage;

        // the actual image to display
        Image<Bgr, byte> displayImage;
        DisplayWindow displayWindow;

        public MainWindow()
        {
            InitializeComponent();

            //Random r = new Random();

            //Image<Gray, float> i = new Image<Gray, float>(200, 200);
            //// loop over each row and column of the depth
            //for (int y = 0; y < i.Height; y++)
            //    for (int x = 0; x < i.Width; x++)
            //    {
            //        i.Data[y, x, 0] = (float)r.NextDouble();
            //    }

            //Image<Gray, float> j = i.ThresholdBinary(new Gray(0.5), new Gray(1.0));
            //Image<Gray, double> ii = i.Convert<Gray, double>();
            ////Image<Gray, double> jj = ii.ThresholdBinary(new Gray(0.5), new Gray(1.0));


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

                // output image
                displayImage = new Image<Bgr, byte>(colorWidth, colorHeight);

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
                displayWindow = new DisplayWindow();
                displayWindow.Show();
            }

            // Emgu window setup
            CvInvoke.cvNamedWindow(win1); //Create the window using the specific name
        }

        Bgr brushColour = new Bgr(0, 0, 255);

        String win1 = "Emgu Window"; //The name of the window

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine("Window_Closing");

            displayWindow.Close();
            displayWindow = null;

            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }

            // Emgu window shutdown
            //CvInvoke.cvWaitKey(0);  //Wait for the key pressing event
            CvInvoke.cvDestroyWindow(win1); //Destory the window
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
            }

            if (depthReceived)
            {
                Array.Clear(this.playerPixelData, 0, this.playerPixelData.Length);
                byte byte_id = (byte)255;

                // create Emgu depth and playerMask images
                depthImage = new Image<Gray, float>(sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight);

                int maxdepth = sensor.DepthStream.MaxDepth;

                // loop over each row and column of the depth
                for (int y = 0; y < this.depthHeight; y++)
                {
                    for (int x = 0; x < this.depthWidth; x++)
                    {
                        // calculate index into depth array
                        int depthIndex = x + (y * this.depthWidth);
                        DepthImagePixel depthPixel = this.depthPixels[depthIndex];

                        depthImage.Data[y, x, 0] = (depthPixel.Depth < maxdepth) ? depthPixel.Depth : maxdepth;

                        if (depthPixel.PlayerIndex > 0)
                        {
                            playerPixelData[depthIndex] = byte_id;
                        }
                    }
                }

                depthImage.Data[0, 0, 0] = sensor.DepthStream.MaxDepth;

                playerMask = new Image<Gray, byte>(ToBitmap(playerPixelData,
                    sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight,
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed));
  
            }

            #endregion

            #region use the emgu images to do something interesting

            // if we have a skeleton
            if (skeleton != null && colourImage != null && depthImage != null)
            {
                // display image has RGB background and  highlight the player who's in control

                //Matrix<double> h = ComputeDepthToRGBHomography(depthImage.Convert<Gray, byte>(), sensor);
                //Image<Gray, byte> registeredplayerMask = playerMask.WarpPerspective(h, INTER.CV_INTER_CUBIC, WARP.CV_WARP_DEFAULT, new Gray(0));
                displayImage = colourImage.AddWeighted(playerMask.Resize(2, INTER.CV_INTER_NN).Convert<Bgr, byte>(), 0.7, 0.3, 0);

                // blur it out
                displayImage = displayImage.SmoothBlur(15, 15);

                // get body depth (in m)
                SkeletonPoint sposition = skeleton.Position;
                // get the left and right hand  positions (m)
                SkeletonPoint sleft = skeleton.Joints[JointType.HandLeft].Position;
                SkeletonPoint sright = skeleton.Joints[JointType.HandRight].Position;

                // mask out depth except playermask
                Image<Gray, float> playerDepth = depthImage.Copy();
                playerDepth.SetValue(new Gray(0), playerMask.Not());

                // painting
                // - - - - - - - - - - - - -

                // hands become a brush when more that 350 mm closer to kinect
                float brushThresh = (sposition.Z * 1000) - 350;

                brushMask = playerDepth.Copy();
                brushMask = brushMask.ThresholdToZeroInv(new Gray(brushThresh));

                if (brushImage == null) brushImage = new Image<Bgr, byte>(displayImage.Width, displayImage.Height);

                brushImage.SetZero();
                brushImage.SetValue(brushColour, brushMask.Convert<Gray, byte>().Resize(2, INTER.CV_INTER_NN));
                brushImage = brushImage.SmoothBlur(10, 10);

                if (paintingImage == null) paintingImage = new Image<Bgr, byte>(displayImage.Width, displayImage.Height);

                paintingImage = paintingImage.Add(brushImage * 0.05);


                debugImg2 = depthImage.Convert<Bgr, Byte>();
                displayImage = displayImage.AddWeighted(paintingImage, 0.5, 0.5, 0.0);


                // erase all 
                // - - - - - - - - - - - - -

                // raising both hands over head erases



                // colour picker
                // - - - - - - - - - - - - -

                // hands become a brush when more that 350 mm closer to kinect
                float pickerThresh = (sposition.Z * 1000) - 100;

                Image<Gray, float> pickerImage = playerDepth.ThresholdToZeroInv(new Gray(pickerThresh));
                pickerImage = pickerImage.Dilate(2).Erode(2);

                debugImg1 = pickerImage.Convert<Bgr, byte>();

                // get the contours
            Contour<Point> contours = null;

            Image<Gray, byte> pickerImageByte = pickerImage.Convert<Gray, byte>();

            PointF pickerPoint = PointF.Empty;

            for (contours = pickerImageByte.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                                            RETR_TYPE.CV_RETR_CCOMP);
                    contours != null;
                    contours = contours.HNext)
            {

                // take the first large contour
                Contour<Point> hole = contours.VNext;
                if (hole != null && hole.Area > 100)
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
                    ColorToHSV(cc, out hue, out sat, out val);
                    sat = 1.0; 
                    
                    val = 1.0;
                    cc = HSVToColor(hue, sat, val);
                    debugImg1.Draw(new Cross2DF(pickerPoint, 50, 50), new Bgr(cc), 3);
                    WriteDebugText(debugImg1, (int)pickerPoint.X, (int)pickerPoint.Y, String.Format("{0},{1},{2}", hue, sat, val));
                    brushColour = new Bgr(cc);
                
                }
            
            }
       


                


                double hand_distance = Helpers.Utilities.Distance(sleft, sright);


                SkeletonPoint shands = Utilities.MidPoint(sleft, sright);

                // get a ROI around the hands
                float roi_diameter = 60;
                System.Drawing.SizeF roi_size = new System.Drawing.SizeF(roi_diameter, roi_diameter);

                DepthImagePoint dhands = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(shands, sensor.DepthStream.Format);
                MCvBox2D handsRoi = new MCvBox2D(dhands.ToPointF(), roi_size, 0);
                debugImg2.Draw(handsRoi, new Bgr(Color.Yellow), 1);


                Image<Gray, float> handsImage = depthImage.Copy(handsRoi);

                // set all the 
                handsImage.SetValue(4000, handsImage.Convert<Gray, byte>().ThresholdBinaryInv(new Gray(5), new Gray(255)));

                handsImage = handsImage.Erode(3).Dilate(3);
                //(int)handsRoi.GetVertices()[0].X, (int)handsRoi.GetVertices()[0].Y
                Rectangle temp = new Rectangle(0, 0, (int)roi_size.Width, (int)roi_size.Height);
                debugImg2.ROI = temp;
                handsImage.Convert<Bgr, Byte>().CopyTo(debugImg2);
                debugImg2.ROI = Rectangle.Empty;


                double[] minvals;
                double[] maxvals;
                Point[] minpoints;
                Point[] maxpoints;
                handsImage.MinMax(out minvals, out maxvals, out minpoints, out maxpoints);


                WriteDebugText(debugImg2, 10, 200, String.Format("hd: {0:0.00} d {1}", hand_distance, minvals[0]));

                /*
                // mask out the depth image for player mask

                //depthImage.SetValue(new Gray(1500), playerMask.Not());
                //Image<Gray, Byte> depthImage8 = depthImage.Convert<Gray, Byte>();


                debugImg1 = colourImage.Copy();
                debugImg2 = depthImage.Convert<Bgr, Byte>();




                // get depth pixel at skeleton hand positions
                DepthImagePoint dleft = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(sleft, sensor.DepthStream.Format);
                DepthImagePoint dright = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(sright, sensor.DepthStream.Format);

                // see if hands are in painting area
                float painting_offset = 0.25f;
                bool isLeftPainting = ((sposition.Z - sleft.Z) > painting_offset);
                bool isRightPainting = ((sposition.Z - sright.Z) > painting_offset);

                // get a ROI around the hands
                float roi_diameter = 60;
                System.Drawing.SizeF roi_size = new System.Drawing.SizeF(roi_diameter, roi_diameter);

                // left hand
                MCvBox2D leftRoi = new MCvBox2D(dleft.ToPointF(), roi_size, 0);
                Image<Gray, double> leftDepth = depthImage.Copy(leftRoi);

                if (true || isLeftPainting)
                {
                    //Image<Gray, Byte> leftDepth = leftDepth.Convert<Gray, Byte>();

                    //Image<Gray, Byte> mask = leftDepth.ThresholdBinaryInv(new Gray(5), new Gray(255));
                    //mask = mask.Erode(5);
                    //leftDepth.SetValue(255, mask);

                    //double[] minvals;
                    //double[] maxvals;
                    //Point[] minpoints;
                    //Point[] maxpoints;
                    //leftDepth.MinMax(out minvals, out maxvals, out minpoints, out maxpoints);

                    leftDepth = leftDepth.Dilate(5);

                    double max = double.MinValue;
                    double min = double.MaxValue;
                    for (int y = 0; y < leftDepth.Height; y++)
                        for (int x = 0; x < leftDepth.Width; x++)
                        {
                            double v = leftDepth.Data[y, x, 0];
                            if (v == 0) continue;
                            if (v > max) max = v;
                            if (v < min) min = v;
                        }

                    WriteDebugText(debugImg2, (int)leftRoi.center.X, (int)leftRoi.center.Y,
                            String.Format("{0:0} {1:0}",min, max));

                    Image<Gray, Byte> leftBrush = new Image<Gray, byte>(leftDepth.Width, leftDepth.Height);

                    //leftBrush = leftBrush.ThresholdBinaryInv(new Gray(min + 10), new Gray(255));

                    double vv = min + 50;
                    for (int y = 0; y < leftDepth.Height; y++)
                        for (int x = 0; x < leftDepth.Width; x++)
                        {
                            if (leftDepth.Data[y, x, 0] > vv) leftBrush.Data[y, x, 0] = 0;
                            else leftBrush.Data[y, x, 0] = 255;
                        }

                    Rectangle leftWin = new Rectangle(10, 10, (int)roi_size.Width, (int)roi_size.Height);
                    debugImg1.ROI = leftWin;
                    leftDepth.Convert<Bgr, Byte>().CopyTo(debugImg1);
                    debugImg1.ROI = Rectangle.Empty;


                    Stamp(debugImg1, leftBrush.Convert<Bgr, Byte>(), 100, 10);

                    CvInvoke.cvShowImage(win1, leftBrush);

                    //leftBrush.Convert<Gray, Byte>().ThresholdToZero(new Gray(100));
                    //leftDepth.ThresholdToZero(new Gray(min + 5));

                }

                // right hand
                MCvBox2D rightRoi = new MCvBox2D(dright.ToPointF(), roi_size, 0);
                Image<Gray, Double> rightDepth = depthImage.Copy(rightRoi);
               
                // draw the ROI for debug
                debugImg2.Draw(leftRoi, new Bgr(Color.Red), isLeftPainting ? 5 : 1);
                debugImg2.Draw(rightRoi, new Bgr(Color.Blue), isRightPainting ? 5 : 1);

                // see if the hand ROI intersect
                if (Utilities.Distance(leftRoi.center, rightRoi.center) < roi_diameter)
                {
                    PointF p = Utilities.MidPoint(leftRoi.center, rightRoi.center);
                    //debugImg2.Draw(new CircleF(p, roi_diameter/2), new Bgr(0, 255, 0), 1);
                    System.Drawing.SizeF both_roi_size = new System.Drawing.SizeF(roi_diameter * 1.5f, roi_diameter * 1.5f);
                    MCvBox2D bothRoi = new MCvBox2D(p, both_roi_size, 0);
                    Image<Gray, Double> bothDepth = depthImage.Copy(bothRoi);
                    debugImg2.Draw(bothRoi, new Bgr(Color.Yellow), 1);
                }


                //state.PlayerMask.ROI = leftRoi.MinAreaRect();
                //leftHandImg.SetValue(new Bgr(255, 255, 255), state.PlayerMask.Not());
                //state.PlayerMask.ROI = Rectangle.Empty;

                

                // draw the hand images
                //Rectangle leftWin = new Rectangle(10, 10, (int)roi_size.Width, (int)roi_size.Height);
                //DebugImg.Draw(leftRoi, c, 1);
                //DebugImg.ROI = leftWin;
                //leftHandImg.CopyTo(DebugImg);
                //DebugImg.ROI = Rectangle.Empty;
                //DebugImg.Draw(leftWin, c, 1);


                //System.Drawing.PointF p = new System.Drawing.PointF(left.X, left.Y);
                //debugImg1.Draw(new CircleF(p, 10), new Bgr(255, 0, 0), -1);

                // get the ROI around the skeleton hand position

                //displayImage = colourImage;
                 
                 */


            }
            else if (colourImage != null)
            {
                displayImage = colourImage.Copy();

                debugImg1 = displayImage.Copy();
                debugImg2 = depthImage.Convert<Bgr, Byte>();

                double[] minvals;
                double[] maxvals;
                Point[] minpoints;
                Point[] maxpoints;
                depthImage.MinMax(out minvals, out maxvals, out minpoints, out maxpoints);
                WriteDebugText(debugImg2, 10, 40, String.Format("{0}", maxvals[0]));

            }
            else
            {
                displayImage.SetValue(new Bgr(Color.CadetBlue));
            }

            //sensor.CoordinateMapper.MapDepthFrameToColorFrame(
            //    DepthFormat,
            //    this.depthPixels,
            //    ColorFormat,
            //    this.colorCoordinates);


            #endregion


            //CvInvoke.cvShowImage(win1, colourImage);

            // display image
            if (displayImage != null)
            {
                if (DisplayImageHelper == null)
                {
                    DisplayImageHelper = new InteropBitmapHelper(displayImage.Width, displayImage.Height, displayImage.Bytes, PixelFormats.Bgr24);
                    displayWindow.DisplayImageSource = DisplayImageHelper.InteropBitmap;
                }
                DisplayImageHelper.UpdateBits(displayImage.Bytes);
            }

            // display debug images
            if (debugImg1 != null)
            {
                String debugMsg = String.Format("{0} Sk: {1}", colorFrameNum, id);
                debugImg1.Draw(debugMsg, ref debugFont, new System.Drawing.Point(10, 30), new Bgr(255, 255, 255));

                if (DebugImg1Helper == null || DebugImg1Helper.Width != debugImg1.Width)
                {
                    DebugImg1Helper = new InteropBitmapHelper(debugImg1.Width, debugImg1.Height, debugImg1.Bytes, PixelFormats.Bgr24);
                    MaskedColor.Source = DebugImg1Helper.InteropBitmap;
                }
                DebugImg1Helper.UpdateBits(debugImg1.Bytes);


            }

            if (debugImg2 != null)
            {
                if (DebugImg2Helper == null || DebugImg2Helper.Width != debugImg2.Width)
                {
                    DebugImg2Helper = new InteropBitmapHelper(debugImg2.Width, debugImg2.Height, debugImg2.Bytes, PixelFormats.Bgr24);
                    DepthImage.Source = DebugImg2Helper.InteropBitmap;
                }
                DebugImg2Helper.UpdateBits(debugImg2.Bytes);
            }
        }


        #region helper methods


        public static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0 : 1d - (1d * min / max);
            value = max / 255d;
        }

        public static Color HSVToColor(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return Color.FromArgb(255, t, p, v);
            else
                return Color.FromArgb(255, v, p, q);
        }

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

           // ImageViewArea viewArea = new ImageViewArea();

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
                dp.Depth = (int)depth[dp.X, dp.Y].Intensity;

                //int index = (y * width + x) * 2;
                //int player = depth[index] & 7;
                //short depthValue = (short)(depth[index] | (depth[index + 1] << 8));



                //int cx, cy;

                ColorImagePoint cp = sensor.CoordinateMapper.MapDepthPointToColorPoint(sensor.DepthStream.Format, dp, sensor.ColorStream.Format);
                
                //mKinect.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, viewArea, x, y, depthValue, out cx, out cy);
                destPts[i, 0] = cp.X / ratio;
                destPts[i, 1] = cp.Y / ratio;

            }

            Matrix<double> srcpm = new Matrix<double>(sourcePts);
            Matrix<double> dstpm = new Matrix<double>(destPts);
            Matrix<double> homogm = new Matrix<double>(3, 3, 1);

            //dest.SetValue(new Bgr(255, 255, 255));

 
            CvInvoke.cvFindHomography(srcpm.Ptr, dstpm.Ptr, homogm, HOMOGRAPHY_METHOD.DEFAULT, 3, IntPtr.Zero);
            
            //CvInvoke.cvWarpPerspective(depth.Ptr, registeredDepth, homogm.Ptr, 0, new MCvScalar(255));


            //Image<Gray, Byte> dest2 = new Image<Gray, Byte>(mPlayerMasksImage.Width, mPlayerMasksImage.Height);
            //CvInvoke.cvWarpPerspective(mPlayerMasksImage.Ptr, dest2, homogm.Ptr, 0, new MCvScalar(255, 255, 255));
            //mPlayerMasksImage = dest2;




            return homogm;
        }

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


        public void WriteDebugText(Image<Bgr, Byte> img, int x, int y, string text, params object[] args)
        {
            img.Draw(String.Format(text, args), ref debugFont, new Point(x, y), new Bgr(255, 255, 255));
        }


        public static System.Drawing.Bitmap ToBitmap(byte[] pixels, int width, int height)
        {
            return ToBitmap(pixels, width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        }

        void Stamp(Image<Bgr, Byte> img, Image<Bgr, Byte> stamp, int x, int y)
        {
            Rectangle a = new Rectangle(x, y, (int)stamp.Width, (int)stamp.Height);
            img.ROI = a;
            stamp.Convert<Bgr, Byte>().CopyTo(img);
            img.ROI = Rectangle.Empty;
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
