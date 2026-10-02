using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
class TextureToRaw
{
    static void Main(string[] args)
    {
        using(var source=new Bitmap(args[0]))
        using(var image=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb))
        {
            using(var graphics=Graphics.FromImage(image))graphics.DrawImageUnscaled(source,0,0);
            var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            var row=new byte[image.Width*4];
            using(var output=new BinaryWriter(File.Create(args[1])))
            {
                output.Write(image.Width);output.Write(image.Height);
                for(int y=image.Height-1;y>=0;y--)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                    for(int x=0;x<row.Length;x+=4){byte b=row[x];row[x]=row[x+2];row[x+2]=b;}
                    output.Write(row);
                }
            }
            image.UnlockBits(data);
        }
    }
}
