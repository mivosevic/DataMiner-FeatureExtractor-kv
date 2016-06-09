﻿using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace RUAP_KV_.NET
{
    public partial class WebForm1 : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btn_Upload_Click(object sender, EventArgs e)
        {
            if (FileUpload1.HasFile)
            {
                string fileExtension = System.IO.Path.GetExtension(FileUpload1.FileName);

                if(fileExtension.ToLower() != ".jpg")
                {
                    control_label.Text = "Only .jpg files allowed!";
                    control_label.ForeColor = System.Drawing.Color.Red;
                }

                else
                {
                    int fileSize = FileUpload1.PostedFile.ContentLength;
                    if(fileSize > 2097152)
                    {
                        control_label.Text = "Only files up to 2MB are allowed!";
                        control_label.ForeColor = System.Drawing.Color.Red;
                    }
                    else
                    {
                        string savePath = Server.MapPath("~/Uploads/" + FileUpload1.FileName);

                        FileUpload1.SaveAs(savePath);
                        control_label.Text = "File Uploaded!";
                        control_label.ForeColor = System.Drawing.Color.Green;

                        imageAnalysis(savePath);
                    }                 
                }
                
            }

            else
            {
                control_label.Text = "File not selected!";
                control_label.ForeColor = System.Drawing.Color.Red;
            }

            
        }//End of Upload

        protected void imageAnalysis(string path)
        {
            //Load picture
            Bitmap bmp = new Bitmap(path);
            label_features.Text = "";
            getFeatureArray(bmp);

        }//End of imageAnalysis

        private bool getFeatureArray(Bitmap bmp)
        {
            //Get path to haar testing file
            string haarPath = Server.MapPath("~/haar/haarcascade_frontalface_alt_tree.xml");

            Image<Bgr, byte> ImageFrame = new Image<Bgr, byte>(bmp);
            //Convert to gray scale
            Image<Gray, byte> grayFrame = ImageFrame.Convert<Gray, byte>();

            //Classifier
            CascadeClassifier classifier = new CascadeClassifier(haarPath);
            //Detect faces. gray scale, windowing scale factor (closer to 1 for better detection),minimum number of nearest neighbours
            //min and max size in pixels. Start to search with a window of 800 and go down to 100 
            Rectangle[] rectangles = classifier.DetectMultiScale(grayFrame, 1.4, 0, new Size(15, 15), new Size(800, 800));

            if (rectangles == null)
            {
                control_label.Text = "No faces on image";
                control_label.ForeColor = Color.Red;
                return false;
            }
            control_label.Text = "Found " + rectangles.Length.ToString() + " faces on image";
            control_label.ForeColor = Color.Green;

            int[] feature;

            double[] avg_features = new double[60];

            int counter = 0;
            for (counter = 0; counter < rectangles.Length; counter++)
            {
                //Get LBP BUT FIRST CUT FACE OUT AND RESIZE IMG
                feature = calculateLBP(CutFaceOut(bmp, rectangles[counter]));

                for (int i = 0; i < 59; i++)
                {
                    avg_features[i] += feature[i];
                }
            }

            //Calculate average feature values
            for(int i = 0; i < 59; i++)
            {
                avg_features[i] /= counter;
                //Print features
                if (i == 58)
                    label_features.Text += avg_features[i].ToString();
                else
                    label_features.Text += avg_features[i].ToString() + "<br>";
            }
            //Add default class
            avg_features[59] = 0;


            return true;
        }//End of getFeatureArray

        private Bitmap CutFaceOut(Bitmap srcBitmap, Rectangle section)
        {
            Bitmap bmp = new Bitmap(section.Width, section.Height);
            Graphics g = Graphics.FromImage(bmp);

            Rectangle destRect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            g.DrawImage(srcBitmap, destRect, section, GraphicsUnit.Pixel);

            g.Dispose();
            
            bmp = resizeImage(bmp);
          
            return bmp;
        }//End of CutFaceOut

        private Bitmap resizeImage(Bitmap bmp)
        {
            int newWidth = 48, newHeight = 48;
            Bitmap newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);

            // Draws the image in the specified size with quality mode set to HighQuality
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(bmp, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }//End of resize image


        //LBP part
        private int[] calculateLBP(Bitmap bmp)
        {
            List<int> LBPfeatures = new List<int>();
            int currentFeature;


            //Calculate LBP
            for (int i = 1; i < bmp.Height; i = i + 3)
            {

                for (int j = 1; j < bmp.Width; j = j + 3)
                {

                    currentFeature = calculateCurrentLBP(j, i, bmp);
                    //Write to array
                    LBPfeatures.Add(currentFeature);
                }//End of inner for

            }//End of outer for

            List<int> uniformLBPfeatures = new List<int>();

            //For each 8-bit binary, if it's uniform get its count from LBPfeatures
            for (int i = 0; i < 256; i++)
            {
                if (isUniform(i))
                {
                    int uc = LBPfeatures.FindAll(x => x == i).Count; //x so that x == i
                    //Add this number to the uniformLBPFeatures
                    uniformLBPfeatures.Add(uc);
                }
            }

            //Count all non-uniform
            int nuc = LBPfeatures.FindAll(x => !isUniform(x)).Count; //x so that x is not uniform
            uniformLBPfeatures.Add(nuc);

            //Convert List<int> to int[] and return it
            return uniformLBPfeatures.ToArray();
        }//End of calculateLBP

        private int calculateCurrentLBP(int i, int j, Bitmap bmp)
        {
            double[] arrayNeighbours = new double[8];
            int counter = 0;
            double temp;
            double centerValue = (bmp.GetPixel(i, j).R + bmp.GetPixel(i, j).G + bmp.GetPixel(i, j).B) / 3;
            for (int a = -1; a < 2; a++)
            {

                for (int b = -1; b < 2; b++)
                {
                    if (a == 0 && b == 0)
                        continue;

                    temp = bmp.GetPixel(i + a, j + b).R + bmp.GetPixel(i + a, j + b).G + bmp.GetPixel(i + a, j + b).B;
                    temp /= 3;

                    if (temp > centerValue)
                        arrayNeighbours[counter] = 1;
                    else
                        arrayNeighbours[counter] = 0;
                    counter++;
                }//End of for
            }//End of for

            String binaryCombination;
            //Order them clockwise
            binaryCombination = arrayNeighbours[0].ToString() + arrayNeighbours[1].ToString() + arrayNeighbours[2].ToString() +
                arrayNeighbours[4].ToString() + arrayNeighbours[7].ToString() + arrayNeighbours[6].ToString() +
                arrayNeighbours[5].ToString() + arrayNeighbours[3].ToString();

            try
            {
                int returnNumb = Convert.ToInt32(binaryCombination, 2);
                return returnNumb;
            }
            catch (Exception)
            {
                return -1;
            }

        }//End of CalculateCurrentLBP

        //Check if 8-bit value is uniform
        private bool isUniform(int value)
        {
            int counter = 0;
            //From weight 0 to weight 6
            for (int i = 1; i <= 64; i = i << 1)
            {
                //If current and next bit are different increase the counter
                int currentBit = value & i;
                int nextBit = value & (i << 1);
                if (currentBit != (nextBit >> 1))
                    counter++;
            }
            //If counter is greater than 2 return false
            return counter > 2 ? false : true;
        }//End of isUniform


    }
}