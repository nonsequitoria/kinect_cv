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

using System.Diagnostics;

using Emgu.CV;
using Emgu.CV.Structure;

using Microsoft.Kinect;

namespace KinectCV
{
    /// <summary>
    /// Interaction logic for DisplayWindow.xaml
    /// </summary>
    public partial class DisplayWindow : Window
    {

        public DisplayWindow()
        {
            InitializeComponent();
        }

        public ImageSource DisplayImageSource
        {
            get { return DisplayImage.Source; }
            set {DisplayImage.Source = value; }
        }



    }
}
