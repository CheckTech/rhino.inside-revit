using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using System.Windows.Media.Imaging;

namespace RhinoInside
{
  static class ImageBuilder
  {
    static internal BitmapImage LoadBitmapImage(string name)
    {
      using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
      {
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = resource;
        bitmapImage.EndInit();
        return bitmapImage;
      }
    }

    public static BitmapImage ToBitmapImage(this Bitmap bitmap, int PixelWidth = 0, int PixelHeight = 0)
    {
      using (var memory = new MemoryStream())
      {
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.DecodePixelWidth = PixelWidth;
        bitmapImage.DecodePixelHeight = PixelHeight;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
      }
    }

    static public Bitmap BuildIcon(string tag, int width = 24, int height = 24)
    {
      var bitmap = new Bitmap(width, height);
      var g = System.Drawing.Graphics.FromImage(bitmap);

      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.InterpolationMode = InterpolationMode.HighQualityBicubic;
      g.PixelOffsetMode = PixelOffsetMode.HighQuality;
      var rect = new RectangleF(0.5f, 1.0f, width, height);

      var format = new StringFormat()
      {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
      };

      g.FillEllipse(Brushes.Black, 1.0f, 1.0f, width - 2.0f, height - 2.0f);
      float emSize = ((float) width - ((float) tag.Length * 4.0f));

      if (width == 24)
      {
        switch (tag.Length)
        {
          case 1: emSize = 20.0f; break;
          case 2: emSize = 15.0f; break;
          case 3: emSize = 11.0f; break;
          case 4: emSize = 8.0f; break;
        }
      }

      // Avoid using ClearType rendering on icons that the user can zoom in like icons on Grashopper components.
      g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
      g.DrawString(tag, new System.Drawing.Font("Calibri", emSize, GraphicsUnit.Pixel), Brushes.White, rect, format);
      return bitmap;
    }

    static public System.Windows.Media.ImageSource BuildImage(string tag)
    {
      return BuildIcon(tag, 64, 64).ToBitmapImage(32, 32);
    }
  }
}
