﻿// Copyright (C) Microsoft Corporation. All rights reserved.

using ContainerDetector.Helpers;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace ContainerDetector
{
    /// <summary>
    /// Helper class to render object detections
    /// </summary>
    internal class BoundingBoxRenderer
    {
        private Canvas m_canvas;

        // Cache the original Rects we get for resizing purposes
        private List<BoundingBox> m_rawRects;
   
      
        // Pre-populate rectangles/textblocks to avoid clearing and re-creating on each frame
        private Rectangle[] m_rectangles;
        private TextBlock[] m_textBlocks;

        private List<Line> m_lines;
      
        /// <summary>
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="maxBoxes"></param>
        /// <param name="lineThickness"></param>
        /// <param name="colorBrush">Default Colors.SpringGreen color brush if not specified</param>
        public BoundingBoxRenderer(Canvas canvas, int maxBoxes = 50, int lineThickness = 2, SolidColorBrush colorBrush = null)
        {
            m_rawRects = new List<BoundingBox>();

            m_rectangles = new Rectangle[maxBoxes];
            m_textBlocks = new TextBlock[maxBoxes];

            m_lines = new List<Line>();
       
            if (colorBrush == null)
            {
                colorBrush = new SolidColorBrush(Colors.SpringGreen);
            }
            var lineBrush = new SolidColorBrush(Colors.DarkRed);
            m_canvas = canvas;
            for (int i = 0; i < maxBoxes; i++)
            {
                // Create rectangles
                m_rectangles[i] = new Rectangle();
                // Default configuration
                m_rectangles[i].Stroke = colorBrush;
                m_rectangles[i].StrokeThickness = lineThickness;
                // Hide
                m_rectangles[i].Visibility = Visibility.Collapsed;
                // Add to canvas
                m_canvas.Children.Add(m_rectangles[i]);

                // Create textblocks
                m_textBlocks[i] = new TextBlock();
                // Default configuration
                m_textBlocks[i].Foreground = colorBrush;
                m_textBlocks[i].FontSize = 18;
                // Hide
                m_textBlocks[i].Visibility = Visibility.Collapsed;
                // Add to canvas
                m_canvas.Children.Add(m_textBlocks[i]);

             

               
            }
        }
        public void RenderTrail(ref Tracker tracker)
        {
            var rnd = new Random();
            //clear line
            foreach (var line in m_lines)
            {
                m_canvas.Children.Remove(line);
            }
            m_lines.Clear();
            //draw line
            foreach (var item in tracker.Objects)
            {
                var colorBrush = new SolidColorBrush(Color.FromArgb(255, (byte)rnd.Next(1, 255), (byte)rnd.Next(1, 255), (byte)rnd.Next(1, 255)));
                if (item.Trails.Count > 1)
                {
                    for (int i = 0; i < item.Trails.Count - 1; i++)
                    {
                        var newline = new Line();
                        (newline.X1, newline.Y1) = (item.Trails[i].X, item.Trails[i].Y);
                        (newline.X2, newline.Y2) = (item.Trails[i + 1].X, item.Trails[i + 1].Y);

                        newline.Stroke = colorBrush;
                        newline.StrokeThickness = 2;
                        // Hide
                        newline.Visibility = Visibility.Visible;
                        m_lines.Add(newline);
                        m_canvas.Children.Add(newline);
                    }
                }
            }
        }
            /// <summary>
            /// Render bounding boxes from ObjectDetections
            /// </summary>
            /// <param name="detections"></param>
            public void Render(IList<PredictionModel> detections)
        {
            if (detections == null) return;
            int i = 0;
            m_rawRects.Clear();
            // Render detections up to MAX_BOXES
            for (i = 0; i < detections.Count && i < m_rectangles.Length; i++)
            {
                // Cache rect
                m_rawRects.Add(detections[i].BoundingBox);

                // Render bounding box
                m_rectangles[i].Width = detections[i].BoundingBox.Width * m_canvas.ActualWidth;
                m_rectangles[i].Height = detections[i].BoundingBox.Height * m_canvas.ActualHeight;
                Canvas.SetLeft(m_rectangles[i], detections[i].BoundingBox.Left * m_canvas.ActualWidth);
                Canvas.SetTop(m_rectangles[i], detections[i].BoundingBox.Top * m_canvas.ActualHeight);
                m_rectangles[i].Visibility = Visibility.Visible;

                // Render text label
                m_textBlocks[i].Text = $"{detections[i].TagName} - {detections[i].Probability * 100}%";
                Canvas.SetLeft(m_textBlocks[i], detections[i].BoundingBox.Left * m_canvas.ActualWidth + 2);
                Canvas.SetTop(m_textBlocks[i], detections[i].BoundingBox.Top * m_canvas.ActualHeight + 2);
                m_textBlocks[i].Visibility = Visibility.Visible;
            }
            // Hide all remaining boxes
            for (; i < m_rectangles.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_rectangles[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                m_rectangles[i].Visibility = Visibility.Collapsed;
                m_textBlocks[i].Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Resize canvas and rendered bounding boxes
        /// </summary>
        public void ResizeContent(SizeChangedEventArgs args)
        {
            // Resize rendered bboxes
            for (int i = 0; i < m_rectangles.Length && m_rectangles[i].Visibility == Visibility.Visible; i++)
            {
                // Update bounding box
                m_rectangles[i].Width = m_rawRects[i].Width * m_canvas.Width;
                m_rectangles[i].Height = m_rawRects[i].Height * m_canvas.Height;
                Canvas.SetLeft(m_rectangles[i], m_rawRects[i].Left * m_canvas.Width);
                Canvas.SetTop(m_rectangles[i], m_rawRects[i].Top * m_canvas.Height);

                // Update text label
                Canvas.SetLeft(m_textBlocks[i], m_rawRects[i].Left * m_canvas.Width + 2);
                Canvas.SetTop(m_textBlocks[i], m_rawRects[i].Top * m_canvas.Height + 2);

             
            }
          
          
        }


      
    }
}
