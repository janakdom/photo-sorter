using System.IO;
using System.Text;

namespace PhotoSorting
{
    class Crypto
    {
        public static string GetHash(string path, bool upperCase = false)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    byte[] bytes = md5.ComputeHash(stream);

                    StringBuilder result = new StringBuilder(bytes.Length * 2);
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
                    }
                    return result.ToString();
                }
            }
        }
    }
}
