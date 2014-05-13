kinect_cv
=========

Kinect Image Processing Demo for GRAND 2014 Kinect Workshop

Dmeonstrates how to use the official Microsoft Kinect SDK and Emgu CV to perform real time image processing in C#.

## Requirements

* Windows 7+ (tested on Windows 7 and 8.1)
* Kinect for Windows sensor or Kinect for Xbox sensor
* Visual Studio 2010 or Visual C# Express 2010 
* The Emgu CV .Net wrapper for OpenCV
* Official Kinect SDK v1.8

**Using a virtual machine to run Windows on OSX or Linux?**
Only the Kinect for Windows sensor will work on a virtual machine. Kinect for Xbox sensors are not supported in virtual machines.
http://msdn.microsoft.com/en-us/library/jj663795.aspx

## Required Software Setup

Download and install *Visual C# Express 2010*:  
http://www.visualstudio.com/en-us/downloads#d-2010-express

Download and install the *Kinect for Windows 1.8 SDK and Developer Toolkit*:  
http://www.microsoft.com/en-us/kinectforwindowsdev/Downloads.aspx

Download and install *Emgu 2.9.0 for Windows*:  
http://sourceforge.net/projects/emgucv/files/emgucv/2.4.9-beta/libemgucv-windows-universal-cuda-2.9.0.1922-beta.exe/download

## Setup Kinect

Plug in the Kinect and let all the drivers install.

Test the Kinect by running:  
"C:\Program Files\Microsoft SDKs\Kinect\Developer Toolkit v1.8.0\KinectExplorer-WPF.exe"


## Compiling and Running the Code for the First Time

1. Set the reference path to your Emgu installation.
2. Compile the solution.
3. Before running, copy the "\x64" and "\x86" subdirectories of "C:\Emgu\emgucv-windows-universal-cuda 2.9.0.1922\bin\" to the "Debug\" subdirectory of kinect cv.


