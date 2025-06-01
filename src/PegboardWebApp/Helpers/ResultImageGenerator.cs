using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web;

namespace PegboardWebApp.Helpers
{
    public class ResultImageGenerator
    {
        Dictionary<string, Image> _cache = new Dictionary<string, Image>();
        int _resultWidth;
        int _resultHeight;
        Font _font;

        public ResultImageGenerator(int resultWidth, int resultHeight)
        {
            _font = new Font(FontFamily.GenericSansSerif, 10);
            _resultWidth = resultWidth;
            _resultHeight = resultHeight;

        }
        public Image CreateImage(string results)
        {
            if (!_cache.ContainsKey(results))
            {
                _cache[results] = GenerateImage(results);
            }
            return _cache[results];
        }

        private Image GenerateImage(string results)
        {
            if (results == string.Empty)
                return new Bitmap(1, 1);

            Bitmap bitmap = new Bitmap(_resultWidth * results.Length, _resultHeight);
            Graphics gfx = Graphics.FromImage(bitmap);
            for (int i = 0; i < results.Length; i++)
            {
                bool won = results[i] == 'W';
                Rectangle rect = new Rectangle(i * _resultWidth, 0, _resultWidth, _resultHeight);

                gfx.FillRectangle(won ? Brushes.Green : Brushes.Red, rect);
                gfx.DrawString(results[i].ToString(), _font, Brushes.White, rect.Left + 2, rect.Top + 2);
            }
            return bitmap;
        }
    }
}