using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Utils
{
    public static class TextExtensions
    {
        public static string PadRightIgnoreVowel(this string text, int totalWidth)
        {
            var textLen = GetLength(text);
            var len = totalWidth - textLen;
            for (int i = 0; i < len; i++){
                text += " ";
            }
            return text;
        }

        static int GetLength(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            int length = 0;
            foreach (var c in text.ToCharArray())
            {
                int code = (int)c;
                if (code != 3633
                        && code != 3636
                        && code != 3637
                        && code != 3638
                        && code != 3639
                        && code != 3640
                        && code != 3641
                        && code != 3642
                        && code != 3655
                        && code != 3656
                        && code != 3657
                        && code != 3658
                        && code != 3659
                        && code != 3660
                        && code != 3661
                        && code != 3662)
                {
                    length++;
                }
            }
            return length == 0 ? text.Length : length;
        }
    }
}
