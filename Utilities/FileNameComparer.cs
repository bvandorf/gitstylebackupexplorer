using System;
using System.Collections.Generic;
using System.IO;

namespace gitstylebackupexplorer.Utilities
{
    /// <summary>
    /// Custom comparer for sorting backup version files numerically
    /// </summary>
    public class FileNameComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                if (y == null)
                {
                    return 1;
                }
                else
                {
                    FileInfo fiX = new FileInfo(x);
                    FileInfo fiY = new FileInfo(y);

                    int iX = int.Parse(fiX.Name);
                    int iY = int.Parse(fiY.Name);

                    if (iX > iY)
                        return 1;
                    else
                        return -1;
                }
            }
        }
    }
}
